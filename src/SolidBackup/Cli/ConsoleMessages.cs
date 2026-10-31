using SolidBackup.Audio;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>How the commands show settings problems and audio errors, in one style.</summary>
internal static class ConsoleMessages
{
    public static void Warning(IAnsiConsole console, string text)
    {
        console.MarkupLineInterpolated($"[yellow]{text}[/]");
    }

    /// <summary>Shows the error and returns the exit code 1.</summary>
    public static int Error(IAnsiConsole console, string text)
    {
        console.MarkupLineInterpolated($"[red]{text}[/]");
        return 1;
    }

    public static void SettingsMessage(IAnsiConsole console, SettingsLoadResult loaded)
    {
        if (loaded.Message is { } message)
        {
            Warning(console, message);
        }
    }

    public static int AudioError(IAnsiConsole console, AudioDeviceException exception)
    {
        return Error(console, $"Ljudfel: {exception.Message}");
    }
}
