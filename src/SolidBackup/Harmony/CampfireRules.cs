namespace SolidBackup.Harmony;

/// <summary>Lägereld: I, IV, V and vi in major; i, iv, V and VI in minor, with the major V.</summary>
public sealed class CampfireRules() : ScaleRules([1, 5, 4, 6], TriadOn)
{
    private static Chord? TriadOn(Key key, int degree)
    {
        Chord triad = key.MajorOrMinor.TriadOn(degree);
        return degree == 5 ? triad with { Quality = ChordQuality.Major } : triad;
    }
}
