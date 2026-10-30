namespace SolidBackup.Tests;

public class ProgramTests
{
    [Fact]
    public void AppAssembly_IsReferenced()
    {
        Assert.Equal("SolidBackup", typeof(Program).Assembly.GetName().Name);
    }
}
