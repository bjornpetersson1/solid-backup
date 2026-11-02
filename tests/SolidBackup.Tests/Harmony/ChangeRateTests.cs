using SolidBackup.Accompaniment;
using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Measurement;
using SolidBackup.Rhythm;
using SolidBackup.Tests.Accompaniment;

namespace SolidBackup.Tests.Harmony;

/// <summary>
/// The change-rate examples C1–C9 in <c>docs/harmony.md</c> (Church, C Ionian, 120 BPM), and the maximum and the
/// sensitivity over the generated melodies.
/// </summary>
public sealed class ChangeRateTests
{
    [Fact]
    public void C1_AReadyNote_WaitsForBeatThree()
    {
        Assert.Equal(["0 C", "1000 G"], Run([("C", 0, 500), ("B", 500, 1500)], "2"));
    }

    [Fact]
    public void C2_WithOnePerBar_OnlyBeatOneIsAllowed()
    {
        Assert.Equal(["0 C", "2000 G"], Run([("C", 0, 500), ("B", 500, 2500)], "1"));
    }

    [Fact]
    public void C3_ANoteStillSoundingOnTheBeat_ChangesTheChord()
    {
        Assert.Equal(["0 C", "1000 F"], Run([("C", 0, 500), ("A", 500, 1200)], "2"));
    }

    [Fact]
    public void C4_AReadyNoteThatEndedBeforeItsBeat_ChangesNothing_AndTheNextAfterSilenceChoosesAtOnce()
    {
        Assert.Equal(["0 C", "1100 F"], Run([("C", 0, 500), ("B", 500, 900), (null, 900, 1100), ("A", 1100, 1600)], "2"));
    }

    [Fact]
    public void C5_EveryBeatWithHighSensitivity()
    {
        Assert.Equal(["0 C", "500 G"], Run([("C", 0, 200), ("B", 200, 700)], "Beat", "High"));
    }

    [Fact]
    public void C6_OffWithLowSensitivity_DoublesTheHold()
    {
        Assert.Equal(["0 C", "740 G"], Run([("C", 0, 500), ("B", 500, 1000)], "Off", "Low"));
    }

    [Fact]
    public void C7_InThreeFour_BeatsOneAndThree()
    {
        Assert.Equal(["0 C", "1000 G", "1500 Am"], Run([("C", 0, 500), ("B", 500, 1300), ("A", 1300, 2000)], "2", beatsPerBar: 3));
    }

    [Fact]
    public void C8_AnOnsetMakesTheNoteReady_ButItStillWaitsForTheBeat()
    {
        Assert.Equal(["0 C", "1000 G"], Run([("C", 0, 500), ("B", 500, 1500)], "2", immediateAtMs: 500));
    }

    [Fact]
    public void C9_JazzsLoop_IsNotLimited()
    {
        Assert.Equal(["0 Cmaj7", "1000 Am7", "2000 Dm7", "3000 G7(11)"], Run([("C", 0, 4000)], "2", style: HarmonyStyle.Jazz));
    }

    [Fact]
    public void JazzsLoop_WhileANoteWaitsForItsBeat_VoicesTheNoteBeingSung()
    {
        // B is ready at 2220 and waits for 4000; the loop's V7 at 3000 does not take the C that no longer sounds.
        Assert.Equal(
            ["0 Cmaj7", "1000 Am7", "2000 Dm7", "3000 G7"],
            Run([("C", 0, 2100), ("B", 2100, 4500)], "1", style: HarmonyStyle.Jazz));
    }

    [Theory]
    [InlineData(43, 7)]
    [InlineData(41, 23)]
    [InlineData(46, 13)]
    [InlineData(90, 3)]
    public void ATimeOnABeat_IsThatBeat(int bpm, long beat)
    {
        TempoGrid grid = new(48_000, bpm, beatsPerBar: 4);
        ChangeBeats beats = ChangeBeats.Create(grid, ChordChangesPerBar.Beat) ?? throw new InvalidOperationException("No grid.");
        double onTheBeat = beat * grid.BeatMs;

        Assert.Equal(grid.StepSample(beat, 1) * 1000.0 / 48_000, beats.NextAtOrAfter(onTheBeat), precision: 9);
    }

    [Fact]
    public void InTheChain_AStrumBeatPlaysTheChordThatChangesOnIt()
    {
        // F (A3) then B3, ready at about 1140; with "2" at 120 BPM it changes on beat 3 at 2000 (sample 96000). The
        // estimates lag their buffers by 12 ms, as the detector's window does.
        AppSettings settings = Chain.Settings() with { Bpm = 120, ChordChangesPerBar = "2", RhythmPattern = "Strum" };
        AnalysisFrame[] analyses = [.. Chain.Notes(settings, -30, [(57, 1020), (59, 1500)])
            .Select(frame => frame with { Estimate = frame.Estimate with { TimeMs = frame.Estimate.TimeMs - 12 } })];
        ChainStep[] steps = Chain.Play(settings, analyses);

        int beatBuffer = Array.FindIndex(steps, step => step.Analysis.StartSample <= 96_000 && 96_000 < step.Analysis.EndSample);

        Assert.Equal("G", steps[beatBuffer].Frame.Chord?.Symbol);
        Assert.Equal("F", steps[beatBuffer - 1].Frame.Chord?.Symbol);
    }

    [Fact]
    public void InTheChain_AnOnsetsStrike_ComesWithTheChordOnItsBeat()
    {
        // B3 comes 20 ms after its onset at 1000 and is ready at once; with "2" it is chosen on 2000.
        AppSettings settings = Chain.Settings("onsets") with { Bpm = 120, ChordChangesPerBar = "2" };
        ChainStep[] steps = Chain.Play(settings, Chain.Notes(settings, -30, [(57, 1020), (59, 1500)], 1000));

        ChainStep strike = Assert.Single(steps, step => step.Frame.Strike);
        Assert.InRange(strike.Analysis.TimeMs, 2000, 2002);
        Assert.Equal("G", strike.Frame.Chord?.Symbol);
    }

    [Fact]
    public void TheDefaults_AreTwoPerBarAndMediumSensitivity()
    {
        AppSettings defaults = new SettingsStore(SettingsStore.BundledDefaultsPath, "").Load().Settings;

        Assert.Equal("2", defaults.ChordChangesPerBar);
        Assert.Equal("Medium", defaults.ChordChangeSensitivity);
    }

    // Each maximum with its allowed beats (from 0) in 4/4 and 3/4.
    private static readonly (string PerBar, int[] InFourFour, int[] InThreeFour)[] AllowedBeats =
        [("1", [0], [0]), ("2", [0, 2], [0, 2]), ("Beat", [0, 1, 2, 3], [0, 1, 2])];

    // The melodies as generated (a short silence between notes) and legato, built once for every case.
    private static readonly Lazy<PitchEstimate[][]> AllMelodies = new(() =>
        [.. KeyMelodies.Clips.SelectMany(clip => new[] { KeyMelodies.Estimates(clip), KeyMelodies.Legato(clip) })]);

    public static TheoryData<HarmonyStyle, string, int, int[]> StylesMaximaAndTimeSignatures()
    {
        TheoryData<HarmonyStyle, string, int, int[]> data = [];
        // Jazz's loop and II – V – I change on their own beats, not limited by the maximum, and the loop waits for a
        // note still in its hold times (E5-6), so only the other styles are counted.
        foreach (HarmonyStyle style in Enum.GetValues<HarmonyStyle>().Where(style => style != HarmonyStyle.Jazz))
        {
            foreach ((string perBar, int[] inFourFour, int[] inThreeFour) in AllowedBeats)
            {
                data.Add(style, perBar, 4, inFourFour);
                data.Add(style, perBar, 3, inThreeFour);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(StylesMaximaAndTimeSignatures))]
    public void OverEveryMelody_ChangesWhileSingingFallOnAllowedBeats_AndNoBarHasMoreThanTheMaximum(HarmonyStyle style, string perBar, int beatsPerBar, int[] allowed)
    {
        AppSettings settings = Settings(perBar, "High", beatsPerBar, style) with { Key = HarmonySettings.AutoKey };
        TempoGrid grid = TempoGrid.FromSettings(settings);
        foreach (PitchEstimate[] estimates in AllMelodies.Value)
        {
            Dictionary<long, int> changesPerBar = [];
            Harmonizer harmonizer = Harmonizer.FromSettings(settings);
            bool sounded = false;
            foreach (PitchEstimate estimate in estimates)
            {
                Chord? before = harmonizer.Chord;
                bool afterSilence = estimate.HasNote && !sounded;
                sounded = estimate.HasNote;
                if (!harmonizer.Process(estimate) || before is null || afterSilence)
                {
                    continue;
                }

                long beat = (long)Math.Floor(estimate.TimeMs / grid.BeatMs);
                Assert.InRange(estimate.TimeMs - (beat * grid.BeatMs), 0, KeyMelodies.StepMs);
                Assert.Contains((int)(beat % beatsPerBar), allowed);
                long bar = beat / beatsPerBar;
                changesPerBar[bar] = changesPerBar.GetValueOrDefault(bar) + 1;
                Assert.InRange(changesPerBar[bar], 1, allowed.Length);
            }
        }
    }

    [Fact]
    public void HigherSensitivity_GivesMoreChanges_OnTheSameMelody()
    {
        // The C major melody sung legato at double speed: notes of 225 ms and shorter.
        PitchEstimate[] estimates = KeyMelodies.Legato(KeyMelodies.Clip("generated-melody-c-major"), speed: 2);

        int Changes(string sensitivity)
        {
            return Melody.Run(Harmonizer.FromSettings(Settings("Off", sensitivity, 4, HarmonyStyle.Church) with { Key = "C" }), estimates);
        }

        int low = Changes("Low");
        int medium = Changes("Medium");
        int high = Changes("High");

        Assert.True(high > medium && medium > low, $"High {high}, Medium {medium}, Low {low}.");
    }

    [Fact]
    public void TheChain_BuiltFromTheSettings_FollowsTheChangeRate()
    {
        // A3 then B3 (F → G) through the whole chain, as live mode builds it.
        AppSettings off = Chain.Settings() with { Bpm = 120 };
        AnalysisFrame[] analyses = Chain.Notes(off, -30, [(57, 1020), (59, 1000)]);

        double Change(AppSettings settings)
        {
            return Chain.Play(settings, analyses).First(step => step.Frame.Chord?.Symbol == "G").Analysis.TimeMs;
        }

        Assert.InRange(Change(off), 1140, 1145);
        Assert.InRange(Change(off with { ChordChangeSensitivity = "Low" }), 1260, 1265);
        Assert.InRange(Change(off with { ChordChangesPerBar = "1" }), 2000, 2002);
        Assert.InRange(Change(off with { ChordChangesPerBar = "Beat" }), 1500, 1502);
    }

    private static AppSettings Settings(string perBar, string sensitivity, int beatsPerBar, HarmonyStyle style)
    {
        return Chain.Settings() with
        {
            HarmonyStyle = style.ToString(),
            Bpm = 120,
            TimeSignature = $"{beatsPerBar}/4",
            ChordChangesPerBar = perBar,
            ChordChangeSensitivity = sensitivity,
        };
    }

    private static List<string> Run(
        (string? Name, double StartMs, double EndMs)[] notes,
        string perBar,
        string sensitivity = "Medium",
        int beatsPerBar = 4,
        HarmonyStyle style = HarmonyStyle.Church,
        double immediateAtMs = double.NaN)
    {
        Harmonizer harmonizer = Harmonizer.FromSettings(Settings(perBar, sensitivity, beatsPerBar, style));
        Note[] melody = [.. notes.Select(note => new Note(note.Name is null ? null : $"{note.Name}4", note.StartMs, note.EndMs))];

        // One estimate per millisecond, so every time in the examples is met exactly.
        return [.. Melody.Changes(harmonizer, Melody.Estimates(melody, 48_000, 48), immediateAtMs)
            .Select(change => $"{Math.Round(change.TimeMs)} {change.Chord.Symbol}")];
    }
}
