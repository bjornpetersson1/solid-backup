using SolidBackup.Audio;

namespace SolidBackup.Recording;

/// <summary>
/// Writes a 16-bit PCM stereo WAV file as it goes. The header is written first with <see cref="Pcm16Wav.UnknownSize"/>,
/// so the file is a valid WAV even while it grows, and <see cref="Dispose"/> fills in the real sizes.
/// </summary>
public sealed class StereoWavWriter : IDisposable
{
    private const int Channels = 2;

    private readonly Stream _stream;
    private readonly int _sampleRate;
    private readonly byte[] _bytes;
    private readonly long _maxDataBytes;
    private long _dataBytes;
    private bool _disposed;

    /// <param name="stream">Where the file goes; it must be writable and seekable, and is closed with the writer.</param>
    /// <param name="maxSamples">The most interleaved samples one <see cref="Write"/> takes.</param>
    /// <param name="maxDataBytes">The size limit, <see cref="MaxDataBytes"/> unless a test needs a smaller one.</param>
    public StereoWavWriter(Stream stream, int sampleRate, int maxSamples, long maxDataBytes = MaxDataBytes)
    {
        _maxDataBytes = Math.Min(maxDataBytes, MaxDataBytes);
        _stream = stream;
        _sampleRate = sampleRate;
        _bytes = new byte[maxSamples * Pcm16Wav.BytesPerSample];
        byte[] header = new byte[Pcm16Wav.HeaderSize];
        Pcm16Wav.WriteHeader(header, sampleRate, Channels, Pcm16Wav.UnknownSize);
        _stream.Write(header);
    }

    /// <summary>The most sample bytes a WAV file's 32-bit sizes can describe, about 6 hours of 48 kHz stereo.</summary>
    public const long MaxDataBytes = uint.MaxValue - Pcm16Wav.HeaderSize;

    public long Frames => _dataBytes / (Channels * Pcm16Wav.BytesPerSample);

    /// <summary>True once the file has reached its size limit; later samples are not written.</summary>
    public bool IsFull { get; private set; }

    /// <summary>Writes interleaved left/right samples, clipped to ±1, as far as the format's size limit allows.</summary>
    public void Write(ReadOnlySpan<float> interleaved)
    {
        long room = (_maxDataBytes - _dataBytes) / (Channels * Pcm16Wav.BytesPerSample) * Channels;
        if (interleaved.Length > room)
        {
            interleaved = interleaved[..(int)room];
            IsFull = true;
        }

        Span<byte> bytes = _bytes.AsSpan(0, interleaved.Length * Pcm16Wav.BytesPerSample);
        Pcm16Wav.WritePcm16(interleaved, bytes);
        _stream.Write(bytes);
        _dataBytes += bytes.Length;
    }

    /// <summary>Fills in the sizes and closes the file; calling it again does nothing.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Span<byte> header = stackalloc byte[Pcm16Wav.HeaderSize];
            Pcm16Wav.WriteHeader(header, _sampleRate, Channels, (uint)_dataBytes);
            _stream.Seek(0, SeekOrigin.Begin);
            _stream.Write(header);
        }
        finally
        {
            _stream.Dispose();
        }
    }
}
