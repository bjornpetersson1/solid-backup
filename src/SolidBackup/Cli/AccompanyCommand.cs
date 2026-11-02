using System.Globalization;

using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// Runs a recording through the whole chain (analysis, level features, harmony, synthesizer) and writes the voice
/// mixed with the accompaniment, or the accompaniment alone, to a WAV file, so the level features can be heard
/// before live mode exists. <c>--style</c>, <c>--key</c>, <c>--mode</c>, <c>--sound</c> and <c>--features</c>
/// override the settings for this run only; the recording's own sample rate is used.
/// </summary>
public sealed class AccompanyCommand(IAnsiConsole console, Func<SettingsLoadResult> loadSettings)
{
    public const string NoFeatures = "none";

    public int Run(string inputPath, string outputPath, AccompanyOptions options)
    {
        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);
        if (RecordingFiles.Read(console, inputPath) is not { } audio)
        {
            return 1;
        }

        AppSettings settings = (options.Rhythm?.Apply(loaded.Settings) ?? loaded.Settings) with
        {
            SampleRate = audio.SampleRate,
            HarmonyStyle = options.Style ?? loaded.Settings.HarmonyStyle,
            Key = options.Key ?? loaded.Settings.Key,
            Mode = options.Mode ?? loaded.Settings.Mode,
            Sound = options.Sound ?? loaded.Settings.Sound,
        };
        if (options.Features is { } features && !TrySwitch(settings, features, out settings))
        {
            return ConsoleMessages.Error(console, UnknownFeatureMessage(features));
        }

        if (HarmonySetup.Prepare(console, settings, options.Key is not null, options.Mode is not null) is null)
        {
            return 1;
        }

        float[] output = Accompany(audio.Samples, settings, options.NoVoice);
        if (!RecordingFiles.Write(console, outputPath, audio.SampleRate, output))
        {
            return 1;
        }

        console.WriteLine(WrittenMessage(outputPath, output.Length / (double)audio.SampleRate, settings));
        return 0;
    }

    /// <summary>The voice plus the accompaniment, or the accompaniment alone, through the same pipeline as live audio.</summary>
    public static float[] Accompany(float[] voice, AppSettings settings, bool noVoice)
    {
        int buffers = (voice.Length + settings.BufferSize - 1) / settings.BufferSize;
        using MemoryAudioInput input = new(voice, settings.SampleRate, settings.BufferSize);
        using MemoryAudioOutput accompaniment = new(settings.SampleRate, settings.BufferSize, buffers * settings.BufferSize);
        AudioPipeline pipeline = new(input, accompaniment, new Accompanist(settings));
        pipeline.Start();
        pipeline.Stop();

        // The input pads its last buffer with silence; the result is as long as the voice.
        float[] output = accompaniment.Samples[..voice.Length].ToArray();

        if (!noVoice)
        {
            for (int i = 0; i < output.Length; i++)
            {
                output[i] += voice[i];
            }
        }

        // Live, the voice and the chords never share a channel; in one file their sum may pass full scale, so the
        // whole file is turned down until it fits rather than clipped.
        float peak = output.Length == 0 ? 0 : output.Max(Math.Abs);
        if (peak > 1)
        {
            for (int i = 0; i < output.Length; i++)
            {
                output[i] /= peak;
            }
        }

        return output;
    }

    public static string UnknownFeatureMessage(string features)
    {
        string names = string.Join(", ", LevelFeatures.All.Select(entry => entry.Name));
        return $"--features \"{features}\" går inte; ange några av {names} med kommatecken emellan, eller {NoFeatures}.";
    }

    public static string WrittenMessage(string path, double seconds, AppSettings settings)
    {
        string on = string.Join(", ", LevelFeatures.All.Where(entry => entry.IsOn(settings)).Select(entry => entry.Name));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Skrev {path} ({seconds:0.0} s, ljudet {settings.Sound}, nivåfunktioner: {(on.Length == 0 ? NoFeatures : on)}).");
    }

    private static bool TrySwitch(AppSettings settings, string features, out AppSettings switched)
    {
        string[] names = features == NoFeatures
            ? []
            : features.Split(',', StringSplitOptions.TrimEntries);
        return LevelFeatures.TrySwitchOnly(settings, names, LevelFeatures.All, out switched);
    }
}

/// <summary>The per-run overrides of <c>--accompany</c>; null keeps the setting.</summary>
public sealed record AccompanyOptions(
    string? Style = null, string? Key = null, string? Mode = null, string? Sound = null, string? Features = null, bool NoVoice = false, RhythmOverrides? Rhythm = null);
