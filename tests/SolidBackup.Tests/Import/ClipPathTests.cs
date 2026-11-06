using SolidBackup.Import;

namespace SolidBackup.Tests.Import;

public class ClipPathTests
{
    [Theory]
    [InlineData("/Users/a/Röstmemo 1.m4a", "/Users/a/Röstmemo 1.m4a")]
    [InlineData("  /Users/a/klipp.wav  ", "/Users/a/klipp.wav")]
    [InlineData("'/Users/a/Röstmemo 1.m4a'", "/Users/a/Röstmemo 1.m4a")]
    [InlineData("\"/Users/a/Röstmemo 1.m4a\"", "/Users/a/Röstmemo 1.m4a")]
    [InlineData("/Users/a/R\\u00f6stmemo\\ 1.m4a", "/Users/a/Ru00f6stmemo 1.m4a")]
    [InlineData("/Users/a/Min\\ melodi\\ \\(1\\).m4a", "/Users/a/Min melodi (1).m4a")]
    [InlineData("/x/take\\  ", "/x/take ")]
    [InlineData("'/x/it'\\''s.m4a'", "/x/it's.m4a")]
    public void ADraggedOrTypedPath_LosesItsQuotesAndEscapes(string typed, string path)
    {
        Assert.Equal(path, ClipPath.Parse(typed));
    }

    [Theory]
    [InlineData("~/Downloads/klipp.wav", "Downloads/klipp.wav")]
    [InlineData("'~/Musik/a b.wav'", "Musik/a b.wav")]
    public void ATilde_IsTheHomeFolder(string typed, string underHome)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(Path.Join(home, underHome), ClipPath.Parse(typed));
    }
}
