namespace SolidBackup.Audio.Devices;

/// <summary>The checks before audio starts: microphone permission and the speaker warning.</summary>
public sealed class AudioPreflight(IMicrophonePermissionSource permission, IOutputRouteSource routes, string terminalName)
{
    public const string UnknownTerminal = "terminalappen";

    // E2-1: on first use the open blocks until the dialog is answered, and may then fail once.
    public const string FirstUseNotice = "macOS frågar efter mikrofonåtkomst – svara i dialogen. Om ljudet inte startar, kör igen.";

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

    /// <summary>
    /// Blocked, with instructions, when access is denied or restricted; a notice when macOS has
    /// not asked yet; clear when access is granted or the status cannot be read.
    /// </summary>
    public MicrophoneCheck CheckMicrophone()
    {
        return permission.Status() switch
        {
            MicrophonePermission.Denied or MicrophonePermission.Restricted => new(true, BlockedMessage()),
            MicrophonePermission.NotDetermined => new(false, FirstUseNotice),
            _ => MicrophoneCheck.Clear,
        };
    }

    private string BlockedMessage()
    {
        // Terminal names such as "iTerm" keep their case; only the generic fallback is capitalised.
        string sentenceStart = terminalName == UnknownTerminal ? "Terminalappen" : terminalName;
        return $"{sentenceStart} har inte tillgång till mikrofonen. Öppna Systeminställningar → Integritet och säkerhet → "
            + $"Mikrofon, slå på {terminalName} och starta sedan om {terminalName}.";
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
