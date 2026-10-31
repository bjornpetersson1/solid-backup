using System.Buffers.Binary;
using System.Text;

namespace SolidBackup.Audio;

/// <summary>Reads 16-bit PCM and 32-bit IEEE float WAV files, mono or stereo.</summary>
public static class WavReader
{
    private const ushort PcmFormat = 1;
    private const ushort FloatFormat = 3;
    private const ushort ExtensibleFormat = 0xFFFE;
    private const int BasicFormatSize = 16;
    private const int ExtensibleFormatSize = 40;

    public static WavAudio Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    public static WavAudio Read(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
        try
        {
            ExpectId(reader, "RIFF");
            reader.ReadUInt32();
            ExpectId(reader, "WAVE");
            return ReadChunks(reader);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("WAV-filen tar slut innan dess ljuddata.", exception);
        }
    }

    private static WavAudio ReadChunks(BinaryReader reader)
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
                    return Decode(reader.ReadBytes(DataLength(reader.BaseStream, size)), format);
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
        bool supported = (format.FormatTag, format.BitsPerSample) is (PcmFormat, 16) or (FloatFormat, 32);
        if (!supported)
        {
            throw new InvalidDataException(
                $"WAV-formatet stöds inte (formatkod {format.FormatTag}, {format.BitsPerSample} bitar); "
                + "bara 16 bitars PCM och 32 bitars flyttal stöds.");
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

    private static WavAudio Decode(byte[] data, WavFormat format)
    {
        int bytesPerSample = format.BitsPerSample / 8;
        float[] samples = new float[data.Length / format.BlockAlign];
        for (int frame = 0; frame < samples.Length; frame++)
        {
            float sum = 0;
            for (int channel = 0; channel < format.Channels; channel++)
            {
                ReadOnlySpan<byte> bytes = data.AsSpan((frame * format.BlockAlign) + (channel * bytesPerSample), bytesPerSample);
                sum += format.FormatTag == PcmFormat
                    ? BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f
                    : BinaryPrimitives.ReadSingleLittleEndian(bytes);
            }

            samples[frame] = sum / format.Channels;
        }

        return new WavAudio(format.SampleRate, samples);
    }

    // Streaming writers leave the size as 0xFFFFFFFF; the data then runs to the end of the file.
    private static int DataLength(Stream stream, uint size)
    {
        long length = stream.CanSeek ? Math.Min(size, stream.Length - stream.Position) : size;
        return length <= Array.MaxLength
            ? (int)length
            : throw new InvalidDataException("WAV-filens ljuddata är för stor eller har okänd längd.");
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
