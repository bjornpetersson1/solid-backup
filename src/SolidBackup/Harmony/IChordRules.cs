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

    /// <summary>Called when the key in effect is new or changes, and on a phrase reset; a style with state starts over.</summary>
    void Reset(Key key)
    {
    }
}
