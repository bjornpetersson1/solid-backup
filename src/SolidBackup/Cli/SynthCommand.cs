using System.Globalization;

using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Synthesis;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// Renders chords with the synthesizer to a WAV file (<c>--render</c>) or plays them on the output device
/// (<c>--play</c>), so a sound can be heard on its own, without singing. <c>--sound</c> overrides the setting
/// for this run only.
/// </summary>
public sealed class SynthCommand(
    IAnsiConsole console,
    Func<SettingsLoadResult> loadSettings,
    Func<AppSettings, IAudioPlayback> openPlayback,
    AudioPreflight preflight)
{
    public const string DefaultChords = "C Am F G";
    public const int DefaultChordMs = 1000;
    public const int MinChordMs = 100;
    public const int MaxChordMs = 10_000;
    public const int MaxTotalMinutes = 10;
    public static readonly string TooLongMessage = $"Ackorden blir längre än {MaxTotalMinutes} minuter; använd färre ackord eller ett kortare --chord-ms.";
    public const string NoChordsMessage = "--chords måste innehålla minst ett ackord.";
    public const string PlayingMessage = "Spelar ackorden i utgången. Tryck q eller Esc för att sluta.";
    public const string PlayedMessage = "Uppspelningen är klar.";
    public const string StoppedMessage = "Uppspelningen stoppad.";
    public const string NotInteractiveMessage = "Uppspelningen kräver en interaktiv terminal.";

    public int Render(string path, string? sound = null, string? chords = null, int? chordMs = null, RhythmOverrides? rhythm = null)
    {
        if (Prepare(sound, chords, chordMs, rhythm) is not { } prepared)
        {
            return 1;
        }

        if (!RecordingFiles.Write(console, path, prepared.Settings.SampleRate, prepared.Samples))
        {
            return 1;
        }

        console.WriteLine(RenderedMessage(path, prepared.Samples.Length / (double)prepared.Settings.SampleRate, prepared.Sound));
        return 0;
    }

    /// <summary>Plays until the end, a stop key, Ctrl+C or <paramref name="cancellation"/>.</summary>
    public int Play(string? sound = null, string? chords = null, int? chordMs = null, RhythmOverrides? rhythm = null, CancellationToken cancellation = default)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            return ConsoleMessages.Error(console, NotInteractiveMessage);
        }

        if (Prepare(sound, chords, chordMs, rhythm) is not { } prepared)
        {
            return 1;
        }

        try
        {
            using IAudioPlayback playback = openPlayback(prepared.Settings);
            if (preflight.SpeakerProblem(playback.OutputDeviceName) is { } warning)
            {
                ConsoleMessages.Warning(console, warning);
            }

            playback.Start(prepared.Samples);
            console.WriteLine(PlayingMessage);
            bool finished = StopKeys.WaitForStop(console, () => playback.IsFinished, cancellation) == StopReason.Finished;
            playback.Stop();
            console.WriteLine(PlayedSummary(finished, playback.Underflows));
            return 0;
        }
        catch (AudioDeviceException exception)
        {
            return ConsoleMessages.AudioError(console, exception);
        }
    }

    public static string RenderedMessage(string path, double seconds, Sound sound)
    {
        return string.Create(CultureInfo.InvariantCulture, $"Skrev {path} ({seconds:0.0} s, ljudet {sound}).");
    }

    public static string PlayedSummary(bool finished, long underflows)
    {
        return $"{(finished ? PlayedMessage : StoppedMessage)} Underflöden i utgången: {underflows}.";
    }

    public static string UnknownChordMessage(string symbol)
    {
        return $"\"{symbol}\" är inget känt ackord; skriv t.ex. C, Am, G7 eller Cmaj7(#11).";
    }

    public static string ChordMsMessage(int chordMs)
    {
        return $"--chord-ms måste vara mellan {MinChordMs} och {MaxChordMs} ms; {chordMs} går inte.";
    }

    private Prepared? Prepare(string? sound, string? chords, int? chordMs, RhythmOverrides? rhythm)
    {
        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);
        AppSettings withSound = loaded.Settings with { Sound = sound ?? loaded.Settings.Sound };
        AppSettings settings = rhythm?.Apply(withSound) ?? withSound;
        if (ConsoleMessages.InvalidSettings(console, settings))
        {
            return null;
        }

        int duration = chordMs ?? DefaultChordMs;
        if (duration is < MinChordMs or > MaxChordMs)
        {
            ConsoleMessages.Error(console, ChordMsMessage(duration));
            return null;
        }

        string[] symbols = (chords ?? DefaultChords).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (symbols.Length == 0)
        {
            ConsoleMessages.Error(console, NoChordsMessage);
            return null;
        }

        if ((long)symbols.Length * duration > MaxTotalMinutes * 60_000L)
        {
            ConsoleMessages.Error(console, TooLongMessage);
            return null;
        }

        List<Chord> parsed = [];
        foreach (string symbol in symbols)
        {
            if (!ChordSymbol.TryParse(symbol, out Chord chord))
            {
                ConsoleMessages.Error(console, UnknownChordMessage(symbol));
                return null;
            }

            parsed.Add(chord);
        }

        return new Prepared(settings, Synthesizer.SoundIn(settings), ChordSequence.Render(settings, parsed, duration));
    }

    private sealed record Prepared(AppSettings Settings, Sound Sound, float[] Samples);
}
