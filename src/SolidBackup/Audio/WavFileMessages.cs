namespace SolidBackup.Audio;

/// <summary>The Swedish messages for a WAV file that is missing or cannot be read, shared by every command and menu.</summary>
public static class WavFileMessages
{
    public static string Missing(string path)
    {
        return $"Filen {path} finns inte.";
    }

    /// <param name="detail">Why, when the reader said: its message.</param>
    public static string Unreadable(string path, string? detail = null)
    {
        return detail is null ? $"Filen {path} kunde inte läsas." : $"Filen {path} kunde inte läsas: {detail}";
    }
}
