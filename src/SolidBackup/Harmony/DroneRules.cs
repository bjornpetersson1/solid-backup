namespace SolidBackup.Harmony;

/// <summary>Bordun: the tonic's root and fifth, whatever the note.</summary>
public sealed class DroneRules : IChordRules
{
    public Chord Choose(int pitchClass, Key key, Chord? current)
    {
        return new Chord(key.Tonic, ChordQuality.Power);
    }
}
