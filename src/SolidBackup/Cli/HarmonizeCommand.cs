using System.Globalization;

using SolidBackup.Analysis;
using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Harmony;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// Runs the pitch detector and the harmony over a WAV file and prints a line per chord change, so the
/// styles can be tried on a recording before there is a synthesizer. <c>--style</c>, <c>--key</c> and
/// <c>--mode</c> override the settings for this run only; the file's own sample rate is used.
/// </summary>
public sealed class HarmonizeCommand(IAnsiConsole console, Func<SettingsLoadResult> loadSettings)
{
    public const string NoNotesMessage = "Inga toner hittades.";
    public const string NoKeyMessage = "Ingen tonart hittades: inspelningen är för kort eller för tvetydig för tonarten auto. Ange en fast tonart med --key.";
    public const string ModeIgnoredMessage = "--mode gäller inte med tonarten auto, som bara skattar dur eller moll.";
    public const string KeyIgnoredMessage = "--key och --mode gäller inte med harmonistilen Fri, som inte har någon tonart.";

    public int Run(string path, string? style = null, string? key = null, string? mode = null)
    {
        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);
        if (RecordingFiles.Read(console, path) is not { } audio)
        {
            return 1;
        }

        AppSettings settings = loaded.Settings with
        {
            SampleRate = audio.SampleRate,
            HarmonyStyle = style ?? loaded.Settings.HarmonyStyle,
            Key = key ?? loaded.Settings.Key,
            Mode = mode ?? loaded.Settings.Mode,
        };
        if (PrepareHarmony(console, settings, key is not null, mode is not null) is not { } harmonizer)
        {
            return 1;
        }

        string keyName = !harmonizer.UsesKey ? "–" : harmonizer.Key is { } fixedKey ? HarmonyNames.Key(fixedKey) : HarmonySettings.AutoKey;
        console.WriteLine($"{path}: harmonistil {StyleName(settings.HarmonyStyle)}, tonart {keyName}");
        ChangeRecorder recorder = new(PitchDetector.FromSettings(settings), harmonizer);
        using (MemoryAudioInput input = new(audio.Samples, audio.SampleRate, settings.BufferSize))
        {
            input.Start(recorder);
        }

        foreach (string line in recorder.Lines)
        {
            console.WriteLine(line);
        }

        if (!recorder.HeardNotes)
        {
            console.WriteLine(NoNotesMessage);
        }
        else if (harmonizer.Key is null && harmonizer.UsesKey)
        {
            console.WriteLine(NoKeyMessage);
        }

        return 0;
    }

    /// <summary>
    /// Validates a run's settings and builds its harmony, warning when a given <c>--key</c> or <c>--mode</c> is
    /// ignored; null after an error has been shown. Shared by <c>--harmonize</c> and <c>--accompany</c>.
    /// </summary>
    internal static Harmonizer? PrepareHarmony(IAnsiConsole console, AppSettings settings, bool keyGiven, bool modeGiven)
    {
        if (SettingsValidator.Validate(settings) is { } error)
        {
            ConsoleMessages.Error(console, $"Ogiltigt värde för {error.Setting}: {error.Problem}.");
            return null;
        }

        if (!Harmonizer.HasBuiltStyle(settings))
        {
            ConsoleMessages.Error(console, NotBuiltMessage(settings.HarmonyStyle));
            return null;
        }

        Harmonizer harmonizer = Harmonizer.FromSettings(settings);
        if ((keyGiven || modeGiven) && !harmonizer.UsesKey)
        {
            ConsoleMessages.Warning(console, KeyIgnoredMessage);
        }
        else if (modeGiven && harmonizer.Key is null)
        {
            ConsoleMessages.Warning(console, ModeIgnoredMessage);
        }

        return harmonizer;
    }

    public static string MissingFileMessage(string path)
    {
        return $"Filen {path} finns inte.";
    }

    public static string NotBuiltMessage(string style)
    {
        return $"Harmonistilen {StyleName(style)} är inte byggd än.";
    }

    /// <summary>A chord change as printed, e.g. "1.25 s  A4   Am".</summary>
    public static string ChangeLine(PitchEstimate estimate, Chord chord)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{estimate.TimeMs / 1000:0.00} s  {PitchClass.NoteName(estimate.MidiNote),-4} {chord}");
    }

    /// <summary>A key found or changed by <c>auto</c>, or moved by Blues, as printed, e.g. "2.40 s  tonart A-moll".</summary>
    public static string KeyLine(PitchEstimate estimate, Key key)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{estimate.TimeMs / 1000:0.00} s  tonart {HarmonyNames.Key(key)}");
    }

    private static string StyleName(string style)
    {
        return SettingNames.TryParse(style, out HarmonyStyle harmonyStyle) ? HarmonyNames.Style(harmonyStyle) : style;
    }

    // Runs offline over a file, so it may collect the lines as it goes.
    private sealed class ChangeRecorder(PitchDetector detector, Harmonizer harmonizer) : IAudioInputHandler
    {
        public List<string> Lines { get; } = [];

        public bool HeardNotes { get; private set; }

        public void OnBuffer(ReadOnlySpan<float> samples)
        {
            PitchEstimate estimate = detector.Process(samples);
            HeardNotes |= estimate.HasNote;
            Key? keyBefore = harmonizer.CurrentKey;
            bool chordChanged = harmonizer.Process(estimate);
            if (harmonizer.CurrentKey is { } key && key != keyBefore)
            {
                Lines.Add(KeyLine(estimate, key));
            }

            if (chordChanged && harmonizer.Chord is { } chord)
            {
                Lines.Add(ChangeLine(estimate, chord));
            }
        }
    }
}
