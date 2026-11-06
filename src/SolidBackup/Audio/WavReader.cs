using System.Buffers.Binary;
using System.Text;

namespace SolidBackup.Audio;

/// <summary>Which channel of a stereo WAV file to read; a mono file has only the one.</summary>
public enum WavChannel
{
    /// <summary>Both channels averaged.</summary>
    Mixed,
    Left,
    Right,
}

/// <summary>A WAV file's format and length, read from its header without decoding the audio.</summary>
public sealed record WavInfo(int SampleRate, int Channels, long Frames);

/// <summary>Reads 16-, 24- and 32-bit PCM and 32-bit IEEE float WAV files, mono or stereo.</summary>
public static class WavReader
{
    private const ushort PcmFormat = 1;
    private const ushort FloatFormat = 3;
    private const ushort ExtensibleFormat = 0xFFFE;
    private const int BasicFormatSize = 16;
    private const int ExtensibleFormatSize = 40;

    public static WavAudio Read(string path, WavChannel channel = WavChannel.Mixed)
    {
        using FileStream stream = File.OpenRead(path);
        return Read(stream, channel);
    }

    public static WavAudio Read(Stream stream, WavChannel channel = WavChannel.Mixed)
    {
        return ReadHeader(stream, (reader, format, size) => Decode(reader.ReadBytes(DataLength(reader.BaseStream, size)), format, channel));
    }

    /// <summary>The file's format and length, without reading its audio.</summary>
    public static WavInfo ReadInfo(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return ReadHeader(stream, (reader, format, size) => new WavInfo(format.SampleRate, format.Channels, DataBytes(reader.BaseStream, size) / format.BlockAlign));
    }

    // Reads up to the data chunk and hands it over with its format.
    private static T ReadHeader<T>(Stream stream, Func<BinaryReader, WavFormat, uint, T> data)
    {
        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
        try
        {
            ExpectId(reader, "RIFF");
            reader.ReadUInt32();
            ExpectId(reader, "WAVE");
            return ReadChunks(reader, data);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("WAV-filen tar slut innan dess ljuddata.", exception);
        }
    }

    private static T ReadChunks<T>(BinaryReader reader, Func<BinaryReader, WavFormat, uint, T> data)
    {
        WavFormat? format = null;
        while (true)
        {
            string id = ReadId(reader);
            uint size = reader.ReadUInt32();
            switch (id)
            {
                case "fmt ":
                    format = ReadFormat(reader, size);
                    break;
                case "data" when format is null:
                    throw new InvalidDataException("WAV-filen har ljuddata före formatbeskrivningen.");
                case "data":
                    return data(reader, format, size);
                default:
                    Skip(reader, size + (size & 1));
                    break;
            }
        }
    }

    private static WavFormat ReadFormat(BinaryReader reader, uint size)
    {
        if (size < BasicFormatSize)
        {
            throw new InvalidDataException("WAV-filens formatbeskrivning är för kort.");
        }

        ushort formatTag = reader.ReadUInt16();
        ushort channels = reader.ReadUInt16();
        uint sampleRate = reader.ReadUInt32();
        reader.ReadUInt32();
        ushort blockAlign = reader.ReadUInt16();
        ushort bitsPerSample = reader.ReadUInt16();
        uint remaining = size - BasicFormatSize;
        if (formatTag == ExtensibleFormat && size >= ExtensibleFormatSize)
        {
            // cbSize, valid bits and channel mask come first; the sub-format GUID starts with the format tag.
            reader.ReadBytes(8);
            formatTag = reader.ReadUInt16();
            remaining -= 10;
        }

        Skip(reader, remaining + (size & 1));
        WavFormat format = new(formatTag, channels, (int)sampleRate, blockAlign, bitsPerSample);
        Validate(format);
        return format;
    }

    private static void Validate(WavFormat format)
    {
        bool supported = (format.FormatTag, format.BitsPerSample) is (PcmFormat, 16 or 24 or 32) or (FloatFormat, 32);
        if (!supported)
        {
            throw new InvalidDataException(
                $"WAV-formatet stöds inte (formatkod {format.FormatTag}, {format.BitsPerSample} bitar); "
                + "bara 16, 24 och 32 bitars PCM och 32 bitars flyttal stöds.");
        }

        if (format.Channels is not (1 or 2))
        {
            throw new InvalidDataException($"WAV-filen har {format.Channels} kanaler; bara mono och stereo stöds.");
        }

        if (format.BlockAlign != format.Channels * format.BitsPerSample / 8 || format.SampleRate <= 0)
        {
            throw new InvalidDataException("WAV-filens formatbeskrivning är inkonsekvent.");
        }
    }

    private static WavAudio Decode(byte[] data, WavFormat format, WavChannel wanted)
    {
        int bytesPerSample = format.BitsPerSample / 8;
        float[] samples = new float[data.Length / format.BlockAlign];
        (int first, int count) = format.Channels == 1 || wanted == WavChannel.Mixed ? (0, format.Channels) : (wanted == WavChannel.Left ? 0 : 1, 1);
        for (int frame = 0; frame < samples.Length; frame++)
        {
            float sum = 0;
            for (int channel = first; channel < first + count; channel++)
            {
                ReadOnlySpan<byte> bytes = data.AsSpan((frame * format.BlockAlign) + (channel * bytesPerSample), bytesPerSample);
                sum += Sample(bytes, format);
            }

            samples[frame] = sum / count;
        }

        return new WavAudio(format.SampleRate, samples);
    }

    private static float Sample(ReadOnlySpan<byte> bytes, WavFormat format)
    {
        return (format.FormatTag, format.BitsPerSample) switch
        {
            (FloatFormat, _) => BinaryPrimitives.ReadSingleLittleEndian(bytes),
            (_, 16) => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f,

            // Three bytes, little-endian: shifted into the top of an int, so the sign comes along.
            (_, 24) => ((bytes[0] << 8) | (bytes[1] << 16) | (bytes[2] << 24)) / 2147483648f,
            _ => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648f,
        };
    }

    // Streaming writers leave the size as 0xFFFFFFFF; the data then runs to the end of the file.
    private static int DataLength(Stream stream, uint size)
    {
        long length = DataBytes(stream, size);
        return length <= Array.MaxLength
            ? (int)length
            : throw new InvalidDataException("WAV-filens ljuddata är för stor eller har okänd längd.");
    }

    private static long DataBytes(Stream stream, uint size)
    {
        return stream.CanSeek ? Math.Min(size, stream.Length - stream.Position) : size;
    }

    private static void Skip(BinaryReader reader, long count)
    {
        if (reader.BaseStream.CanSeek)
        {
            reader.BaseStream.Seek(count, SeekOrigin.Current);
            return;
        }

        if (reader.ReadBytes((int)Math.Min(count, Array.MaxLength)).Length < count)
        {
            throw new EndOfStreamException();
        }
    }

    private static void ExpectId(BinaryReader reader, string expected)
    {
        if (ReadId(reader) != expected)
        {
            throw new InvalidDataException($"Filen är ingen WAV-fil ({expected} saknas).");
        }
    }

    private static string ReadId(BinaryReader reader)
    {
        byte[] id = reader.ReadBytes(4);
        return id.Length == 4 ? Encoding.ASCII.GetString(id) : throw new EndOfStreamException();
    }

    private sealed record WavFormat(ushort FormatTag, int Channels, int SampleRate, int BlockAlign, int BitsPerSample);
}
