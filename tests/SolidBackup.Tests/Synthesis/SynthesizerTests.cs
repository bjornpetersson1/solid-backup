using SolidBackup.Analysis;
using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Tests.Synthesis;

public class SynthesizerTests
{
    private const int SampleRate = 48_000;
    private const int BufferSize = 64;

    public static TheoryData<Sound> Sounds => [Sound.Pad, Sound.Organ, Sound.Pluck];

    [Theory]
    [InlineData(Sound.Organ, "C")]
    [InlineData(Sound.Organ, "Cm7")]
    [InlineData(Sound.Pluck, "C")]
    [InlineData(Sound.Pluck, "Cm7")]
    public void RenderedChord_HasEveryChordTonesFundamentalWithinFiveCents(Sound sound, string symbol)
    {
        Chord chord = Parse(symbol);
        Synthesizer synthesizer = new(sound, SampleRate);
        synthesizer.SetChord(chord, strength: 1);
        float[] samples = Render(synthesizer, SampleRate * 2);

        // Pluck dies away, so it is measured over the first 200 ms after its attack; Organ once it is steady.
        ReadOnlySpan<float> steady = sound == Sound.Pluck
            ? samples.AsSpan(Ms(5), Ms(200))
            : samples.AsSpan(Ms(500), Ms(1000));
        Spectrum spectrum = new(steady, SampleRate);

        AssertFundamentals(chord, frequency => spectrum.PeakNear(frequency));
    }

    // Pad's fundamental is the centre of its two oscillators, 7 cents up and down; a long window resolves them.
    [Theory]
    [InlineData("C")]
    [InlineData("Cm7")]
    public void RenderedPadChord_HasEveryChordTonesFundamentalWithinFiveCents(string symbol)
    {
        Chord chord = Parse(symbol);
        Synthesizer synthesizer = new(Sound.Pad, SampleRate);
        synthesizer.SetChord(chord, strength: 1);
        float[] samples = Render(synthesizer, SampleRate * 7);
        Spectrum spectrum = new(samples.AsSpan(Ms(1000), 262_144), SampleRate, size: 524_288);
        double detune = Math.Pow(2, synthesizer.Preset.DetuneCents / 1200);

        AssertFundamentals(chord, frequency =>
        {
            (double lowHz, double lowMagnitude) = spectrum.PeakNear(frequency / detune, searchCents: 3);
            (double highHz, double highMagnitude) = spectrum.PeakNear(frequency * detune, searchCents: 3);
            return (Math.Sqrt(lowHz * highHz), Math.Min(lowMagnitude, highMagnitude));
        });
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void Render_WithStartsReleasesAndChordChanges_AllocatesNothing(Sound sound)
    {
        Synthesizer synthesizer = new(sound, SampleRate);
        float[] buffer = new float[BufferSize];
        Chord[] chords = [Parse("C"), Parse("Am"), Parse("Cmaj7(#11)"), Parse("F#dim")];
        PlayEvents(synthesizer, chords, buffer, buffers: 400);

        long before = GC.GetAllocatedBytesForCurrentThread();
        PlayEvents(synthesizer, chords, buffer, buffers: 2000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void FiveToneChord_SoundsInFull()
    {
        Synthesizer synthesizer = new(Sound.Organ, SampleRate);
        synthesizer.SetChord(Parse("Cmaj7(#11)"), strength: 1);
        Render(synthesizer, BufferSize);

        foreach (int note in new[] { 48, 52, 55, 59, 66 })
        {
            Assert.Equal(1, synthesizer.Held(note));
        }
    }

    [Fact]
    public void AllVoicesBusy_NewToneTakesTheVoiceReleasingLongest()
    {
        Synthesizer synthesizer = FullSynthesizer();
        synthesizer.ReleaseTone(41);
        Render(synthesizer, BufferSize);
        synthesizer.ReleaseTone(42);
        Render(synthesizer, BufferSize);

        synthesizer.StartTone(70, strength: 1);
        Render(synthesizer, BufferSize);

        Assert.Equal(0, synthesizer.Sounding(41));
        Assert.Equal(1, synthesizer.Sounding(42));
        Assert.Equal(1, synthesizer.Held(70));
    }

    [Fact]
    public void AllVoicesHeld_NewToneTakesTheOldestVoice()
    {
        Synthesizer synthesizer = FullSynthesizer();

        synthesizer.StartTone(70, strength: 1);
        Render(synthesizer, BufferSize);

        Assert.Equal(0, synthesizer.Sounding(40));
        Assert.Equal(1, synthesizer.Held(41));
        Assert.Equal(1, synthesizer.Held(70));
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void FiveToneChordAtFullStrength_PeaksAtMostPointNine(Sound sound)
    {
        Synthesizer synthesizer = new(sound, SampleRate);
        synthesizer.SetChord(Parse("Cmaj7(#11)"), strength: 1);

        float[] samples = Render(synthesizer, SampleRate * 2);

        Assert.InRange(samples.Max(Math.Abs), 0.1, 0.9);
    }

    [Fact]
    public void ChordChange_KeepsCommonTonesReleasesLeavingOnesAndStartsNewOnes()
    {
        Synthesizer synthesizer = new(Sound.Pad, SampleRate);
        synthesizer.SetChord(Parse("C"), strength: 1);
        Render(synthesizer, SampleRate / 2);
        long startsBefore = synthesizer.Starts;

        synthesizer.SetChord(Parse("Am"), strength: 1);
        Render(synthesizer, BufferSize);

        Assert.Equal(startsBefore + 1, synthesizer.Starts);
        Assert.Equal(1, synthesizer.Held(48));
        Assert.Equal(1, synthesizer.Held(52));
        Assert.Equal(1, synthesizer.Held(57));
        Assert.Equal(0, synthesizer.Held(55));
        Assert.Equal(1, synthesizer.Sounding(55));
    }

    [Fact]
    public void ChordChangesQueuedTogether_EachLeadsFromTheChordBeforeItOnItsSample()
    {
        Synthesizer synthesizer = new(Sound.Pad, SampleRate);
        synthesizer.SetChord(Parse("C"), strength: 1);
        synthesizer.SetChord(Parse("Am"), strength: 1, offset: BufferSize * 3);

        Render(synthesizer, BufferSize * 4);

        Assert.Equal(4, synthesizer.Starts);
        Assert.Equal(1, synthesizer.Held(48));
        Assert.Equal(1, synthesizer.Held(57));
        Assert.Equal(0, synthesizer.Held(55));
    }

    [Fact]
    public void ChordChange_StartsACommonToneAgainOnceItHasDiedAway()
    {
        Synthesizer synthesizer = new(Sound.Pluck, SampleRate);
        synthesizer.SetChord(Parse("C"), strength: 1);
        Render(synthesizer, SampleRate);

        synthesizer.SetChord(Parse("Am"), strength: 1);
        Render(synthesizer, BufferSize);

        Assert.Equal(1, synthesizer.Held(48));
        Assert.Equal(1, synthesizer.Held(52));
        Assert.Equal(1, synthesizer.Held(57));
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void StartsChangesAndRelease_StepNoMoreThanTheSteadyChords(Sound sound)
    {
        int[] events = [Ms(100), Ms(1600), Ms(3100), Ms(4600)];
        Synthesizer synthesizer = new(sound, SampleRate);
        synthesizer.SetChord(Parse("C"), strength: 1, offset: events[0]);
        synthesizer.SetChord(Parse("Am"), strength: 1, offset: events[1]);
        synthesizer.SetChord(Parse("F#dim"), strength: 1, offset: events[2]);
        synthesizer.ReleaseAll(offset: events[3]);
        float[] samples = Render(synthesizer, Ms(5600));
        int attack = Ms(synthesizer.Preset.PlayedAttackMs);

        for (int i = 0; i < events.Length; i++)
        {
            double steadyBefore = i > 0 ? LargestStep(samples, events[i - 1] + attack, events[i]) : 0;
            double steadyAfter = i < events.Length - 1 ? LargestStep(samples, events[i] + attack, events[i + 1]) : 0;
            double atEvent = LargestStep(samples, events[i], events[i] + Ms(20));

            Assert.True(
                atEvent <= Math.Max(steadyBefore, steadyAfter),
                $"Event {i}: step {atEvent} > steady {Math.Max(steadyBefore, steadyAfter)}");
        }
    }

    [Fact]
    public void StolenVoice_FadesOutInsteadOfStoppingAtOnce()
    {
        // One loud voice, taken first as the oldest, beside fifteen almost silent ones.
        Synthesizer synthesizer = new(Sound.Organ, SampleRate);
        synthesizer.StartTone(48, strength: 1);
        for (int note = 49; note < 48 + Synthesizer.VoiceCount; note++)
        {
            synthesizer.StartTone(note, strength: 0.01, offset: 1);
        }

        float[] before = Render(synthesizer, Ms(200));
        synthesizer.StartTone(36, strength: 1);
        float[] after = Render(synthesizer, Ms(20));

        Assert.Equal(0, synthesizer.Sounding(48));
        float[] aroundTheSteal = [before[^1], .. after];
        Assert.True(LargestStep(aroundTheSteal, 1, aroundTheSteal.Length) <= LargestStep(before, Ms(100), before.Length) * 1.1, "The stolen voice stopped with a click.");
    }

    [Fact]
    public void FastChordChanges_NeverPassFullScale()
    {
        string[] symbols = ["Cmaj7(9)", "F#m7b5(11)", "Bbmaj7(#11)", "Em7(9)", "Abmaj7(9)", "Dm7(11)", "Dbmaj7(#11)", "Gm7(9)"];
        Synthesizer synthesizer = new(Sound.Pad, SampleRate);
        for (int i = 0; i < symbols.Length; i++)
        {
            synthesizer.SetChord(Parse(symbols[i]), strength: 1, offset: i * Ms(100));
        }

        float[] samples = Render(synthesizer, SampleRate * 2);

        Assert.True(samples.Max(Math.Abs) <= 1);
    }

    [Fact]
    public void ReleasingAStolenVoice_LetsItFadeAndStartsNothing()
    {
        Synthesizer synthesizer = FullSynthesizer();
        synthesizer.StartTone(70, strength: 1);
        synthesizer.ReleaseTone(70, offset: 10);

        Render(synthesizer, Ms(20));

        Assert.Equal(0, synthesizer.Held(70));
        Assert.Equal(0, synthesizer.Sounding(40));
    }

    [Fact]
    public void SixteenVoicesAtFullStrength_NeverPassFullScale()
    {
        Synthesizer synthesizer = new(Sound.Organ, SampleRate);
        for (int note = 48; note < 48 + Synthesizer.VoiceCount; note++)
        {
            synthesizer.StartTone(note, strength: 1);
        }

        float[] samples = Render(synthesizer, SampleRate);

        Assert.True(samples.Max(Math.Abs) <= 1);
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void EveryAttackAndRelease_LastsAtLeastFiveMs(Sound sound)
    {
        SoundPreset preset = SoundPreset.For(sound);

        Assert.True(preset.AttackMs >= SoundPreset.MinEdgeMs);
        Assert.True(preset.ReleaseMs >= SoundPreset.MinEdgeMs);
    }

    [Fact]
    public void ShortEdges_AreStretchedToFiveMs()
    {
        Synthesizer synthesizer = new(Sound.Organ, SampleRate);
        synthesizer.StartTone(69, strength: 1);
        float[] samples = Render(synthesizer, Ms(4));

        // Organ's 10 ms attack is above the floor; a tone never reaches full level within 4 ms either way.
        Assert.True(samples.Max(Math.Abs) < Synthesizer.VoiceGain * 0.5);
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void SameEvents_GiveTheSameSamplesBitForBit(Sound sound)
    {
        float[] first = RenderSequence(sound);
        float[] second = RenderSequence(sound);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EventInALaterBuffer_StartsOnItsSample()
    {
        Synthesizer synthesizer = new(Sound.Organ, SampleRate);
        synthesizer.StartTone(69, strength: 1, offset: 100);

        float[] samples = Render(synthesizer, 200);

        Assert.All(samples[..101], sample => Assert.Equal(0, sample));
        Assert.NotEqual(0, samples[101]);
    }

    [Fact]
    public void Release_EndsInSilence()
    {
        Synthesizer synthesizer = new(Sound.Pad, SampleRate);
        synthesizer.SetChord(Parse("C"), strength: 1);
        Render(synthesizer, SampleRate);

        synthesizer.ReleaseAll();
        Render(synthesizer, Ms(610));

        Assert.True(synthesizer.IsSilent);
    }

    private static void AssertFundamentals(Chord chord, Func<double, (double Hz, double Magnitude)> find)
    {
        Span<int> notes = stackalloc int[ChordVoicing.MaxTones];
        int count = ChordVoicing.Voice(chord, notes);
        (double Hz, double Magnitude)[] peaks = new (double, double)[count];
        for (int i = 0; i < count; i++)
        {
            peaks[i] = find(Pitch.Frequency(notes[i] * 100));
        }

        double strongest = peaks.Max(peak => peak.Magnitude);
        for (int i = 0; i < count; i++)
        {
            double expected = Pitch.Frequency(notes[i] * 100);
            Assert.InRange(Pitch.Cents(peaks[i].Hz) - Pitch.Cents(expected), -5, 5);
            Assert.True(peaks[i].Magnitude > strongest / 10, $"The tone at {expected:0.00} Hz is too weak to count as found.");
        }
    }

    private static Synthesizer FullSynthesizer()
    {
        Synthesizer synthesizer = new(Sound.Pad, SampleRate);
        for (int note = 40; note < 40 + Synthesizer.VoiceCount; note++)
        {
            synthesizer.StartTone(note, strength: 0.5);
            Render(synthesizer, BufferSize);
        }

        return synthesizer;
    }

    private static void PlayEvents(Synthesizer synthesizer, Chord[] chords, float[] buffer, int buffers)
    {
        for (int i = 0; i < buffers; i++)
        {
            if (i % 40 == 0)
            {
                synthesizer.SetChord(chords[i / 40 % chords.Length], strength: 0.8, offset: i % BufferSize);
            }

            if (i % 25 == 0)
            {
                synthesizer.StartTone(72 + (i % 5), strength: 0.5, offset: 10);
                synthesizer.ReleaseTone(72 + ((i + 3) % 5), offset: 30);
            }

            if (i % 300 == 299)
            {
                synthesizer.ReleaseAll(offset: 5);
            }

            synthesizer.Render(buffer);
        }
    }

    private static float[] RenderSequence(Sound sound)
    {
        Synthesizer synthesizer = new(sound, SampleRate);
        synthesizer.SetChord(Parse("C"), strength: 1);
        synthesizer.SetChord(Parse("G7"), strength: 0.7, offset: Ms(400));
        synthesizer.ReleaseAll(offset: Ms(900));
        return Render(synthesizer, SampleRate);
    }

    private static float[] Render(Synthesizer synthesizer, int length)
    {
        float[] samples = new float[length];
        for (int start = 0; start < length; start += BufferSize)
        {
            synthesizer.Render(samples.AsSpan(start, Math.Min(BufferSize, length - start)));
        }

        return samples;
    }

    private static double LargestStep(float[] samples, int from, int to)
    {
        double largest = 0;
        for (int i = Math.Max(1, from); i < to; i++)
        {
            largest = Math.Max(largest, Math.Abs(samples[i] - samples[i - 1]));
        }

        return largest;
    }

    private static int Ms(double ms)
    {
        return (int)(ms * SampleRate / 1000);
    }

    private static Chord Parse(string symbol)
    {
        Assert.True(ChordSymbol.TryParse(symbol, out Chord chord));
        return chord;
    }
}
