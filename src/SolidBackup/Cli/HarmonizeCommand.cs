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
    public const string AutoKeyNotBuiltMessage = "Tonarten auto (tonartsdetektering) är inte byggd än; ange en fast tonart med --key.";
    public const string NoNotesMessage = "Inga toner hittades.";

    public int Run(string path, string? style = null, string? key = null, string? mode = null)
    {
        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);
        if (!File.Exists(path))
        {
            return ConsoleMessages.Error(console, MissingFileMessage(path));
        }

        WavAudio audio;
        try
        {
            audio = WavReader.Read(path);
        }
        catch (InvalidDataException exception)
        {
            return ConsoleMessages.Error(console, $"Filen {path} kunde inte läsas: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ConsoleMessages.Error(console, $"Filen {path} kunde inte läsas.");
        }

        AppSettings settings = loaded.Settings with
        {
            SampleRate = audio.SampleRate,
            HarmonyStyle = style ?? loaded.Settings.HarmonyStyle,
            Key = key ?? loaded.Settings.Key,
            Mode = mode ?? loaded.Settings.Mode,
        };
        if (SettingsValidator.Validate(settings) is { } error)
        {
            return ConsoleMessages.Error(console, $"Ogiltigt värde för {error.Setting}: {error.Problem}.");
        }

        switch (Harmonizer.Check(settings))
        {
            case HarmonizerProblem.StyleNotBuilt:
                return ConsoleMessages.Error(console, NotBuiltMessage(settings.HarmonyStyle));
            case HarmonizerProblem.AutoKey:
                return ConsoleMessages.Error(console, AutoKeyNotBuiltMessage);
        }

        Harmonizer harmonizer = Harmonizer.FromSettings(settings);
        console.WriteLine($"{path}: harmonistil {StyleName(settings.HarmonyStyle)}, tonart {PitchClass.Name(harmonizer.Key.Tonic)} {HarmonyNames.Mode(harmonizer.Key.Mode)}");
        ChangeRecorder recorder = new(PitchDetector.FromSettings(settings), harmonizer);
        using (MemoryAudioInput input = new(audio.Samples, audio.SampleRate, settings.BufferSize))
        {
            input.Start(recorder);
        }

        foreach (string line in recorder.Lines)
        {
            console.WriteLine(line);
        }

        if (recorder.Lines.Count == 0)
        {
            console.WriteLine(NoNotesMessage);
        }

        return 0;
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

    private static string StyleName(string style)
    {
        return HarmonySettings.TryParseName(style, out HarmonyStyle harmonyStyle) ? HarmonyNames.Style(harmonyStyle) : style;
    }

    // Runs offline over a file, so it may collect the lines as it goes.
    private sealed class ChangeRecorder(PitchDetector detector, Harmonizer harmonizer) : IAudioInputHandler
    {
        public List<string> Lines { get; } = [];

        public void OnBuffer(ReadOnlySpan<float> samples)
        {
            PitchEstimate estimate = detector.Process(samples);
            if (harmonizer.Process(estimate) && harmonizer.Chord is { } chord)
            {
                Lines.Add(ChangeLine(estimate, chord));
            }
        }
    }
}
