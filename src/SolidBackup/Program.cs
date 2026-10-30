using SolidBackup.Cli;

using Spectre.Console;

namespace SolidBackup;

public static class Program
{
    public static int Main(string[] args)
    {
        // Ctrl+C ends the process without running the prompt's cleanup, so show the cursor it hid.
        Console.CancelKeyPress += (_, _) => AnsiConsole.Cursor.Show();
        return AppCommand.Run(args, CreateConsole);
    }

    private static IAnsiConsole CreateConsole()
    {
        // Spectre's own detection treats every macOS session as interactive, even with piped input.
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Interactive = Console.IsInputRedirected ? InteractionSupport.No : InteractionSupport.Detect,
        });
    }
}
