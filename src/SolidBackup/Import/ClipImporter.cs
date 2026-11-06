using SolidBackup.Audio;
using SolidBackup.Configuration;

namespace SolidBackup.Import;

/// <summary>A clip ready to run through the chain: its name (the file's, without the extension) and its audio, in mono.</summary>
public sealed record ImportedClip(string Name, WavAudio Audio);

/// <summary>
/// Loads an audio file to accompany (E8-4). A WAV at a rate the app runs at is read directly at that rate, stereo
/// averaged to mono; a WAV at another rate, and the formats macOS can read, are converted to a temporary WAV at the
/// app's rate first. Every failure is an <see cref="ImportFailedException"/> with a Swedish message.
/// </summary>
public static class ClipImporter
{
    /// <summary>The longest clip, as the whole clip and its result are held in memory.</summary>
    public const int MaxMinutes = 20;

    private const string WavExtension = ".wav";

    // The extensions converted with an IAudioConverter.
    private static readonly string[] ConvertedExtensions = [".m4a", ".aac", ".mp3", ".aif", ".aiff", ".caf"];

    /// <exception cref="ImportFailedException">The file is missing, of a kind not supported, unreadable, empty or too long.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> stopped a conversion.</exception>
    public static ImportedClip Load(string path, int sampleRate, IAudioConverter converter, CancellationToken cancellation = default)
    {
        if (!File.Exists(path))
        {
            throw new ImportFailedException(WavFileMessages.Missing(path));
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        WavAudio audio = extension switch
        {
            WavExtension when AtTheAppsRate(Info(path)) => Read(path, path),
            WavExtension => Convert(path, sampleRate, converter, cancellation),
            _ when ConvertedExtensions.Contains(extension) => Convert(path, sampleRate, converter, cancellation),
            _ => throw new ImportFailedException(
                $"Filtypen {(extension.Length == 0 ? "utan ändelse" : extension)} stöds inte; importera WAV, {string.Join(", ", ConvertedExtensions)}."),
        };
        if (audio.Samples.Length == 0)
        {
            throw new ImportFailedException($"Filen {path} innehåller inget ljud.");
        }

        CheckLength(audio.Samples.Length, audio.SampleRate);
        return new ImportedClip(Path.GetFileNameWithoutExtension(path), audio);
    }

    /// <summary>
    /// Whether loading <paramref name="path"/> converts it, which can take a while: any format but WAV, and WAV at a
    /// rate the app does not run at. A file that cannot be looked at is left to <see cref="Load"/> to report.
    /// </summary>
    public static bool Converts(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension != WavExtension)
        {
            return ConvertedExtensions.Contains(extension);
        }

        try
        {
            return !AtTheAppsRate(WavReader.ReadInfo(path));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // A WAV at a rate the app runs at is read as it is; any other is converted.
    private static bool AtTheAppsRate(WavInfo info)
    {
        return SettingsValidator.SupportedSampleRates.Contains(info.SampleRate);
    }

    private static void CheckLength(long frames, int sampleRate)
    {
        if (frames > (long)MaxMinutes * 60 * sampleRate)
        {
            throw new ImportFailedException($"Klippet är längre än {MaxMinutes} minuter; korta det först.");
        }
    }

    // A WAV file's rate and length from its header, refused when too long before any of it is decoded.
    private static WavInfo Info(string path)
    {
        WavInfo info = Guard(path, () => WavReader.ReadInfo(path));
        CheckLength(info.Frames, info.SampleRate);
        return info;
    }

    private static WavAudio Convert(string path, int sampleRate, IAudioConverter converter, CancellationToken cancellation)
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"solid-backup-import-{Guid.NewGuid():N}{WavExtension}");
        try
        {
            converter.ConvertToWav(path, temporary, sampleRate, cancellation);
            return Read(temporary, path);
        }
        finally
        {
            DeleteQuietly(temporary);
        }
    }

    // A temporary file that cannot be deleted is left to the system; it must not hide what happened.
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Reads a WAV file; errors name the file that was chosen, not a temporary one.
    private static WavAudio Read(string wav, string chosen)
    {
        return Guard(chosen, () => WavReader.Read(wav));
    }

    private static T Guard<T>(string chosen, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (InvalidDataException exception)
        {
            throw new ImportFailedException(WavFileMessages.Unreadable(chosen, exception.Message), exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ImportFailedException(WavFileMessages.Unreadable(chosen), exception);
        }
    }
}
