namespace SolidBackup.Accompaniment;

/// <summary>The downbeat accent of Arpeggio and Strum (<c>docs/rhythm.md</c>): the first step of a bar at full strength, the others a little softer.</summary>
public static class Accent
{
    public const double OffBeat = 0.8;

    public static double For(int stepInBar)
    {
        return stepInBar == 0 ? 1 : OffBeat;
    }
}
