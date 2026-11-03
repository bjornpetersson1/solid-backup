using SolidBackup.Accompaniment;
using SolidBackup.Configuration;

namespace SolidBackup.Tests.Accompaniment;

/// <summary>
/// The autoplay examples A1–A6 in docs/rhythm.md, through the whole chain: 48 kHz, 90 BPM (a beat is 32000 samples,
/// an eighth 16000), 4/4, Church in C, so the note C4 gives the chord C voiced C3 E3 G3 from sample 0.
/// </summary>
public class AutoplayTests
{
    private const int C3 = 48;
    private const int E3 = 52;
    private const int G3 = 55;
    private const int C4 = 60;
    private const int A4 = 69;
    private const int B4 = 71;
    private const int Silence = -1;
    private const int NoteEnd = 100_000;
    private const long End = NoteEnd + 480_000;

    // A melody note, then ten seconds of silence.
    private static readonly Part[] NoteThenSilence = [new(C4, NoteEnd), new(Silence, End - NoteEnd)];

    [Fact]
    public void A1_Arpeggio_GoesOnThroughTenSecondsOfSilence()
    {
        PatternRun run = Play("Arpeggio", [], NoteThenSilence);

        int[] tones = [C3, E3, G3];
        Assert.Equal(Every(16_000).Select(sample => (sample, tones[(int)(sample / 16_000 % 8 % 3)])), run.Starts.Select(tone => (tone.Sample, tone.Note)));
    }

    [Fact]
    public void A2_StrumWithTheGate_StrikesEveryBeatThroughTheSilence()
    {
        PatternRun run = Play("Strum", ["gate"], NoteThenSilence);

        Assert.Equal(Every(32_000), run.Starts.Where(tone => tone.Note == C3).Select(tone => tone.Sample));
        Assert.All(run.Starts, tone => Assert.Equal(tone.Sample / 32_000 % 4 == 0 ? 1 : Accent.OffBeat, tone.Strength));
    }

    [Fact]
    public void A3_HeldWithTheGate_KeepsTheChordWithoutANewStrike()
    {
        PatternRun run = Play("Held", ["gate"], NoteThenSilence);

        Assert.Equal([C3, E3, G3], run.Starts.Select(tone => tone.Note).Order());
        Assert.All(run.Starts, tone => Assert.True(tone.Sample < PatternRun.BufferSize));
        Assert.Empty(run.Releases);
    }

    [Fact]
    public void A4_WithDynamics_TheSilenceKeepsTheLastStrength()
    {
        // −19.5 dBFS is three quarters of the way from DynamicsQuietDbfs (−42) to DynamicsLoudDbfs (−12): −6 dB.
        PatternRun run = Play("Arpeggio", ["gate", "dynamics"], NoteThenSilence, noteDbfs: -19.5);

        double last = Math.Pow(10, -6 / 20.0);
        List<ToneEvent> inSilence = [.. run.Starts.Where(tone => tone.Sample >= NoteEnd + 16_000)];
        Assert.NotEmpty(inSilence);
        Assert.All(inSilence, tone => Assert.Equal(last * (tone.Sample % 128_000 == 0 ? 1 : Accent.OffBeat), tone.Strength, precision: 6));
    }

    [Fact]
    public void A4_TheKeptStrength_IsTheLastNotesStrongest_NotItsFadingEnd()
    {
        // The note at −19.5 dBFS (−6 dB), then its fading end at −36 dBFS (gate still open), then silence.
        PatternRun run = Play("Arpeggio", ["gate", "dynamics"], [new(C4, NoteEnd - 16_000), new(C4, 16_000, LevelDbfs: -36), new(Silence, 200_000)], noteDbfs: -19.5);

        List<ToneEvent> inSilence = [.. run.Starts.Where(tone => tone.Sample >= NoteEnd + 16_000)];
        Assert.NotEmpty(inSilence);
        Assert.All(inSilence, tone => Assert.Equal(Math.Pow(10, -6 / 20.0) * (tone.Sample % 128_000 == 0 ? 1 : Accent.OffBeat), tone.Strength, precision: 6));
    }

    [Fact]
    public void AnOnsetInTheSilence_DoesNotStrikeTheHeldChordAnew()
    {
        AppSettings settings = Chain.Settings("gate", "onsets") with { RhythmPattern = "Held", BufferSize = PatternRun.BufferSize, Autoplay = true };
        AnalysisFrame[] frames = Chain.Notes(settings, -30, [(C4, NoteEnd * 1000.0 / 48_000), (Silence, 200_000 * 1000.0 / 48_000)], 3000);
        AnalysisFrame[] gated = [.. frames.Select(frame => frame.Estimate.HasNote ? frame : frame with { GateOpen = false, LevelDbfs = -90, SmoothedLevelDbfs = -90 })];

        Chain.PlayRecorded(settings, gated, out PatternRun run);

        Assert.All(run.Starts, tone => Assert.True(tone.Sample < PatternRun.BufferSize));
        Assert.Empty(run.Releases);
    }

    [Fact]
    public void A5_APitchedNoiseBelowTheGate_DoesNotChangeTheChord()
    {
        ChainStep[] steps = Steps("Strum", ["gate"], [new(C4, NoteEnd), new(B4, 200_000, GateOpen: false), new(Silence, 100_000)], out PatternRun run);

        Assert.All(steps, step => Assert.Equal("C", step.Frame.Chord?.Symbol));
        Assert.All(steps, step => Assert.True(step.Frame.Sounding));
        Assert.Contains(run.Starts, tone => tone.Sample == 384_000 && tone.Note == C3);
    }

    [Fact]
    public void A6_ThePhraseResetStillHappens_WhileTheChordKeepsPlaying()
    {
        // B (G), 2500 ms of silence, then A: chosen afresh it gives F (priority 4), not Am as V's successor.
        ChainStep[] steps = Steps("Strum", ["gate"], [new(B4, 48_000), new(Silence, 120_000), new(A4, 48_000)], out _);

        Assert.All(steps.Where(step => step.Analysis.EndSample <= 168_000), step => Assert.Equal(("G", true), (step.Frame.Chord?.Symbol, step.Frame.Sounding)));
        Assert.Equal("F", steps[^1].Frame.Chord?.Symbol);
    }

    [Theory]
    [InlineData("Held")]
    [InlineData("Arpeggio")]
    [InlineData("Strum")]
    public void BeforeTheFirstNote_NothingPlays(string pattern)
    {
        PatternRun run = Play(pattern, ["gate"], [new(Silence, 100_000)]);

        Assert.Empty(run.Starts);
    }

    [Fact]
    public void WithAutoplayOff_TheGateSilencesTheSilenceAtOnce()
    {
        PatternRun run = Play("Strum", ["gate"], NoteThenSilence, autoplay: false);

        Assert.DoesNotContain(run.Starts, tone => tone.Sample >= NoteEnd);
        Assert.Contains(run.Releases, tone => tone.Sample is >= NoteEnd and < NoteEnd + PatternRun.BufferSize);
    }

    [Fact]
    public void WithAutoplayOff_ThePatternStopsAfterPhraseResetMs()
    {
        PatternRun run = Play("Arpeggio", [], NoteThenSilence, autoplay: false);

        Assert.DoesNotContain(run.Starts, tone => tone.Sample >= NoteEnd + 96_000);
    }

    private static PatternRun Play(string pattern, string[] features, Part[] parts, double noteDbfs = -30, bool autoplay = true)
    {
        Steps(pattern, features, parts, out PatternRun run, noteDbfs, autoplay);
        return run;
    }

    // The chain with only the named features on; a silence, and a note below the gate, have the gate closed and the level at −90 dBFS.
    private static ChainStep[] Steps(string pattern, string[] features, Part[] parts, out PatternRun run, double noteDbfs = -30, bool autoplay = true)
    {
        AppSettings settings = Chain.Settings(features) with { RhythmPattern = pattern, BufferSize = PatternRun.BufferSize, Autoplay = autoplay };
        double[] partEndsMs = [.. parts.Select((_, i) => parts.Take(i + 1).Sum(part => part.Samples) * 1000.0 / settings.SampleRate)];
        AnalysisFrame[] frames = Chain.Notes(settings, noteDbfs, [.. parts.Select(part => (part.Midi, part.Samples * 1000.0 / settings.SampleRate))]);
        AnalysisFrame[] gated = [.. frames.Select(frame =>
        {
            Part part = parts[Array.FindIndex(partEndsMs, end => frame.TimeMs <= end + 1e-9)];
            bool open = part.Midi != Silence && part.GateOpen;
            double level = part.LevelDbfs ?? noteDbfs;
            return open ? frame with { LevelDbfs = level, SmoothedLevelDbfs = level } : frame with { GateOpen = false, LevelDbfs = -90, SmoothedLevelDbfs = -90 };
        })];
        return Chain.PlayRecorded(settings, gated, out run);
    }

    // The samples from 0 up to End, every <paramref name="step"/>.
    private static IEnumerable<long> Every(long step)
    {
        for (long sample = 0; sample < End; sample += step)
        {
            yield return sample;
        }
    }

    private sealed record Part(int Midi, long Samples, bool GateOpen = true, double? LevelDbfs = null);
}
