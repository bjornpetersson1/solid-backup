namespace SolidBackup.Measurement;

/// <summary>Finds the repo's <c>corpus/</c> folder and the paths inside it.</summary>
public sealed class CorpusDirectory(string root)
{
    public string Root { get; } = root;

    public string ScriptPath => Path.Combine(Root, "script.csv");

    public string RecordingsPath => Path.Combine(Root, "recordings");

    /// <summary>Walks up from <paramref name="start"/> (the app's folder by default) to the folder holding <c>corpus/script.csv</c>.</summary>
    public static CorpusDirectory Locate(string? start = null)
    {
        for (DirectoryInfo? folder = new(start ?? AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "corpus");
            if (File.Exists(Path.Combine(candidate, "script.csv")))
            {
                return new CorpusDirectory(candidate);
            }
        }

        throw new DirectoryNotFoundException("Hittar inte korpusen (corpus/script.csv) i någon överliggande mapp.");
    }

    public string WavPath(string clip)
    {
        return Path.Combine(RecordingsPath, clip + ".wav");
    }

    public string ReferencePath(string clip)
    {
        return Path.Combine(RecordingsPath, clip + ".ref.csv");
    }
}
