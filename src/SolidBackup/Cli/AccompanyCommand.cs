using System.Globalization;

using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// Runs a recording through the whole chain (analysis, level features, harmony, synthesizer) and writes the voice
/// mixed with the accompaniment, or the accompaniment alone, to a WAV file, as live mode would play it. The one-run
/// options (<see cref="SettingOverrides"/>) override the settings for this run only; the recording's own sample rate
/// is used.
/// </summary>
public sealed class AccompanyCommand(IAnsiConsole console, Func<SettingsLoadResult> loadSettings)
{
    public int Run(string inputPath, string outputPath, AccompanyOptions options)
    {
        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);
        if (RecordingFiles.Read(console, inputPath) is not { } audio)
        {
            return 1;
        }

        if (!options.Overrides.TryApply(loaded.Settings with { SampleRate = audio.SampleRate }, out AppSettings settings, out string? error))
        {
            return ConsoleMessages.Error(console, error);
        }

        if (HarmonySetup.Prepare(console, settings, options.Overrides.Key is not null, options.Overrides.Mode is not null) is null)
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

    public static string WrittenMessage(string path, double seconds, AppSettings settings)
    {
        string on = string.Join(", ", LevelFeatures.NamesOn(settings));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Skrev {path} ({seconds:0.0} s, ljudet {settings.Sound}, nivåfunktioner: {(on.Length == 0 ? SettingOverrides.NoFeatures : on)}{(settings.Autoplay ? ", autospel på" : "")}).");
    }
}

/// <summary>The per-run options of <c>--accompany</c>: the overrides it shares with <c>--live</c>, and whether to leave the voice out.</summary>
public sealed record AccompanyOptions(SettingOverrides Overrides, bool NoVoice = false);
