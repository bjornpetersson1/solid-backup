using System.Globalization;

using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Import;
using SolidBackup.Recording;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// The menu's Importera klipp (E8-4): a clip's path is typed or dragged in, the run's settings are chosen starting from
/// the saved ones (and never saved), and the clip goes through the same chain as live mode. The result is saved as a
/// recording, clip left and chords right, and can be heard at once with the recordings' player.
/// </summary>
/// <param name="now">The local time a result is named after.</param>
/// <param name="pollInterval">How often rendering looks at Esc and redraws its progress; 50 ms when not given.</param>
public sealed class ImportMenu(
    IAnsiConsole console, IAudioConverter converter, RecordingsMenu recordings, Func<DateTime> now, TimeSpan? pollInterval = null)
{
    public const string RunChoice = "Kör";
    public const string CancelChoice = "Avbryt";
    public const string CancelledMessage = "Avbrutet; inget sparades.";

    private readonly SettingPrompts _prompts = new(console);
    private readonly TimeSpan _pollInterval = pollInterval ?? StopKeys.PollInterval;

    /// <summary>The run menu's items, in the order shown.</summary>
    public static IReadOnlyList<string> RunChoices { get; } = [RunChoice, .. SettingPrompts.MusicChoices, CancelChoice];

    public static string SavedMessage(string path)
    {
        return $"Sparade {path}.";
    }

    public const string LoadingText = "Läser in klippet … (Esc avbryter)";

    public static string ProgressText(double done)
    {
        return string.Create(CultureInfo.InvariantCulture, $"Spelar in ackorden till klippet … {done:0%} (Esc avbryter)");
    }

    /// <summary>
    /// Imports one clip, starting from <paramref name="saved"/>, until it is saved and heard, or cancelled. From the path
    /// on, Ctrl+C cancels the import (the main menu's quit is off meanwhile), so a conversion or a half-written result
    /// is always cleaned up.
    /// </summary>
    public void Run(AppSettings saved)
    {
        string? typed = LineInput.Ask(console, "Sökväg till klippet (dra in filen hit, Esc avbryter): ");
        if (string.IsNullOrWhiteSpace(typed))
        {
            return;
        }

        using CancellationTokenSource interrupt = new();
        using IDisposable ctrlC = StopKeys.CancelOnCtrlC(interrupt);
        Import(ClipPath.Parse(typed), saved, interrupt.Token);
    }

    private void Import(string path, AppSettings saved, CancellationToken interrupt)
    {
        ImportedClip? clip;
        try
        {
            // Only a conversion takes long enough to need a progress line and Esc; a WAV at the app's rate is read at once.
            clip = ClipImporter.Converts(path)
                ? Work(_ => LoadingText, (_, cancellation) => ClipImporter.Load(path, saved.SampleRate, converter, cancellation), interrupt)
                : ClipImporter.Load(path, saved.SampleRate, converter);
        }
        catch (ImportFailedException exception)
        {
            ConsoleMessages.Error(console, exception.Message);
            return;
        }

        if (clip is null || ChooseSettings(clip, saved with { SampleRate = clip.Audio.SampleRate }) is not { } settings || interrupt.IsCancellationRequested)
        {
            console.WriteLine(CancelledMessage);
            return;
        }

        float[]? chords;
        try
        {
            chords = Work(ProgressText, (progress, cancellation) => AccompanyCommand.Accompany(clip.Audio.Samples, settings, noVoice: true, progress, cancellation), interrupt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A worker's failure (e.g. out of memory on a long clip) ends this import, not the app.
            ConsoleMessages.Error(console, $"Klippet kunde inte köras ({exception.Message}).");
            return;
        }

        if (chords is null || interrupt.IsCancellationRequested)
        {
            console.WriteLine(CancelledMessage);
            return;
        }

        if (Save(clip, chords, saved.RecordingFolder) is { } result)
        {
            OfferListening(result, saved);
        }
    }

    // The run menu: the run's settings and the music items; null for Avbryt.
    private AppSettings? ChooseSettings(ImportedClip clip, AppSettings settings)
    {
        while (true)
        {
            Table table = new Table().AddColumn("Inställning").AddColumn("Val").Title(Markup.Escape($"{clip.Name} ({SettingTexts.Duration(clip.Audio.Samples.Length, clip.Audio.SampleRate)})"));
            SettingPrompts.AddMusicRows(table, settings);
            console.Write(table);
            string choice = console.Prompt(new SelectionPrompt<string>().Title("Ändra för den här körningen, eller kör:").AddChoices(RunChoices));
            switch (choice)
            {
                case CancelChoice:
                    return null;
                case RunChoice when HarmonySetup.Prepare(console, settings, keyGiven: false, modeGiven: false) is not null:
                    return settings;
                case RunChoice:
                    break;
                default:
                    settings = _prompts.ChangeMusic(choice, settings);
                    break;
            }
        }
    }

    // Runs work on a worker thread with a line that shows how far it has come; null once Esc, q or Ctrl+C cancels it.
    private T? Work<T>(Func<double, string> text, Func<Action<double>, CancellationToken, T?> work, CancellationToken interrupt)
        where T : class
    {
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(interrupt);

        // A stop pressed before the work starts counts too, so it is never lost to quick work.
        if (StopKeys.Pressed(console))
        {
            cancel.Cancel();
        }

        double done = 0;
        Task<T?> task = Task.Run(() => work(progress => Volatile.Write(ref done, progress), cancel.Token));
        console.Live(new Text(text(0))).Start(context =>
        {
            while (!task.IsCompleted)
            {
                if (StopKeys.Pressed(console))
                {
                    cancel.Cancel();
                }

                context.UpdateTarget(new Text(text(Volatile.Read(ref done))));
                Thread.Sleep(_pollInterval);
            }

            if (task.IsCompletedSuccessfully && task.Result is not null)
            {
                context.UpdateTarget(new Text(text(1)));
            }
        });

        try
        {
            return task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private string? Save(ImportedClip clip, float[] chords, string folder)
    {
        try
        {
            string path = ClipRecording.Save(folder, now(), clip, chords);
            console.WriteLine(SavedMessage(path));
            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConsoleMessages.Error(console, RecordingSession.RecordingFolderMessage(folder));
            return null;
        }
    }

    private void OfferListening(string path, AppSettings settings)
    {
        RecordingEntry entry = RecordingLibrary.Entry(path);
        while (console.Prompt(new SelectionPrompt<string>().Title("Vill du lyssna?").AddChoices(RecordingsMenu.ListenChoice, RecordingsMenu.BackChoice)) == RecordingsMenu.ListenChoice)
        {
            recordings.Listen(entry, settings);
        }
    }
}
