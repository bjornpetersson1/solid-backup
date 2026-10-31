using System.Globalization;
using System.Text;

namespace SolidBackup.Measurement;

/// <summary>
/// Reads and writes a clip's <c>.ref.csv</c>. Each line is either
/// <c>segment,&lt;start ms&gt;,&lt;end ms&gt;,&lt;kind&gt;[,&lt;cents&gt;,…]</c>, where a note has one or more pitch
/// values spaced evenly from start to end, or <c>onset,&lt;ms&gt;</c>; lines starting with <c>#</c> and blank
/// lines are ignored.
/// </summary>
public static class ReferenceFile
{
    private const string Header = "# segment,start_ms,end_ms,kind[,midi_cents,...] | onset,time_ms";

    private static readonly Dictionary<SegmentKind, string> KindNames = new()
    {
        [SegmentKind.Note] = "note",
        [SegmentKind.Silence] = "silence",
        [SegmentKind.Breath] = "breath",
        [SegmentKind.Noise] = "noise",
        [SegmentKind.OutOfRange] = "out-of-range",
        [SegmentKind.Unclear] = "unclear",
    };

    private static readonly Dictionary<string, SegmentKind> KindsByName = KindNames.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static ClipReference Read(string path)
    {
        return Parse(File.ReadAllLines(path), path);
    }

    public static ClipReference Parse(IEnumerable<string> lines, string source)
    {
        List<ReferenceSegment> segments = [];
        List<double> onsets = [];
        int lineNumber = 0;
        foreach (string rawLine in lines)
        {
            lineNumber++;
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split(',');
            switch (fields[0])
            {
                case "segment" when fields.Length >= 4:
                    segments.Add(ParseSegment(fields, segments.Count == 0 ? 0 : segments[^1].EndMs, source, lineNumber));
                    break;
                case "onset" when fields.Length == 2:
                    onsets.Add(ParseNumber(fields[1], source, lineNumber));
                    break;
                default:
                    throw Invalid(source, lineNumber, "okänd rad");
            }
        }

        return new ClipReference(segments, onsets);
    }

    public static void Write(string path, ClipReference reference)
    {
        StringBuilder text = new();
        text.Append(Header).Append('\n');
        foreach (ReferenceSegment segment in reference.Segments)
        {
            text.Append("segment,").Append(Format(segment.StartMs)).Append(',').Append(Format(segment.EndMs))
                .Append(',').Append(KindNames[segment.Kind]);
            foreach (double pitch in segment.Pitches)
            {
                text.Append(',').Append(Format(pitch));
            }

            text.Append('\n');
        }

        foreach (double onset in reference.OnsetsMs)
        {
            text.Append("onset,").Append(Format(onset)).Append('\n');
        }

        File.WriteAllText(path, text.ToString());
    }

    // Segments must follow each other without gaps or overlap from 0 ms, which SegmentAt relies on.
    private static ReferenceSegment ParseSegment(string[] fields, double expectedStart, string source, int lineNumber)
    {
        double start = ParseNumber(fields[1], source, lineNumber);
        double end = ParseNumber(fields[2], source, lineNumber);
        if (start != expectedStart || end <= start)
        {
            throw Invalid(source, lineNumber, $"segmentet {start}–{end} ms följer inte direkt på det förra (som slutar vid {expectedStart} ms)");
        }

        if (!KindsByName.TryGetValue(fields[3], out SegmentKind kind))
        {
            throw Invalid(source, lineNumber, $"okänd typ '{fields[3]}'");
        }

        double[] pitches = fields[4..].Select(field => ParseNumber(field, source, lineNumber)).ToArray();
        ReferenceSegment segment = new(start, end, kind, pitches);
        if (segment.HasPitch != (pitches.Length > 0))
        {
            throw Invalid(source, lineNumber, segment.HasPitch ? "tonhöjd saknas" : "tonhöjd för ett segment utan ton");
        }

        return segment;
    }

    private static double ParseNumber(string text, string source, int lineNumber)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw Invalid(source, lineNumber, $"'{text}' är inget tal");
    }

    private static string Format(double value)
    {
        return Math.Round(value, 1).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static InvalidDataException Invalid(string source, int lineNumber, string problem)
    {
        return new InvalidDataException($"Referensfilen {source}, rad {lineNumber}: {problem}.");
    }
}
