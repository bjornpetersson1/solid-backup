using System.Globalization;

using SolidBackup.Audio;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// Plays rendered audio with a line that shows where it is (E8-3): space pauses and resumes, ← and → seek
/// <see cref="SeekSeconds"/> back and forward, q or Esc stops; it also ends by itself at the end.
/// </summary>
/// <param name="pollInterval">How often it looks at the keys and redraws the line; 50 ms when not given.</param>
public sealed class PlaybackScreen(IAnsiConsole console, TimeSpan? pollInterval = null)
{
    public const int SeekSeconds = 5;

    private readonly TimeSpan _pollInterval = pollInterval ?? StopKeys.PollInterval;

    public static string KeysText { get; } = $"mellanslag: paus · ←/→: {SeekSeconds} s · q/Esc: stopp";

    /// <summary>Plays <paramref name="samples"/> at <paramref name="sampleRate"/> until they end or q/Esc.</summary>
    public void Play(IAudioPlayback playback, float[] samples, int sampleRate)
    {
        playback.Start(samples);
        try
        {
            console.Live(new Text(Line(playback, samples.Length, sampleRate))).Start(context =>
            {
                while (HandleKeys(playback, sampleRate) && (playback.Paused || !playback.IsFinished))
                {
                    context.UpdateTarget(new Text(Line(playback, samples.Length, sampleRate)));
                    Thread.Sleep(_pollInterval);
                }

                context.UpdateTarget(new Text(Line(playback, samples.Length, sampleRate)));
            });
        }
        finally
        {
            playback.Stop();
        }
    }

    // The position line, e.g. "0:12 / 1:30 · mellanslag: paus · ←/→: 5 s · q/Esc: stopp".
    private static string Line(IAudioPlayback playback, int length, int sampleRate)
    {
        string state = playback.Paused ? " (pausad)" : string.Empty;
        return $"{SettingTexts.Duration(playback.Position, sampleRate)} / {SettingTexts.Duration(length, sampleRate)}{state} · {KeysText}";
    }

    // Handles the keys pressed since the last look; false once q or Esc was pressed.
    private bool HandleKeys(IAudioPlayback playback, int sampleRate)
    {
        while (console.Input.IsKeyAvailable())
        {
            if (console.Input.ReadKey(intercept: true) is not { } key)
            {
                continue;
            }

            if (StopKeys.IsStopKey(key))
            {
                return false;
            }

            switch (key.Key)
            {
                case ConsoleKey.Spacebar:
                    playback.Paused = !playback.Paused;
                    break;
                case ConsoleKey.LeftArrow:
                    playback.Seek(playback.Position - (SeekSeconds * sampleRate));
                    break;
                case ConsoleKey.RightArrow:
                    playback.Seek(playback.Position + (SeekSeconds * sampleRate));
                    break;
            }
        }

        return true;
    }
}
