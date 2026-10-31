using System.Text.Json;

using SolidBackup.Configuration;
using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

public sealed class CorpusRangeTests
{
    [Fact]
    public void Range_IsTheDefaultRangeInAppsettings()
    {
        using JsonDocument defaults = JsonDocument.Parse(File.ReadAllText(SettingsStore.BundledDefaultsPath));

        Assert.True(NoteName.TryParseMidiNumber(defaults.RootElement.GetProperty(nameof(AppSettings.LowestNote)).GetString(), out int lowest));
        Assert.True(NoteName.TryParseMidiNumber(defaults.RootElement.GetProperty(nameof(AppSettings.HighestNote)).GetString(), out int highest));
        Assert.Equal((lowest, highest), (CorpusRange.LowestMidi, CorpusRange.HighestMidi));
    }

    [Theory]
    [InlineData(6900, 440)]
    [InlineData(5700, 220)]
    [InlineData(8100, 880)]
    public void CentsAndFrequency_AreInverse(double cents, double frequency)
    {
        Assert.Equal(frequency, CorpusRange.Frequency(cents), 1e-9);
        Assert.Equal(cents, CorpusRange.Cents(frequency), 1e-9);
    }
}
