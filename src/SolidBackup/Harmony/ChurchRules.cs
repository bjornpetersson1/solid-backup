namespace SolidBackup.Harmony;

/// <summary>Kyrko: the diatonic triads of the chosen mode, strictly, without the diminished one.</summary>
public sealed class ChurchRules() : ScaleRules(DefaultPriority, TriadOn)
{
    private static Chord? TriadOn(Key key, int degree)
    {
        Chord triad = key.TriadOn(degree);

        // Locrian's tonic is diminished; it is kept so the mode has a home chord.
        return triad.Quality == ChordQuality.Diminished && degree != 1 ? null : triad;
    }
}
