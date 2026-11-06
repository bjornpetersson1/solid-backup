using SolidBackup.Accompaniment;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Rhythm;
using SolidBackup.Synthesis;
using SolidBackup.Tests.Accompaniment;
using SolidBackup.Tests.Analysis;

namespace SolidBackup.Tests.Synthesis;

/// <summary>
/// E8-2: the synthesizer itself does not click. Through the whole chain, at each chord change, strike, start and stop,
/// the output steps no further from one sample to the next than the same chord struck alone from silence.
/// </summary>
public class ChordEventStepTests
{
    // A note struck alone is the sharpest event the envelope allows; anything else stays within this of it.
    private const double StepBound = 1.5;

    // Notes that change the chord, repeated notes that strike it again, and pauses that stop and start it.
    private static readonly float[] Voice = Chain.Join(
        Chain.Tone(60, -20, 0.6), Chain.Tone(65, -20, 0.6), Chain.Tone(65, -12, 0.4), Chain.Tone(67, -20, 0.6),
        TestSignal.Silence(0.6), Chain.Tone(69, -20, 0.6), Chain.Tone(62, -20, 0.6), Chain.Tone(71, -14, 0.6),
        TestSignal.Silence(0.6), Chain.Tone(64, -20, 0.6), Chain.Tone(65, -20, 0.6), Chain.Tone(60, -20, 0.8));

    public static TheoryData<Sound, RhythmPatternName> SoundsAndPatterns()
    {
        TheoryData<Sound, RhythmPatternName> data = [];
        foreach (Sound sound in Enum.GetValues<Sound>())
        {
            foreach (RhythmPatternName pattern in Enum.GetValues<RhythmPatternName>())
            {
                data.Add(sound, pattern);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SoundsAndPatterns))]
    public void EachChordEvent_StepsNoFurtherThanItsChordStruckAloneFromSilence(Sound sound, RhythmPatternName pattern)
    {
        AppSettings settings = Chain.Settings() with { Sound = sound.ToString(), RhythmPattern = pattern.ToString() };
        int buffer = settings.BufferSize;
        int window = settings.SampleRate / 50;

        float[] chords = AccompanyCommand.Accompany(Voice, settings, noVoice: true);
        ChainStep[] steps = Chain.Run(settings, Voice);

        int events = 0;
        for (int i = 1; i < steps.Length; i++)
        {
            AccompanimentFrame before = steps[i - 1].Frame;
            AccompanimentFrame now = steps[i].Frame;
            bool changed = now.Chord != before.Chord || now.Sounding != before.Sounding || now.Strike;
            if (!changed || (now.Chord ?? before.Chord) is not { } chord)
            {
                continue;
            }

            // From the buffer before the event, to cover a release that starts there, over the next 20 ms.
            int from = (i - 1) * buffer;
            double atEvent = TestSignal.LargestStep(chords, from, Math.Min(chords.Length, from + buffer + window));
            double struckAlone = StruckAlone(sound, chord, settings.SampleRate);
            Assert.True(atEvent <= StepBound * struckAlone, $"Event at buffer {i} ({chord}): a step of {atEvent} against {struckAlone} struck alone.");
            events++;
        }

        Assert.True(events >= 6, $"Only {events} chord events.");
    }

    // The largest step of a chord struck at full strength from silence and held for half a second.
    private static double StruckAlone(Sound sound, Chord chord, int sampleRate)
    {
        Synthesizer synthesizer = new(sound, sampleRate);
        synthesizer.SetChord(chord, strength: 1);
        float[] samples = new float[sampleRate / 2];
        synthesizer.Render(samples);
        return TestSignal.LargestStep(samples, 0, samples.Length);
    }
}
