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
}
