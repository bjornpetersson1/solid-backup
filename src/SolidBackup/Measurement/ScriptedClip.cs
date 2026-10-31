namespace SolidBackup.Measurement;

/// <summary>One recording in the script: its name, what it holds, and the notes in order (MIDI numbers).</summary>
public sealed record ScriptedClip(string Name, RecordingKind Kind, IReadOnlyList<int> Notes)
{
    public bool IsPitched => Kind is RecordingKind.Voice or RecordingKind.Whistle;
}
