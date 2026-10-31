using System.Buffers.Binary;
using System.Text;

namespace SolidBackup.Audio;

/// <summary>Writes mono audio as a 16-bit PCM WAV file, clipping samples to ±1.</summary>
public static class WavWriter
{
    private const int HeaderSize = 44;

    public static void WritePcm16(string path, int sampleRate, ReadOnlySpan<float> samples)
    {
        byte[] file = new byte[HeaderSize + (samples.Length * 2)];
        Span<byte> header = file.AsSpan(0, HeaderSize);
        Encoding.ASCII.GetBytes("RIFF", header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(file.Length - 8));
        Encoding.ASCII.GetBytes("WAVEfmt ", header[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)(sampleRate * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], 16);
        Encoding.ASCII.GetBytes("data", header[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], (uint)(samples.Length * 2));
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * 32767);
            BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(HeaderSize + (i * 2)), value);
        }

        File.WriteAllBytes(path, file);
    }
}
