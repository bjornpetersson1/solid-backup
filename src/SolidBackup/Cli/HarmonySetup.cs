using SolidBackup.Configuration;
using SolidBackup.Harmony;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>Builds a run's harmony from its settings for the commands that analyse a recording (<c>--harmonize</c>, <c>--accompany</c>).</summary>
public static class HarmonySetup
{
    public const string ModeIgnoredMessage = "--mode gäller inte med tonarten auto, som bara skattar dur eller moll.";
    public const string KeyIgnoredMessage = "--key och --mode gäller inte med harmonistilen Fri, som inte har någon tonart.";

    /// <summary>
    /// Validates a run's settings and builds its harmony, warning when a given <c>--key</c> or <c>--mode</c> is
    /// ignored; null after an error has been shown.
    /// </summary>
    public static Harmonizer? Prepare(IAnsiConsole console, AppSettings settings, bool keyGiven, bool modeGiven)
    {
        if (ConsoleMessages.InvalidSettings(console, settings))
        {
            return null;
        }

        Harmonizer harmonizer = Harmonizer.FromSettings(settings);
        if ((keyGiven || modeGiven) && !harmonizer.UsesKey)
        {
            ConsoleMessages.Warning(console, KeyIgnoredMessage);
        }
        else if (modeGiven && harmonizer.Key is null)
        {
            ConsoleMessages.Warning(console, ModeIgnoredMessage);
        }

        return harmonizer;
    }
}
