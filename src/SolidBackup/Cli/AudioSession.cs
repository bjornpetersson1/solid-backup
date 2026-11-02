using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;
using SolidBackup.Recording;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// What a command that plays live audio (<c>--passthrough</c>, <c>--live</c>) does around it: it stops before opening
/// anything when the microphone is blocked, warns when the output is the built-in speakers, opens a recording when
/// asked, and on every way of stopping closes the audio before finishing the recording, then prints the summary.
/// Once a recording is open, Ctrl+C never ends the process with the file unfinished: before the audio runs (the open
/// can block on macOS's permission dialog) it throws the file away and lets the process end as usual; from then on it
/// asks the activity to stop, and the audio and the file are closed in order.
/// </summary>
internal sealed class AudioSession(
    IAnsiConsole console,
    Func<AppSettings, IAudioDevices> openDevices,
    AudioPreflight preflight,
    Func<AppSettings, (string Path, Stream Stream)>? createRecording = null)
{
    private readonly Func<AppSettings, (string Path, Stream Stream)> _createRecording =
        createRecording ?? (settings => RecordingFile.Create(settings.RecordingFolder, DateTime.Now));

    /// <summary>
    /// Runs the activity <paramref name="createActivity"/> makes on the devices the settings name; it is made after the
    /// microphone check and before anything opens. The exit code is 0 when it ran and the recording, if any, was written.
    /// </summary>
    public int Run(AppSettings settings, bool record, Func<IAudioActivity> createActivity, CancellationToken cancellation)
    {
        MicrophoneCheck microphone = preflight.CheckMicrophone();
        if (microphone is { Blocked: true, Message: { } problem })
        {
            return ConsoleMessages.Error(console, problem);
        }

        if (microphone.Message is { } notice)
        {
            ConsoleMessages.Warning(console, notice);
        }

        IAudioActivity activity = createActivity();
        try
        {
            using IAudioDevices devices = openDevices(settings);
            if (preflight.SpeakerProblem(devices.OutputDeviceName) is { } warning)
            {
                ConsoleMessages.Warning(console, warning);
            }

            RecordingSession? recording = null;
            if (record)
            {
                recording = RecordingSession.Open(console, devices.Input, settings.RecordingFolder, () => _createRecording(settings));
                if (recording is null)
                {
                    return 1;
                }
            }

            using CancellationTokenSource interrupt = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            bool running = false;
            ConsoleCancelEventHandler onCtrlC = (_, args) =>
            {
                if (!Volatile.Read(ref running))
                {
                    recording?.Discard();
                    return;
                }

                args.Cancel = true;
                interrupt.Cancel();
            };
            Console.CancelKeyPress += onCtrlC;
            try
            {
                return RunStarted(activity, devices, recording, () => Volatile.Write(ref running, true), interrupt.Token);
            }
            finally
            {
                Console.CancelKeyPress -= onCtrlC;
            }
        }
        catch (AudioDeviceException exception)
        {
            return ConsoleMessages.AudioError(console, exception);
        }
    }

    private int RunStarted(IAudioActivity activity, IAudioDevices devices, RecordingSession? recording, Action started, CancellationToken interrupt)
    {
        try
        {
            activity.Start(devices, recording?.Recorder);
        }
        catch
        {
            recording?.Discard();
            throw;
        }

        started();
        bool recorded = true;
        try
        {
            activity.WaitForStop(console, interrupt);
        }
        finally
        {
            // The audio stops before the file is finished, and the file is finished even if stopping fails.
            try
            {
                activity.Stop();
            }
            finally
            {
                recorded = recording?.Finish(console) ?? true;
            }
        }

        console.WriteLine(activity.Summary());
        return recorded ? 0 : 1;
    }
}

/// <summary>The audio an <see cref="AudioSession"/> runs.</summary>
internal interface IAudioActivity
{
    /// <summary>Starts the audio on the opened devices, with the recorder when recording.</summary>
    void Start(IAudioDevices devices, SessionRecorder? recorder);

    /// <summary>Returns when the user stops it (q, Esc, Ctrl+C) or on <paramref name="cancellation"/>, which Ctrl+C also sets.</summary>
    void WaitForStop(IAnsiConsole console, CancellationToken cancellation);

    /// <summary>Stops the audio; called after every start, also after an error.</summary>
    void Stop();

    /// <summary>What the run did, e.g. its dropouts; called after it has stopped.</summary>
    string Summary();
}
