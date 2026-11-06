using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>Why a command that plays audio stopped waiting.</summary>
internal enum StopReason
{
    /// <summary>The audio came to its end.</summary>
    Finished,

    /// <summary>q or Esc.</summary>
    StopKey,

    /// <summary>Ctrl+C or a cancellation.</summary>
    Interrupted,
}

/// <summary>How a command that plays audio waits to be stopped: by q, Esc, Ctrl+C or a cancellation.</summary>
internal static class StopKeys
{
    /// <summary>How often a command or screen that plays or renders looks at the keys: often enough to feel immediate.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Until disposed, Ctrl+C cancels <paramref name="cancel"/> instead of ending the process, so whatever is open is
    /// closed or cleaned up.
    /// </summary>
    public static IDisposable CancelOnCtrlC(CancellationTokenSource cancel)
    {
        ConsoleCancelEventHandler handler = (_, args) =>
        {
            args.Cancel = true;
            cancel.Cancel();
        };
        Console.CancelKeyPress += handler;
        return new Unhook(handler);
    }

    /// <summary>
    /// Waits until a stop key, Ctrl+C, <paramref name="cancellation"/> or <paramref name="finished"/>, and returns
    /// which it was. Call it once the audio runs: until then, opening can block on the permission dialog, and
    /// Ctrl+C must still be able to end the process. From here on Ctrl+C becomes a normal stop, so the audio closes.
    /// </summary>
    public static StopReason WaitForStop(IAnsiConsole console, Func<bool> finished, CancellationToken cancellation)
    {
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        using IDisposable ctrlC = CancelOnCtrlC(stop);

        // Polls instead of blocking in ReadKey, so a cancellation is noticed without a key press.
        while (!stop.IsCancellationRequested)
        {
            if (Pressed(console))
            {
                return StopReason.StopKey;
            }

            if (finished())
            {
                return StopReason.Finished;
            }

            stop.Token.WaitHandle.WaitOne(PollInterval);
        }

        return StopReason.Interrupted;
    }

    /// <summary>q (by the character typed, so it follows the keyboard layout) or Esc.</summary>
    public static bool IsStopKey(ConsoleKeyInfo key)
    {
        return key.Key == ConsoleKey.Escape || char.ToLowerInvariant(key.KeyChar) == 'q';
    }

    /// <summary>Whether q or Esc is among the keys pressed since the last look; the keys before it are read and dropped.</summary>
    public static bool Pressed(IAnsiConsole console)
    {
        while (console.Input.IsKeyAvailable())
        {
            if (console.Input.ReadKey(intercept: true) is { } key && IsStopKey(key))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class Unhook(ConsoleCancelEventHandler handler) : IDisposable
    {
        public void Dispose()
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
