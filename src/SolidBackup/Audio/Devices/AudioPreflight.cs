namespace SolidBackup.Audio.Devices;

/// <summary>The checks before audio starts: microphone permission and the speaker warning.</summary>
public sealed class AudioPreflight(IMicrophonePermissionSource permission, IOutputRouteSource routes, string terminalName)
{
    public const string UnknownTerminal = "terminalappen";

    public const string SpeakerWarning =
        "Varning: ljudet går till de inbyggda högtalarna. Använd hörlurar, annars når ackorden mikrofonen (det finns ingen ekosläckning).";

    /// <summary>The terminal app as the user knows it, from TERM_PROGRAM.</summary>
    public static string TerminalName(string? termProgram)
    {
        return termProgram switch
        {
            "Apple_Terminal" => "Terminal",
            "iTerm.app" => "iTerm",
            "vscode" => "Visual Studio Code",
            _ => UnknownTerminal,
        };
    }

    /// <summary>A Swedish message with instructions when the microphone is blocked; null when audio may start.</summary>
    public string? MicrophoneProblem()
    {
        // Terminal names such as "iTerm" keep their case; only the generic fallback is capitalised.
        string sentenceStart = terminalName == UnknownTerminal ? "Terminalappen" : terminalName;
        return permission.Status() is MicrophonePermission.Denied or MicrophonePermission.Restricted
            ? $"{sentenceStart} har inte tillgång till mikrofonen. Öppna Systeminställningar → Integritet och säkerhet → "
                + $"Mikrofon, slå på {terminalName} och starta sedan om {terminalName}."
            : null;
    }

    public bool IsBuiltInSpeakers(string outputDeviceName)
    {
        return routes.Route(outputDeviceName)?.IsBuiltInSpeakers ?? false;
    }

    /// <summary>The speaker warning when the output is the built-in speakers; null otherwise.</summary>
    public string? SpeakerProblem(string outputDeviceName)
    {
        return IsBuiltInSpeakers(outputDeviceName) ? SpeakerWarning : null;
    }
}
