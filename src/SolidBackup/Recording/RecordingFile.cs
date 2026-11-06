namespace SolidBackup.Recording;

/// <summary>Where a new recording goes: a file named after the local start time in the recording folder, which is created if it is missing.</summary>
public static class RecordingFile
{
    /// <summary>Creates the folder and a new, empty file; <c>_2</c>, <c>_3</c> … is added when the name is taken.</summary>
    /// <exception cref="IOException">The folder or the file cannot be created.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder cannot be written.</exception>
    /// <param name="prefix">Put before the time, e.g. an imported clip's name: <c>&lt;prefix&gt;_&lt;time&gt;.wav</c>.</param>
    public static (string Path, FileStream Stream) Create(string folder, DateTime localStart, string? prefix = null)
    {
        Directory.CreateDirectory(folder);
        string time = localStart.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
        string name = prefix is null ? time : $"{prefix}_{time}";
        for (int attempt = 1; ; attempt++)
        {
            string path = Path.Combine(folder, attempt == 1 ? $"{name}.wav" : $"{name}_{attempt}.wav");
            try
            {
                return (path, new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite));
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
    }
}
