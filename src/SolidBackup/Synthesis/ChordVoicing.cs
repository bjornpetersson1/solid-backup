using SolidBackup.Harmony;

namespace SolidBackup.Synthesis;

/// <summary>
/// Where a chord's tones are played, always within C3–C5. The first chord is in close voicing: the root in
/// octave 3 (C3–B3, MIDI 48–59), and each next tone, in the order <see cref="Chord.Intervals"/> gives with the
/// tension last, on the nearest note above the previous one; a tone past C5 goes down an octave. Each later chord is led from the one before (<see cref="Lead"/>):
/// shared tones stay where they are, and new tones go as near as they can to the tones that left.
/// </summary>
public static class ChordVoicing
{
    /// <summary>The range a led chord stays in, C3–C5; close voicing puts the root in the octave from <see cref="LowestNote"/>.</summary>
    public const int LowestNote = 48;

    public const int HighestNote = 72;

    /// <summary>The largest number of tones a chord has: a seventh chord with a tension.</summary>
    public const int MaxTones = 5;

    /// <summary>The largest number of tones a shaped chord has: a full chord adds a bass root.</summary>
    public const int MaxShapedTones = MaxTones + 1;

    /// <summary>Writes the chord's MIDI notes in close voicing, lowest first, and returns how many there are.</summary>
    public static int Voice(Chord chord, Span<int> notes)
    {
        IReadOnlyList<int> intervals = Chord.Intervals(chord.Quality);
        int previous = LowestNote + chord.Root;
        notes[0] = previous;
        int count = 1;
        for (int i = 1; i < intervals.Count; i++)
        {
            previous = NextAbove(previous, (chord.Root + intervals[i]) % 12);
            notes[count++] = previous;
        }

        if (chord.Tension != Chord.NoTension && !Contains(intervals, chord.Tension))
        {
            notes[count++] = NextAbove(previous, (chord.Root + chord.Tension) % 12);
        }

        // A high root with a seventh and a tension reaches past C5; those tones go down an octave.
        for (int i = 0; i < count; i++)
        {
            if (notes[i] > HighestNote)
            {
                notes[i] -= 12;
            }
        }

        notes[..count].Sort();
        return count;
    }

    /// <summary>
    /// Voices <paramref name="chord"/> led from <paramref name="previous"/> (no previous gives close voicing), writes
    /// its MIDI notes lowest first and returns how many there are. A tone in both chords keeps its note. Each new
    /// tone, in chord order, takes the note of its pitch class within C3–C5 nearest to a tone that left and has not
    /// been taken yet (or to any previous tone, once every leaving tone is taken); a tie goes to the lower note.
    /// </summary>
    public static int Lead(Chord chord, ReadOnlySpan<int> previous, Span<int> notes)
    {
        int count = Voice(chord, notes);
        if (previous.IsEmpty)
        {
            return count;
        }

        Span<int> pitchClasses = stackalloc int[MaxTones];
        for (int i = 0; i < count; i++)
        {
            pitchClasses[i] = notes[i] % 12;
        }

        Span<bool> taken = stackalloc bool[previous.Length];
        for (int i = 0; i < previous.Length; i++)
        {
            taken[i] = pitchClasses[..count].Contains(previous[i] % 12);
        }

        for (int i = 0; i < count; i++)
        {
            int kept = IndexOfPitchClass(previous, pitchClasses[i]);
            notes[i] = kept >= 0 ? previous[kept] : NearestLeaving(pitchClasses[i], previous, taken);
        }

        notes[..count].Sort();
        return count;
    }

    /// <summary>
    /// Writes the tones of a voiced chord that sound in <paramref name="shape"/>, lowest first, and returns how many
    /// there are. Sparse keeps the root and the perfect fifth above it, or the root and the third (minor or major)
    /// when there is no perfect fifth, or else the root and the lowest other tone (Free's clusters); full adds the root
    /// an octave below the voiced root.
    /// </summary>
    public static int Shape(Chord chord, ReadOnlySpan<int> voiced, ChordShape shape, Span<int> notes)
    {
        int root = IndexOfPitchClass(voiced, chord.Root);
        switch (shape)
        {
            case ChordShape.Sparse:
                int partner = -1;
                foreach (int interval in SparsePartners)
                {
                    partner = IndexOfPitchClass(voiced, (chord.Root + interval) % 12);
                    if (partner >= 0)
                    {
                        break;
                    }
                }

                if (partner < 0 && voiced.Length > 1)
                {
                    partner = root == 0 ? 1 : 0;
                }

                notes[0] = voiced[root];
                int count = 1;
                if (partner >= 0)
                {
                    notes[count++] = voiced[partner];
                }

                notes[..count].Sort();
                return count;
            case ChordShape.Full:
                notes[0] = voiced[root] - 12;
                voiced.CopyTo(notes[1..]);
                notes[..(voiced.Length + 1)].Sort();
                return voiced.Length + 1;
            default:
                voiced.CopyTo(notes);
                return voiced.Length;
        }
    }

    // The tone a sparse chord keeps beside the root: the perfect fifth, else the minor or major third.
    private static readonly int[] SparsePartners = [7, 3, 4];

    private static bool Contains(IReadOnlyList<int> intervals, int interval)
    {
        for (int i = 0; i < intervals.Count; i++)
        {
            if (intervals[i] == interval)
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOfPitchClass(ReadOnlySpan<int> notes, int pitchClass)
    {
        for (int i = 0; i < notes.Length; i++)
        {
            if (notes[i] % 12 == pitchClass)
            {
                return i;
            }
        }

        return -1;
    }

    private static int NearestLeaving(int pitchClass, ReadOnlySpan<int> previous, Span<bool> taken)
    {
        bool anyLeft = taken.Contains(false);
        int bestNote = -1;
        int bestIndex = -1;
        int bestDistance = int.MaxValue;
        for (int note = LowestNote + PitchClass.Interval(LowestNote % 12, pitchClass); note <= HighestNote; note += 12)
        {
            for (int i = 0; i < previous.Length; i++)
            {
                if (anyLeft && taken[i])
                {
                    continue;
                }

                int distance = Math.Abs(note - previous[i]);
                if (distance < bestDistance)
                {
                    (bestNote, bestIndex, bestDistance) = (note, i, distance);
                }
            }
        }

        taken[bestIndex] = true;
        return bestNote;
    }

    private static int NextAbove(int note, int pitchClass)
    {
        return note + PitchClass.Interval(note + 1, pitchClass) + 1;
    }
}
