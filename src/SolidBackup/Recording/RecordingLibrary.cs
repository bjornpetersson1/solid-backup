using SolidBackup.Audio;

namespace SolidBackup.Recording;

/// <summary>A WAV file in the recordings folder: its name without <c>.wav</c>, when it was last written, and its length.</summary>
/// <param name="Info">The file's format and length, or null when it cannot be read as WAV.</param>
public sealed record RecordingEntry(string Path, string Name, DateTime Written, WavInfo? Info);

/// <summary>
/// The recordings in the recordings folder (E8-3): listed newest first, renamed and deleted. Failures come back as
/// Swedish messages, so the menu can show them and carry on.
/// </summary>
public static class RecordingLibrary
{
    public const string Extension = ".wav";

    /// <summary>Every <c>*.wav</c> in the folder, newest first; none when the folder is missing.</summary>
    public static IReadOnlyList<RecordingEntry> List(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(folder, "*" + Extension)
            .Select(path => new RecordingEntry(path, Path.GetFileNameWithoutExtension(path), File.GetLastWriteTime(path), TryReadInfo(path)))
            .OrderByDescending(entry => entry.Written)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)];
    }

    public static string MissingMessage(RecordingEntry entry)
    {
        return $"Inspelningen {entry.Name} finns inte längre.";
    }

    /// <summary>Why <paramref name="name"/> cannot be the new name of <paramref name="entry"/>, or null when it can.</summary>
    public static string? NameProblem(RecordingEntry entry, string name)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "Namnet får inte vara tomt.";
        }

        if (trimmed is "." or ".." || trimmed.IndexOfAny([.. Path.GetInvalidFileNameChars(), ':', '\\']) >= 0)
        {
            return $"Namnet {trimmed} går inte att använda som filnamn; undvik / \\ : och . eller .. ensamma.";
        }

        // Ignoring case, as macOS's file system does, so a name never differs from another only in case.
        string folder = Path.GetDirectoryName(entry.Path) ?? string.Empty;
        bool taken = Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*" + Extension)
            .Where(path => !string.Equals(path, entry.Path, StringComparison.Ordinal))
            .Any(path => string.Equals(Path.GetFileNameWithoutExtension(path), trimmed, StringComparison.OrdinalIgnoreCase));
        return taken ? $"Det finns redan en inspelning som heter {trimmed}." : null;
    }

    /// <summary>Renames the recording to <paramref name="name"/> plus <c>.wav</c>; null when done, else why not.</summary>
    public static string? Rename(RecordingEntry entry, string name)
    {
        if (!File.Exists(entry.Path))
        {
            return MissingMessage(entry);
        }

        if (NameProblem(entry, name) is { } problem)
        {
            return problem;
        }

        try
        {
            File.Move(entry.Path, Target(entry, name.Trim()));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Inspelningen {entry.Name} kunde inte byta namn ({exception.Message}).";
        }
    }

    /// <summary>Deletes the recording; null when done, else why not.</summary>
    public static string? Delete(RecordingEntry entry)
    {
        if (!File.Exists(entry.Path))
        {
            return MissingMessage(entry);
        }

        try
        {
            File.Delete(entry.Path);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Inspelningen {entry.Name} kunde inte tas bort ({exception.Message}).";
        }
    }

    private static string Target(RecordingEntry entry, string name)
    {
        return Path.Combine(Path.GetDirectoryName(entry.Path) ?? string.Empty, name + Extension);
    }

    private static WavInfo? TryReadInfo(string path)
    {
        try
        {
            return WavReader.ReadInfo(path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
