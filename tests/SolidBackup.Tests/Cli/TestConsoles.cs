using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

internal static class TestConsoles
{
    /// <summary>An interactive test console wide enough that no message wraps.</summary>
    public static TestConsole Wide()
    {
        TestConsole console = new TestConsole().Interactive();
        console.Profile.Width = 400;
        return console;
    }
}
