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
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Waits until a stop key, Ctrl+C, <paramref name="cancellation"/> or <paramref name="finished"/>, and returns
    /// which it was. Call it once the audio runs: until then, opening can block on the permission dialog, and
    /// Ctrl+C must still be able to end the process. From here on Ctrl+C becomes a normal stop, so the audio closes.
    /// </summary>
    public static StopReason WaitForStop(IAnsiConsole console, Func<bool> finished, CancellationToken cancellation)
    {
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        ConsoleCancelEventHandler stopOnCtrlC = (_, args) =>
        {
            args.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += stopOnCtrlC;
        try
        {
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
        finally
        {
            Console.CancelKeyPress -= stopOnCtrlC;
        }
    }

    private static bool Pressed(IAnsiConsole console)
    {
        while (console.Input.IsKeyAvailable())
        {
            if (console.Input.ReadKey(intercept: true) is { } key && (key.Key == ConsoleKey.Escape || char.ToLowerInvariant(key.KeyChar) == 'q'))
            {
                return true;
            }
        }

        return false;
    }
}
