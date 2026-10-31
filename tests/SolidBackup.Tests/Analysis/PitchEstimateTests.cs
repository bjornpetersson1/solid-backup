using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

public sealed class PitchEstimateTests
{
    [Theory]
    [InlineData(6912, 69, 12, "A4 +12 cent")]
    [InlineData(6888, 69, -12, "A4 -12 cent")]
    [InlineData(6950, 70, -50, "A#4 -50 cent")]
    [InlineData(4000, 40, 0, "E2 0 cent")]
    public void Note_IsTheNearestWithItsDeviation(double midiCents, int note, double cents, string text)
    {
        PitchEstimate estimate = new(0, true, midiCents, 1);

        Assert.Equal(note, estimate.MidiNote);
        Assert.Equal(cents, estimate.Cents, 1e-9);
        Assert.Equal(text, estimate.ToString());
    }

    [Fact]
    public void NoNote_ShowsADashAndHasNoFrequency()
    {
        Assert.Equal("–", PitchEstimate.NoNote(0, 0).ToString());
        Assert.Equal(0, PitchEstimate.NoNote(0, 0).Frequency);
    }

    [Fact]
    public void Frequency_FollowsTheCents()
    {
        Assert.Equal(440, new PitchEstimate(0, true, 6900, 1).Frequency, 1e-9);
    }
}
