namespace SolidBackup.Harmony;

/// <summary>One harmony style's rules: which chord a note gives, in a key, after the current chord.</summary>
public interface IChordRules
{
    /// <param name="pitchClass">The note's pitch class, 0 = C.</param>
    /// <param name="key">The key in effect.</param>
    /// <param name="current">The chord in effect, or null before the first note and after a phrase reset.</param>
    /// <returns>The chord to play; <paramref name="current"/> when it stays.</returns>
    Chord Choose(int pitchClass, Key key, Chord? current);

    /// <summary>
    /// The key the style is playing in, given the key in effect: that key, unless the style has moved it (Blues);
    /// null for a style without a key.
    /// </summary>
    Key? CurrentKey(Key key)
    {
        return key;
    }

    /// <summary>Whether the style needs a key; one that does not (Free) chooses from the first note even with <c>auto</c>.</summary>
    bool UsesKey => true;

    /// <summary>Called when the key in effect is new or changes, and on a phrase reset; a style with state starts over.</summary>
    void Reset(Key key)
    {
    }

    /// <summary>
    /// Called on every estimate, note or not, after <see cref="Choose"/>: a chord the style changes to on its own, at
    /// a time it keeps (Jazz's loop and cadence), without the hold times; null for none.
    /// </summary>
    /// <param name="nowMs">The estimate's time.</param>
    /// <param name="soundingPitchClass">The stable note sounding now (the last one chosen for), or <see cref="PitchClass.None"/> in silence.</param>
    /// <param name="notePending">True while a new note waits out its hold time before it is chosen for.</param>
    Chord? Advance(double nowMs, int soundingPitchClass, bool notePending)
    {
        return null;
    }

    /// <summary>Whether the style keeps the chord going through any silence (Jazz's loop), so the phrase does not end.</summary>
    bool KeepsPlaying => false;
}
