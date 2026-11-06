using System.Buffers.Binary;
using System.Text;

namespace SolidBackup.Tests.Audio;

/// <summary>Builds WAV files in memory for the tests.</summary>
internal static class TestWav
{
    public const ushort Pcm = 1;
    public const ushort Float = 3;

    public static byte[] Pcm16(int sampleRate, int channels, params float[] interleaved)
    {
        byte[] data = new byte[interleaved.Length * 2];
        for (int i = 0; i < interleaved.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 2), (short)Math.Round(interleaved[i] * 32767));
        }

        return Build(Pcm, sampleRate, channels, 16, data);
    }

    public static byte[] Float32(int sampleRate, int channels, params float[] interleaved)
    {
        byte[] data = new byte[interleaved.Length * 4];
        for (int i = 0; i < interleaved.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 4), interleaved[i]);
        }

        return Build(Float, sampleRate, channels, 32, data);
    }

    /// <summary>24-bit PCM, little-endian, from samples in ±1.</summary>
    public static byte[] Pcm24(int sampleRate, int channels, params float[] interleaved)
    {
        return Build(Pcm, sampleRate, channels, 24, [.. interleaved.SelectMany(sample => BitConverter.GetBytes((int)(sample * 8388607)).Take(3))]);
    }

    /// <summary>32-bit PCM, little-endian, from samples in ±1.</summary>
    public static byte[] Pcm32(int sampleRate, int channels, params float[] interleaved)
    {
        return Build(Pcm, sampleRate, channels, 32, [.. interleaved.SelectMany(sample => BitConverter.GetBytes((int)(sample * 2147483647.0)))]);
    }

    public static byte[] Build(ushort formatTag, int sampleRate, int channels, int bitsPerSample, byte[] data, bool withListChunk = false)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.ASCII);
        int blockAlign = channels * bitsPerSample / 8;
        writer.Write("RIFF"u8);
        writer.Write(0u);
        writer.Write("WAVE"u8);
        if (withListChunk)
        {
            writer.Write("LIST"u8);
            writer.Write(3u);
            writer.Write(new byte[4]);
        }

        writer.Write("fmt "u8);
        writer.Write(16u);
        writer.Write(formatTag);
        writer.Write((ushort)channels);
        writer.Write((uint)sampleRate);
        writer.Write((uint)(sampleRate * blockAlign));
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)bitsPerSample);
        writer.Write("data"u8);
        writer.Write((uint)data.Length);
        writer.Write(data);
        writer.Flush();
        return stream.ToArray();
    }

    public static string WriteTemp(byte[] wav)
    {
        string folder = Path.Combine(Path.GetTempPath(), "solid-backup-tests");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, wav);
        return path;
    }

    /// <summary>One channel of a stereo 16-bit PCM WAV file with a 44-byte header, as floats.</summary>
    public static float[] Pcm16Channel(byte[] file, int channel)
    {
        int frames = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)) / 4;
        return [.. Enumerable.Range(0, frames).Select(frame => Pcm16Sample(file, frame, channel, channels: 2))];
    }

    /// <summary>One sample of a 16-bit PCM WAV file with a 44-byte header, as a float.</summary>
    public static float Pcm16Sample(byte[] file, int frame, int channel, int channels)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(file.AsSpan(44 + (((frame * channels) + channel) * 2))) / 32767f;
    }
}
