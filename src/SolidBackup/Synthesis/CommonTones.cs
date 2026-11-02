namespace SolidBackup.Synthesis;

/// <summary>Where a chord change sends its tones: the synthesizer itself, or the events queued on it.</summary>
public interface IToneTarget
{
    bool IsHeld(int note);

    void Release(int note);

    void Start(int note);
}

/// <summary>
/// The click-free rule for changing from one set of chord tones to the next: a tone in both keeps sounding without a
/// new attack, a tone only in the old set is released, and a tone only in the new set starts with an attack. A shared
/// tone that has already died away (a sound without sustain) starts again. With <c>strike</c>, every old tone is
/// released and every new one starts anew.
/// </summary>
public static class CommonTones
{
    // Generic over a struct target, so the change allocates nothing.
    public static void Change<TTarget>(ReadOnlySpan<int> previous, ReadOnlySpan<int> next, bool strike, TTarget target)
        where TTarget : struct, IToneTarget
    {
        foreach (int note in previous)
        {
            if (strike || !next.Contains(note))
            {
                target.Release(note);
            }
        }

        foreach (int note in next)
        {
            if (strike || !previous.Contains(note) || !target.IsHeld(note))
            {
                target.Start(note);
            }
        }
    }
}
