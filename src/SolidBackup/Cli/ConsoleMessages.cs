using SolidBackup.Audio;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>How the commands show settings problems and audio errors, in one style.</summary>
internal static class ConsoleMessages
{
    public static void SettingsMessage(IAnsiConsole console, SettingsLoadResult loaded)
    {
        if (loaded.Message is { } message)
        {
            console.MarkupLineInterpolated($"[yellow]{message}[/]");
        }
    }

    public static int AudioError(IAnsiConsole console, AudioDeviceException exception)
    {
        console.MarkupLineInterpolated($"[red]Ljudfel: {exception.Message}[/]");
        return 1;
    }
}
