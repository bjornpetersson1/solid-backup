using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>The voiced, shaped tones of the chord a pattern plays, voiced again only when the chord or its shape changes.</summary>
internal sealed class ChordTones
{
    private readonly ChordVoicer _voicer = new();
    private readonly int[] _notes = new int[ChordVoicing.MaxShapedTones];
    private int _count;
    private Chord _chord;
    private ChordShape _shape;

    public ReadOnlySpan<int> For(Chord chord, ChordShape shape)
    {
        if (_count == 0 || chord != _chord || shape != _shape)
        {
            _count = _voicer.Next(chord, shape, _notes);
            _chord = chord;
            _shape = shape;
        }

        return _notes.AsSpan(0, _count);
    }

    /// <summary>After silence the next chord is in close voicing again.</summary>
    public void Forget()
    {
        _count = 0;
        _voicer.Forget();
    }
}
