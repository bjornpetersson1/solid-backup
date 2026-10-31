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
    [InlineData(9649, true)]
    [InlineData(9650, false)]
    [InlineData(3950, true)]
    [InlineData(3949, false)]
    public void PitchIsInTheRange_ByItsNearestNote(double midiCents, bool expected)
    {
        Assert.Equal(expected, CorpusRange.ContainsPitch(midiCents));
    }
}
