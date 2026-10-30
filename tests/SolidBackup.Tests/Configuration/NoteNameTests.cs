using SolidBackup.Configuration;

namespace SolidBackup.Tests.Configuration;

public class NoteNameTests
{
    [Theory]
    [InlineData("E2", 40)]
    [InlineData("C6", 84)]
    [InlineData("A4", 69)]
    [InlineData("F#3", 54)]
    [InlineData("Bb3", 58)]
    [InlineData("C-1", 0)]
    public void TryParseMidiNumber_ValidName_ReturnsMidiNumber(string name, int expected)
    {
        bool parsed = NoteName.TryParseMidiNumber(name, out int midiNumber);

        Assert.True(parsed);
        Assert.Equal(expected, midiNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("E")]
    [InlineData("H2")]
    [InlineData("E#")]
    [InlineData("Ex2")]
    [InlineData("A9")]
    [InlineData("E10")]
    [InlineData("E+2")]
    [InlineData("E02")]
    [InlineData("C2147483647")]
    public void TryParseMidiNumber_InvalidName_ReturnsFalse(string? name)
    {
        Assert.False(NoteName.TryParseMidiNumber(name, out _));
    }
}
