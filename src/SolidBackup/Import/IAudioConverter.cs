namespace SolidBackup.Import;

/// <summary>Turns an audio file that is not WAV into a mono WAV file (E8-4).</summary>
public interface IAudioConverter
{
    /// <summary>Converts <paramref name="source"/> to a mono WAV file at <paramref name="destination"/>.</summary>
    /// <exception cref="ImportFailedException">The conversion failed, with a Swedish message.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> stopped it.</exception>
    void ConvertToWav(string source, string destination, int sampleRate, CancellationToken cancellation);
}
