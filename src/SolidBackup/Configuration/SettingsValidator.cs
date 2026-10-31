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

        if (!double.IsFinite(settings.GateThresholdDbfs) || settings.GateThresholdDbfs > 0)
        {
            return new(nameof(AppSettings.GateThresholdDbfs), $"{settings.GateThresholdDbfs} dBFS måste vara ett tal på högst 0 dBFS");
        }

        if (settings.LatencyTargetMs <= 0)
        {
            return new(nameof(AppSettings.LatencyTargetMs), $"{settings.LatencyTargetMs} ms måste vara större än 0");
        }

        return null;
    }
}
