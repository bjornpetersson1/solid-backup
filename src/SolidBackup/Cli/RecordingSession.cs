using System.Globalization;

using SolidBackup.Audio;
using SolidBackup.Recording;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// The recording of a command that plays audio: opened once the devices are open and before the audio starts (so a
/// device error leaves no empty file), and finished after the audio has stopped, with the Swedish messages for both.
/// </summary>
public sealed class RecordingSession(string path, SessionRecorder recorder)
{
    public SessionRecorder Recorder => recorder;

    /// <summary>Opens a recording at the input's rate, or shows an error and returns null.</summary>
    public static RecordingSession? Open(IAnsiConsole console, IAudioInput input, string folder, Func<(string Path, Stream Stream)> create)
    {
        string? path = null;
        Stream? stream = null;
        try
        {
            (path, stream) = create();
            SessionRecorder recorder = new(stream, input.SampleRate, input.BufferSize);
            console.WriteLine(RecordingMessage(path));
            return new RecordingSession(path, recorder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            stream?.Dispose();
            DeleteQuietly(path);
            ConsoleMessages.Error(console, RecordingFolderMessage(folder));
            return null;
        }
    }

    public const string FullMessage = "Inspelningen nådde WAV-formatets gräns på 4 GB (cirka 6 timmar); resten sparades inte.";

    public static string RecordingMessage(string path)
    {
        return $"Spelar in till {path}.";
    }

    public static string RecordedMessage(string path, double seconds, long overruns)
    {
        return string.Create(CultureInfo.InvariantCulture, $"Inspelningen sparad: {path} ({seconds:0.0} s, tappade inspelningsbuffertar: {overruns}).");
    }

    public static string RecordingFolderMessage(string folder)
    {
        return $"Inspelningsmappen {folder} kunde inte skapas eller skrivas.";
    }

    /// <summary>Finishes the file and says how it went; false when it could not be written.</summary>
    public bool Finish(IAnsiConsole console)
    {
        try
        {
            recorder.Stop();
        }
        catch (RecordingFailedException exception)
        {
            ConsoleMessages.Error(console, exception.Message);
            return false;
        }

        console.WriteLine(RecordedMessage(path, recorder.Seconds, recorder.Overruns));
        if (recorder.IsFull)
        {
            ConsoleMessages.Warning(console, FullMessage);
        }

        return true;
    }

    /// <summary>Throws the recording away, for a session whose audio never started.</summary>
    public void Discard()
    {
        recorder.Dispose();
        DeleteQuietly(path);
    }

    // The file is the app's own and possibly half made; if it cannot be removed it stays, and the error that led here is the one to report.
    private static void DeleteQuietly(string? path)
    {
        try
        {
            if (path is not null)
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
