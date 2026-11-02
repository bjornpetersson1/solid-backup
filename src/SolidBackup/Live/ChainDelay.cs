using SolidBackup.Accompaniment;
using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Rhythm;

namespace SolidBackup.Live;

/// <summary>
/// The chain's part of the latency: a generated tone after silence runs offline through a fresh chain with the given
/// settings but the Held pattern (the step wait of Arpeggio and Strum is musical timing, not latency,
/// <c>docs/rhythm.md</c>), and the delay is the time from the tone's first sample until the chord is heard, i.e. the
/// first output sample above <see cref="HeardDbfs"/>, when the chord's envelope has started. With the key
/// <c>auto</c> it is measured in C Ionian: the estimate needs <c>KeyFirstSeconds</c> of notes before the first chord, a
/// wait at the start of a session rather than latency.
/// </summary>
public static class ChainDelay
{
    public const int ToneMidi = 57;
    public const double ToneDbfs = -20;
    public const double SilenceMs = 500;
    public const double HeardDbfs = -60;

    /// <summary>How long the tone may play without a chord before the delay counts as unknown.</summary>
    public const double MaxToneMs = 150;

    /// <summary>The delay in milliseconds, or null when the tone never gives a chord (e.g. a gate threshold above the tone).</summary>
    public static double? MeasureMs(AppSettings settings)
    {
        AppSettings measured = settings with { RhythmPattern = nameof(RhythmPatternName.Held) };
        if (settings.Key == HarmonySettings.AutoKey)
        {
            measured = measured with { Key = "C", Mode = nameof(Mode.Ionian) };
        }

        Accompanist chain = new(measured);
        int rate = settings.SampleRate;
        float[] input = new float[settings.BufferSize];
        float[] output = new float[settings.BufferSize];
        long toneStart = (long)(SilenceMs * rate / 1000);
        long end = toneStart + (long)(MaxToneMs * rate / 1000);
        double amplitude = Math.Pow(10, ToneDbfs / 20);
        double radiansPerSample = 2 * Math.PI * Pitch.Frequency(ToneMidi * 100) / rate;
        float heard = (float)Math.Pow(10, HeardDbfs / 20);
        for (long start = 0; start < end; start += input.Length)
        {
            for (int i = 0; i < input.Length; i++)
            {
                long sample = start + i - toneStart;
                input[i] = sample < 0 ? 0 : (float)(amplitude * Math.Sin(radiansPerSample * sample));
            }

            chain.Process(input, output);
            for (int i = 0; i < output.Length; i++)
            {
                if (start + i >= toneStart && Math.Abs(output[i]) > heard)
                {
                    return (start + i - toneStart) * 1000.0 / rate;
                }
            }
        }

        return null;
    }
}
