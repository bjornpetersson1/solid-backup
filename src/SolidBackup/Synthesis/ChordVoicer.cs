using SolidBackup.Harmony;

namespace SolidBackup.Synthesis;

/// <summary>
/// Voices chords one after the other: each chord is led from the one before (<see cref="ChordVoicing.Lead"/>),
/// then shaped (<see cref="ChordVoicing.Shape"/>). The voice leading always follows the whole chord, so a change
/// of shape never moves a tone. Allocates nothing after construction.
/// </summary>
public sealed class ChordVoicer
{
    private readonly int[] _previous = new int[ChordVoicing.MaxTones];
    private readonly int[] _voiced = new int[ChordVoicing.MaxTones];
    private int _previousCount;

    /// <summary>Voices <paramref name="chord"/> from the previous one, writes the tones of <paramref name="shape"/>, lowest first, and returns how many there are.</summary>
    public int Next(Chord chord, ChordShape shape, Span<int> notes)
    {
        int count = ChordVoicing.Lead(chord, _previous.AsSpan(0, _previousCount), _voiced);
        _voiced.AsSpan(0, count).CopyTo(_previous);
        _previousCount = count;
        return ChordVoicing.Shape(chord, _voiced.AsSpan(0, count), shape, notes);
    }

    /// <summary>Forgets the previous chord, so the next one is in close voicing.</summary>
    public void Forget()
    {
        _previousCount = 0;
    }
}
