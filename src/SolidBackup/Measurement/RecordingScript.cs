using SolidBackup.Configuration;

namespace SolidBackup.Measurement;

/// <summary>
/// Reads <c>corpus/script.csv</c>: one line per recording, <c>&lt;clip&gt;,&lt;kind&gt;,&lt;notes&gt;</c>, where
/// kind is voice, whistle, silence or breath and notes are note names separated by spaces
/// (empty for silence and breath). Lines starting with <c>#</c> and blank lines are ignored.
/// </summary>
public static class RecordingScript
{
    public static IReadOnlyList<ScriptedClip> Read(string path)
    {
        List<ScriptedClip> clips = [];
        int lineNumber = 0;
        foreach (string rawLine in File.ReadLines(path))
        {
            lineNumber++;
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split(',');
            if (fields.Length != 3 || !Enum.TryParse(fields[1], ignoreCase: true, out RecordingKind kind))
            {
                throw Invalid(path, lineNumber, "väntade <klipp>,<voice|whistle|silence|breath>,<toner>");
            }

            int[] notes = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => NoteName.TryParseMidiNumber(name, out int midi) ? midi : throw Invalid(path, lineNumber, $"okänd ton '{name}'"))
                .ToArray();
            ScriptedClip clip = new(fields[0], kind, notes);
            if (clip.IsPitched == (notes.Length == 0))
            {
                throw Invalid(path, lineNumber, clip.IsPitched ? "toner saknas" : "toner för ett klipp utan ton");
            }

            clips.Add(clip);
        }

        return clips;
    }

    private static InvalidDataException Invalid(string path, int lineNumber, string problem)
    {
        return new InvalidDataException($"Tonlistan {path}, rad {lineNumber}: {problem}.");
    }
}
