using SolidBackup.Analysis;

namespace SolidBackup.Configuration;

public sealed record SettingsError(string Setting, string Problem);

public static class SettingsValidator
{
    private static readonly int[] SupportedSampleRates = [44_100, 48_000];

    public static SettingsError? Validate(AppSettings settings)
    {
        if (!NoteName.TryParseMidiNumber(settings.LowestNote, out int lowest))
        {
            return new(nameof(AppSettings.LowestNote), $"\"{settings.LowestNote}\" är inget giltigt tonnamn");
        }

        if (!NoteName.TryParseMidiNumber(settings.HighestNote, out int highest))
        {
            return new(nameof(AppSettings.HighestNote), $"\"{settings.HighestNote}\" är inget giltigt tonnamn");
        }

        if (lowest >= highest)
        {
            return new(nameof(AppSettings.LowestNote), $"lägsta tonen {settings.LowestNote} måste vara lägre än högsta tonen {settings.HighestNote}");
        }

        if (!int.IsPow2(settings.BufferSize))
        {
            return new(nameof(AppSettings.BufferSize), $"{settings.BufferSize} är inte en positiv tvåpotens");
        }

        if (!SupportedSampleRates.Contains(settings.SampleRate))
        {
            return new(nameof(AppSettings.SampleRate), $"{settings.SampleRate} Hz stöds inte; välj {string.Join(" eller ", SupportedSampleRates)}");
        }

        if (settings.InputDevice is null)
        {
            return new(nameof(AppSettings.InputDevice), "ett enhetsnamn eller \"\" (systemets standard) krävs");
        }

        if (settings.OutputDevice is null)
        {
            return new(nameof(AppSettings.OutputDevice), "ett enhetsnamn eller \"\" (systemets standard) krävs");
        }

        if (settings.PitchConfidenceThreshold is not (>= 0 and <= 1))
        {
            return new(nameof(AppSettings.PitchConfidenceThreshold), $"{settings.PitchConfidenceThreshold} måste vara ett tal mellan 0 och 1");
        }

        if (!double.IsFinite(settings.GateThresholdDbfs) || settings.GateThresholdDbfs > 0)
        {
            return new(nameof(AppSettings.GateThresholdDbfs), $"{settings.GateThresholdDbfs} dBFS måste vara ett tal på högst 0 dBFS");
        }

        if (OutOfRange(settings.GateHysteresisDb, 40))
        {
            return new(nameof(AppSettings.GateHysteresisDb), $"{settings.GateHysteresisDb} dB måste vara större än 0 och högst 40 dB");
        }

        if (settings.GateThresholdDbfs - settings.GateHysteresisDb <= LevelMeter.FloorDbfs)
        {
            return new(nameof(AppSettings.GateThresholdDbfs), $"grinden stänger under {settings.GateThresholdDbfs - settings.GateHysteresisDb} dBFS, vilket nivåmätaren aldrig visar (lägst {LevelMeter.FloorDbfs} dBFS); höj tröskeln eller minska hysteresen");
        }

        if (OutOfRange(settings.GateHoldMs, 1000))
        {
            return new(nameof(AppSettings.GateHoldMs), $"{settings.GateHoldMs} ms måste vara större än 0 och högst 1000 ms");
        }

        if (OutOfRange(settings.OnsetThresholdDb, 60))
        {
            return new(nameof(AppSettings.OnsetThresholdDb), $"{settings.OnsetThresholdDb} dB måste vara större än 0 och högst 60 dB");
        }

        if (OutOfRange(settings.OnsetMinIntervalMs, 2000))
        {
            return new(nameof(AppSettings.OnsetMinIntervalMs), $"{settings.OnsetMinIntervalMs} ms måste vara större än 0 och högst 2000 ms");
        }

        if (settings.LatencyTargetMs <= 0)
        {
            return new(nameof(AppSettings.LatencyTargetMs), $"{settings.LatencyTargetMs} ms måste vara större än 0");
        }

        return null;
    }

    // Written as a negation so NaN, for which every comparison is false, is out of range too.
    private static bool OutOfRange(double value, double maximum)
    {
        return !(value > 0 && value <= maximum);
    }
}
