using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Recording;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// The menu's Inspelningar (E8-3): the recordings in <c>RecordingFolder</c>, newest first, each of which can be heard
/// (both channels mixed, the microphone or the chords), renamed or deleted. Every failure is a Swedish message, after
/// which the list is read again.
/// </summary>
/// <param name="pollInterval">How often the player looks at the keys; 50 ms when not given.</param>
public sealed class RecordingsMenu(IAnsiConsole console, Func<AppSettings, IAudioPlayback> openPlayback, TimeSpan? pollInterval = null)
{
    public const string ListenChoice = "Lyssna";
    public const string BackChoice = "Tillbaka";
    public const string MicrophoneOnly = "Bara mikrofonen";
    public const string UnreadableLength = "oläsbar";


    private const string RenameChoice = "Byt namn";
    private const string DeleteChoice = "Ta bort";
    private const string BothChannels = "Båda";
    private const string ChordsOnly = "Bara ackorden";

    // The list's last row, which leads back to the main menu.
    private static readonly RecordingEntry Back = new(string.Empty, BackChoice, default, null);

    private static readonly IReadOnlyList<string> Actions = [ListenChoice, RenameChoice, DeleteChoice, BackChoice];

    // A recording has the microphone on the left and the chords on the right; both mixed comes first, as the default.
    private static readonly (string Text, WavChannel Channel)[] Channels =
        [(BothChannels, WavChannel.Mixed), (MicrophoneOnly, WavChannel.Left), (ChordsOnly, WavChannel.Right)];

    public static string EmptyMessage(string folder)
    {
        return $"Inga inspelningar i {folder}.";
    }

    /// <summary>A recording's row in the list: its name, when it was written and how long it is.</summary>
    public static string Row(RecordingEntry entry)
    {
        string length = entry.Info is { } info ? SettingTexts.Duration(info.Frames, info.SampleRate) : UnreadableLength;
        return $"{entry.Name}  {entry.Written:yyyy-MM-dd HH:mm}  {length}";
    }

    /// <summary>Shows the recordings until Tillbaka, or until there are none.</summary>
    public void Run(AppSettings settings)
    {
        while (true)
        {
            if (List(settings.RecordingFolder) is not { } entries)
            {
                return;
            }

            if (entries.Count == 0)
            {
                console.WriteLine(EmptyMessage(settings.RecordingFolder));
                return;
            }

            RecordingEntry entry = console.Prompt(new SelectionPrompt<RecordingEntry>()
                .Title("Vilken inspelning?")
                .AddChoices([.. entries, Back])
                .UseConverter(choice => ReferenceEquals(choice, Back) ? BackChoice : Markup.Escape(Row(choice))));
            if (ReferenceEquals(entry, Back))
            {
                return;
            }

            Choose(entry, settings);
        }
    }

    // The recording's choices; after listening they come back, so another channel can be heard.
    private void Choose(RecordingEntry entry, AppSettings settings)
    {
        while (true)
        {
            string action = console.Prompt(new SelectionPrompt<string>().Title($"Vad vill du göra med {Markup.Escape(entry.Name)}?").AddChoices(Actions));
            switch (action)
            {
                case ListenChoice:
                    Listen(entry, settings);
                    break;
                case RenameChoice:
                    Rename(entry);
                    return;
                case DeleteChoice:
                    Delete(entry);
                    return;
                default:
                    return;
            }
        }
    }

    // The recordings, or null after a Swedish error when the folder cannot be read.
    private IReadOnlyList<RecordingEntry>? List(string folder)
    {
        try
        {
            return RecordingLibrary.List(folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConsoleMessages.Error(console, $"Inspelningsmappen {folder} kunde inte läsas.");
            return null;
        }
    }

    /// <summary>Plays the recording, asking which channels for a stereo file; a missing or unreadable file is a Swedish error.</summary>
    public void Listen(RecordingEntry entry, AppSettings settings)
    {
        if (!File.Exists(entry.Path))
        {
            ConsoleMessages.Error(console, RecordingLibrary.MissingMessage(entry));
            return;
        }

        WavChannel channel = entry.Info is { Channels: 2 }
            ? console.Prompt(new SelectionPrompt<(string Text, WavChannel Channel)>().Title("Vad vill du höra?").AddChoices(Channels).UseConverter(choice => choice.Text)).Channel
            : WavChannel.Mixed;
        if (RecordingFiles.Read(console, entry.Path, channel) is not { } audio)
        {
            return;
        }

        try
        {
            using IAudioPlayback playback = openPlayback(settings with { SampleRate = audio.SampleRate });
            new PlaybackScreen(console, pollInterval).Play(playback, audio.Samples, audio.SampleRate);
        }
        catch (AudioDeviceException exception)
        {
            ConsoleMessages.AudioError(console, exception);
        }
    }

    // Asks until the name can be used; Esc, an empty answer or the same name cancels.
    private void Rename(RecordingEntry entry)
    {
        console.MarkupLine($"Nu heter den {Markup.Escape(entry.Name)}.");
        string? name = LineInput.Ask(console, "Nytt namn (utan .wav, Esc avbryter): ");
        while (!string.IsNullOrWhiteSpace(name) && name.Trim() != entry.Name)
        {
            if (RecordingLibrary.NameProblem(entry, name) is not { } problem)
            {
                if (RecordingLibrary.Rename(entry, name) is { } failure)
                {
                    ConsoleMessages.Error(console, failure);
                }

                return;
            }

            ConsoleMessages.Error(console, problem);
            name = LineInput.Ask(console, "Nytt namn (utan .wav, tomt eller Esc avbryter): ");
        }
    }

    private void Delete(RecordingEntry entry)
    {
        ConfirmationPrompt confirm = new($"Ta bort {Markup.Escape(entry.Name)}?") { Yes = 'j', No = 'n', DefaultValue = false };
        if (console.Prompt(confirm) && RecordingLibrary.Delete(entry) is { } failure)
        {
            ConsoleMessages.Error(console, failure);
        }
    }
}
