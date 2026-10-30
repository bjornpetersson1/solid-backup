using SolidBackup.Cli;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public class MainMenuTests
{
    [Fact]
    public void Run_ChoosingQuit_ShowsTitleAndChoiceAndReturnsZero()
    {
        TestConsole console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);

        int exitCode = new MainMenu(console).Run();

        Assert.Equal(0, exitCode);
        Assert.Contains(MainMenu.Title, console.Output);
        Assert.Contains(MainMenu.QuitChoice, console.Output);
    }

    [Fact]
    public void Run_NonInteractiveTerminal_ShowsSwedishMessageAndReturnsOne()
    {
        TestConsole console = new();

        int exitCode = new MainMenu(console).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains(MainMenu.NotInteractiveMessage, console.Output);
    }
}
