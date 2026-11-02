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

    public int Run(string path, string? style = null, string? key = null, string? mode = null, RhythmOverrides? rhythm = null)
    {
        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);
        if (RecordingFiles.Read(console, path) is not { } audio)
        {
            return 1;
        }

        AppSettings settings = (rhythm?.Apply(loaded.Settings) ?? loaded.Settings) with
        {
            SampleRate = audio.SampleRate,
            HarmonyStyle = style ?? loaded.Settings.HarmonyStyle,
            Key = key ?? loaded.Settings.Key,
            Mode = mode ?? loaded.Settings.Mode,
        };
        if (HarmonySetup.Prepare(console, settings, key is not null, mode is not null) is not { } harmonizer)
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

    /// <summary>A chord change as printed, e.g. "1.25 s  A4   Am".</summary>
    /// <remarks>A change that a style makes on its own in silence (Jazz's loop) shows "–" for the note.</remarks>
    public static string ChangeLine(PitchEstimate estimate, Chord chord)
    {
        string note = estimate.HasNote ? PitchClass.NoteName(estimate.MidiNote) : "–";
        return string.Create(CultureInfo.InvariantCulture, $"{estimate.TimeMs / 1000:0.00} s  {note,-4} {chord}");
    }

    /// <summary>A key found or changed by <c>auto</c>, or moved by Blues or Jazz, as printed, e.g. "2.40 s  tonart A-moll".</summary>
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
