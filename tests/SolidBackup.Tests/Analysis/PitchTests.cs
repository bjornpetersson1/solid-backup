using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

public sealed class PitchTests
{
    [Theory]
    [InlineData(6900, 440)]
    [InlineData(5700, 220)]
    [InlineData(8100, 880)]
    public void CentsAndFrequency_AreInverse(double cents, double frequency)
    {
        Assert.Equal(frequency, Pitch.Frequency(cents), 1e-9);
        Assert.Equal(cents, Pitch.Cents(frequency), 1e-9);
    }

    [Theory]
    [InlineData(6949.9, 69)]
    [InlineData(6950, 70)]
    [InlineData(6850, 69)]
    public void NearestMidi_CountsHalfwayAsTheUpperNote(double midiCents, int expected)
    {
        Assert.Equal(expected, Pitch.NearestMidi(midiCents));
    }

    [Theory]
    [InlineData(1, 0, 3, true, -0.25)]
    [InlineData(3, 0, 1, true, 0.25)]
    [InlineData(-1, 0, -3, false, -0.25)]
    [InlineData(0, 1, 2, true, 0)]
    [InlineData(1, 0, 1, false, 0)]
    public void ParabolaOffset_FindsTheExtremeOfTheRightKind(double left, double centre, double right, bool minimum, double expected)
    {
        Assert.Equal(expected, Pitch.ParabolaOffset(left, centre, right, minimum), 1e-12);
    }
}
