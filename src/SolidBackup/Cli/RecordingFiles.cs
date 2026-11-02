using SolidBackup.Audio;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>Reads and writes the commands' WAV files, showing a Swedish error instead of throwing.</summary>
internal static class RecordingFiles
{
    /// <summary>The recording, or null after an error has been shown.</summary>
    public static WavAudio? Read(IAnsiConsole console, string path)
    {
        if (!File.Exists(path))
        {
            ConsoleMessages.Error(console, HarmonizeCommand.MissingFileMessage(path));
            return null;
        }

        try
        {
            return WavReader.Read(path);
        }
        catch (InvalidDataException exception)
        {
            ConsoleMessages.Error(console, $"Filen {path} kunde inte läsas: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConsoleMessages.Error(console, $"Filen {path} kunde inte läsas.");
        }

        return null;
    }

    /// <summary>Writes a 16-bit WAV file; false after an error has been shown.</summary>
    public static bool Write(IAnsiConsole console, string path, int sampleRate, ReadOnlySpan<float> samples)
    {
        try
        {
            WavWriter.WritePcm16(path, sampleRate, samples);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ConsoleMessages.Error(console, $"Filen {path} kunde inte skrivas.");
            return false;
        }
    }
}
