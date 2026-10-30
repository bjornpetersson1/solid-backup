using Spectre.Console;

namespace SolidBackup.Cli;

public sealed class MainMenu(IAnsiConsole console)
{
    public const string Title = "solid-backup";
    public const string QuitChoice = "Avsluta";
    public const string NotInteractiveMessage = "Menyn kräver en interaktiv terminal.";

    public int Run()
    {
        console.Write(new Rule($"[bold]{Title}[/]"));
        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLine($"[red]{NotInteractiveMessage}[/]");
            return 1;
        }

        console.Prompt(new SelectionPrompt<string>()
            .Title("Vad vill du göra?")
            .AddChoices(QuitChoice));
        return 0;
    }
}
