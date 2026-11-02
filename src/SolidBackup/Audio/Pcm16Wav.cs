using System.Buffers.Binary;
using System.Text;

namespace SolidBackup.Audio;

/// <summary>The 16-bit PCM WAV header and sample conversion shared by the WAV writers.</summary>
public static class Pcm16Wav
{
    public const int HeaderSize = 44;
    public const int BytesPerSample = 2;

    /// <summary>The size a streaming writer puts in the header until it knows the real one: "to the end of the file".</summary>
    public const uint UnknownSize = uint.MaxValue;

    /// <summary>Writes a 16-bit PCM header for <paramref name="dataBytes"/> bytes of samples, or <see cref="UnknownSize"/>.</summary>
    public static void WriteHeader(Span<byte> header, int sampleRate, int channels, uint dataBytes)
    {
        Encoding.ASCII.GetBytes("RIFF", header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], dataBytes == UnknownSize ? UnknownSize : dataBytes + HeaderSize - 8);
        Encoding.ASCII.GetBytes("WAVEfmt ", header[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)(sampleRate * channels * BytesPerSample));
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], (ushort)(channels * BytesPerSample));
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], BytesPerSample * 8);
        Encoding.ASCII.GetBytes("data", header[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], dataBytes);
    }

    /// <summary>Writes samples as little-endian 16-bit PCM, clipped to ±1.</summary>
    public static void WritePcm16(ReadOnlySpan<float> samples, Span<byte> bytes)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)MathF.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.Slice(i * BytesPerSample, BytesPerSample), value);
        }
    }
}
