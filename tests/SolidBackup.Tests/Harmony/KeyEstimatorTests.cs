using SolidBackup.Analysis;
using SolidBackup.Harmony;
using SolidBackup.Measurement;

namespace SolidBackup.Tests.Harmony;

public sealed class KeyEstimatorTests
{
    public static TheoryData<string, double> NonModulating => new()
    {
        { "generated-melody-c-major", 4 },
        { "generated-melody-g-major", 4 },
        { "generated-melody-eb-major", 4 },
        { "generated-melody-f-sharp-major", 4 },
        { "generated-melody-a-minor", 4 },
        { "generated-melody-e-minor", 4 },
        { "generated-melody-c-minor", 4 },
        { "generated-melody-c-major", 8 },
        { "generated-melody-g-major", 8 },
        { "generated-melody-eb-major", 8 },
        { "generated-melody-f-sharp-major", 8 },
        { "generated-melody-a-minor", 8 },
        { "generated-melody-e-minor", 8 },
        { "generated-melody-c-minor", 8 },
    };

    public static TheoryData<string, double> Modulating => new()
    {
        { "generated-melody-c-to-g-major", 4 },
        { "generated-melody-a-to-d-minor", 4 },
        { "generated-melody-c-to-g-major", 8 },
        { "generated-melody-a-to-d-minor", 8 },
    };

    [Fact]
    public void Corpus_HasFourMajorThreeMinorAndTwoModulatingMelodies()
    {
        IReadOnlyList<ReferenceKey>[] keys = [.. KeyMelodies.Clips.Select(clip => clip.Reference.Keys)];

        Assert.Equal(4, keys.Count(list => list is [{ Key.Mode: Mode.Ionian }]));
        Assert.Equal(3, keys.Count(list => list is [{ Key.Mode: Mode.Aeolian }]));
        Assert.Equal(2, keys.Count(list => list.Count == 2));
        Assert.Contains(keys, list => list is [{ Key.Tonic: 6 }]);
    }

    [Theory]
    [InlineData(new[] { 0, 2, 4, 5, 7, 9, 11 }, 0, Mode.Ionian)]
    [InlineData(new[] { 9, 11, 0, 2, 4, 5, 8 }, 9, Mode.Aeolian)]
    public void Scale_GivesItsKey(int[] pitchClasses, int tonic, Mode mode)
    {
        int[] upAndDown = [.. pitchClasses, .. pitchClasses.Reverse().Skip(1), pitchClasses[0]];
        PitchEstimate[] estimates = [.. Enumerable.Range(0, 600 * 4)
            .Select(i => new PitchEstimate(i * 5.0, true, (60 + upAndDown[i / 100 % upAndDown.Length]) * 100.0, 1))];

        Assert.Equal(new Key(tonic, mode), KeyMelodies.KeyChanges(estimates)[^1].Key);
    }

    [Fact]
    public void TransposedMelody_GivesTheTransposedKey()
    {
        CorpusClip clip = KeyMelodies.Clip("generated-melody-a-minor");
        for (int semitones = 1; semitones < 12; semitones++)
        {
            List<(double TimeMs, Key Key)> changes = KeyMelodies.KeyChanges(KeyMelodies.Estimates(clip, (_, cents) => cents + (semitones * 100)));

            Assert.Equal(new Key((9 + semitones) % 12, Mode.Aeolian), changes[0].Key);
        }
    }

    [Fact]
    public void NoKey_BeforeKeyFirstSecondsOfNotes()
    {
        PitchEstimate[] estimates = KeyMelodies.Estimates(KeyMelodies.Clip("generated-melody-a-minor"));
        KeyEstimator estimator = new(keyChangeSeconds: 4, keyFirstSeconds: 2);
        double sungMs = 0;
        foreach (PitchEstimate estimate in estimates)
        {
            estimator.Process(estimate);
            sungMs += estimate.HasNote ? KeyMelodies.StepMs : 0;
            if (sungMs < 1990)
            {
                Assert.Null(estimator.Key);
            }
        }

        Assert.NotNull(estimator.Key);
    }

    [Theory]
    [MemberData(nameof(NonModulating))]
    public void MelodyInAKnownKey_GivesThatKeyAndKeepsIt(string name, double keyChangeSeconds)
    {
        CorpusClip clip = KeyMelodies.Clip(name);

        List<(double TimeMs, Key Key)> changes = KeyMelodies.KeyChanges(KeyMelodies.Estimates(clip), keyChangeSeconds);

        Assert.Equal([clip.Reference.Keys[0].Key], changes.Select(change => change.Key));
    }

    [Theory]
    [MemberData(nameof(Modulating))]
    public void ModulatingMelody_GivesTheNewKeyWithinKeyChangeSeconds(string name, double keyChangeSeconds)
    {
        CorpusClip clip = KeyMelodies.Clip(name);
        ReferenceKey before = clip.Reference.Keys[0];
        ReferenceKey after = clip.Reference.Keys[1];

        List<(double TimeMs, Key Key)> changes = KeyMelodies.KeyChanges(KeyMelodies.Estimates(clip), keyChangeSeconds);

        Assert.Equal([before.Key, after.Key], changes.Select(change => change.Key));
        Assert.InRange(changes[1].TimeMs, after.StartMs, after.StartMs + (keyChangeSeconds * 1000));
    }

    [Fact]
    public void SingleForeignNote_DoesNotChangeTheKey()
    {
        CorpusClip clip = KeyMelodies.Clip("generated-melody-c-major");
        // A C, D, F, G or A a semitone up is off the C major scale (an E or B would land on F or C).
        ReferenceSegment note = clip.Reference.SungSegments()
            .First(segment => segment.StartMs > 10_000 && (int)Math.Round(segment.MedianPitch / 100) % 12 is 0 or 2 or 5 or 7 or 9);
        PitchEstimate[] estimates = KeyMelodies.Estimates(clip, (time, cents) => note.Contains(time) ? cents + 100 : cents);

        Assert.Equal([new Key(0, Mode.Ionian)], KeyMelodies.KeyChanges(estimates).Select(change => change.Key));
    }

    [Fact]
    public void VibratoAcrossASemitoneBoundary_DoesNotCountAsNotes()
    {
        CorpusClip clip = KeyMelodies.Clip("generated-melody-c-major");

        // ±60 cents at 6 Hz: every peak crosses into the neighbouring semitone for about 30 ms.
        PitchEstimate[] estimates = KeyMelodies.Estimates(clip, (time, cents) => cents + (60 * Math.Sin(2 * Math.PI * 6 * time / 1000)));

        Assert.Equal([new Key(0, Mode.Ionian)], KeyMelodies.KeyChanges(estimates).Select(change => change.Key));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    public void BorrowedPassageShorterThanHalfOfKeyChangeSeconds_DoesNotChangeTheKey(double keyChangeSeconds)
    {
        CorpusClip clip = KeyMelodies.Clip("generated-melody-c-major");
        double start = 12_000;
        double end = start + (keyChangeSeconds * 1000 / 2) - 100;

        // Mode mixture from C minor: E, A and B are lowered to Eb, Ab and Bb.
        PitchEstimate[] estimates = KeyMelodies.Estimates(clip, (time, cents) =>
            time >= start && time < end && (int)Math.Round(cents / 100) % 12 is 4 or 9 or 11 ? cents - 100 : cents);

        Assert.Equal([new Key(0, Mode.Ionian)], KeyMelodies.KeyChanges(estimates, keyChangeSeconds).Select(change => change.Key));
    }
}
