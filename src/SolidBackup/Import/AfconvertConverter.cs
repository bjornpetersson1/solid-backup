using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace SolidBackup.Import;

/// <summary>
/// Converts with macOS's own <c>/usr/bin/afconvert</c> (E8-4): local, no network, and the only process the app starts.
/// It writes 16-bit mono WAV at the given rate.
/// </summary>
public sealed class AfconvertConverter(string program = AfconvertConverter.Program) : IAudioConverter
{
    public const string Program = "/usr/bin/afconvert";

    // Long enough for a 20-minute clip on a slow Mac.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public void ConvertToWav(string source, string destination, int sampleRate, CancellationToken cancellation)
    {
        if (!File.Exists(program))
        {
            throw new ImportFailedException($"Programmet {program} saknas, så bara WAV-filer kan importeras.");
        }

        ProcessStartInfo start = new(program)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new[] { "-f", "WAVE", "-d", string.Create(CultureInfo.InvariantCulture, $"LEI16@{sampleRate}"), "-c", "1", source, destination })
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(start) ?? throw new ImportFailedException($"{program} kunde inte startas.");
            Task<string> error = process.StandardError.ReadToEndAsync(CancellationToken.None);
            using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            limit.CancelAfter(Timeout);
            try
            {
                process.WaitForExitAsync(limit.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Stop(process);
                cancellation.ThrowIfCancellationRequested();
                throw new ImportFailedException("Konverteringen tog för lång tid och avbröts.");
            }

            if (process.ExitCode != 0)
            {
                throw new ImportFailedException($"Filen kunde inte konverteras ({error.GetAwaiter().GetResult().Trim()}).");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            throw new ImportFailedException($"{program} kunde inte köras ({exception.Message}).", exception);
        }
    }

    // A process that has just ended cannot be killed; that is fine.
    private static void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
