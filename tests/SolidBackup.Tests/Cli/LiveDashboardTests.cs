using SolidBackup.Analysis;
using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Live;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public class LiveDashboardTests
{
    // Ten rows between the panel's top and bottom border, and the footer.
    private const int Lines = 10 + 2 + 1;

    private static readonly DashboardContext FromLive = new(ReturnsToMenu: false, OnSpeakers: false);

    private static readonly LiveSnapshot Playing = TestSnapshots.Playing;

    [Fact]
    public void EveryRow_ShowsItsPartOfTheLiveState()
    {
        string[] lines = Rendered(Playing, FromLive, 120);

        Assert.Contains(LiveDashboard.Title, lines[0]);
        Assert.Contains(lines, line => line.Contains("Ton") && line.Contains("A4 +12 cent"));
        Assert.Contains(lines, line => line.Contains("Ackord") && line.Contains("Am"));
        Assert.Contains(lines, line => line.Contains("Tonart") && line.Contains("C-dur") && !line.Contains("(auto)"));
        Assert.Contains(lines, line => line.Contains("Nivå") && line.Contains("-24.0 dBFS · grind öppen") && line.Contains('│'));
        Assert.Contains(lines, line => line.Contains("Stil") && line.Contains("Kyrko · Pad · Arpeggio, 90 BPM, 4/4"));
        Assert.Contains(lines, line => line.Contains("Byten") && line.Contains("2 per takt, känslighet medel"));
        Assert.Contains(lines, line => line.Contains("Autospel") && line.Contains("av"));
        Assert.Contains(lines, line => line.Contains("Funktioner") && line.Contains("grind ✓  ansättningar ✓  ackordkaraktär ✓  dynamik ✓"));
        Assert.Contains(lines, line => line.Contains("Latens") && line.Contains("42.8 ms ✓ mål 50 · in 6.0 · kedja 30.5 · överl. 1.3 · ut 5.0"));
        Assert.Contains(lines, line => line.Contains("Avbrott") && line.Contains('0'));
        Assert.Equal("q/Esc: avsluta", lines[^1].Trim());
    }

    [Fact]
    public void NoNoteAndNoKey_AreShownAsADash()
    {
        LiveSnapshot quiet = Playing with { State = new LiveState(PitchEstimate.NoNote(0, 0), -70, GateOpen: false, Chord: null, Key: null) };

        string[] lines = Rendered(quiet, FromLive, 120);

        Assert.Contains(lines, line => line.Contains("Ton") && line.Contains('–'));
        Assert.Contains(lines, line => line.Contains("Ackord") && line.Contains('–'));
        Assert.Contains(lines, line => line.Contains("Tonart") && line.Contains('–'));
        Assert.Contains(lines, line => line.Contains("grind stängd"));
    }

    [Fact]
    public void AnEstimatedKey_IsMarkedAuto()
    {
        string[] lines = Rendered(Playing with { Settings = Playing.Settings with { Key = HarmonySettings.AutoKey } }, FromLive, 120);

        Assert.Contains(lines, line => line.Contains("C-dur (auto)"));
    }

    [Fact]
    public void WithTheGateOff_TheGateIsNotShown()
    {
        string[] lines = Rendered(Playing with { Settings = Playing.Settings with { GateOn = false } }, FromLive, 120);

        Assert.DoesNotContain(lines, line => line.Contains("grind öppen"));
        Assert.Contains(lines, line => line.Contains("grind ✗"));
    }

    [Fact]
    public void Autoplay_IsShownWhenOn()
    {
        string[] lines = Rendered(Playing with { Settings = Playing.Settings with { Autoplay = true } }, FromLive, 120);

        Assert.Contains(lines, line => line.Contains("Autospel") && line.Contains("på"));
    }

    [Fact]
    public void LatencyOverTheTarget_IsMarked()
    {
        string[] lines = Rendered(Playing with { Latency = new LiveLatency(44.5, 34, 1.3, 10) }, FromLive, 120);

        Assert.Contains(lines, line => line.Contains("89.8 ms ✗ mål 50"));
    }

    [Fact]
    public void AnUnknownChainDelay_IsAQuestionMark()
    {
        string[] lines = Rendered(Playing with { Latency = new LiveLatency(6, null, 1.3, 5) }, FromLive, 120);

        Assert.Contains(lines, line => line.Contains("? ms mål 50") && line.Contains("kedja ?"));
    }

    [Fact]
    public void DropoutsAndTheRecording_AreShown()
    {
        LiveSnapshot recording = Playing with { Counters = new AudioCounters(2, 1, 0, 0, 0), Recording = new RecordingStatus(83.4, 2) };

        string[] lines = Rendered(recording, FromLive, 120);

        Assert.Contains(lines, line => line.Contains("Avbrott") && line.Contains('3') && line.Contains("Inspelning 01:23 (tappade 2)"));
    }

    [Fact]
    public void TheSpeakerWarningAndTheMenuFooter_AreShown()
    {
        string[] lines = Rendered(Playing, new DashboardContext(ReturnsToMenu: true, OnSpeakers: true), 80);

        Assert.Equal(Lines + 1, lines.Length);
        Assert.All(lines, line => Assert.InRange(line.Length, 0, 80));
        Assert.Contains(lines, line => line.Contains("Varning") && line.Contains(LiveDashboard.SpeakerRow));
        Assert.Equal("q/Esc: tillbaka till menyn", lines[^1].Trim());
    }

    [Fact]
    public void At120Columns_TheWidestValuesFitWithoutWrappingOrCutting()
    {
        Chord widestChord = Enum.GetValues<ChordQuality>()
            .SelectMany(quality => Chord.Tensions.Select(tension => new Chord(1, quality, tension)))
            .MaxBy(chord => chord.Symbol.Length);
        Key widestKey = Key.All.MaxBy(key => HarmonyNames.Key(key).Length);
        string widestStyle = Enum.GetValues<HarmonyStyle>().MaxBy(style => HarmonyNames.Style(style).Length).ToString();
        LiveSnapshot widest = new(
            new LiveState(new PitchEstimate(0, HasNote: true, 9649, Confidence: 1), -0.1, GateOpen: true, widestChord, widestKey),
            TestSettings.Default with { HarmonyStyle = widestStyle, Sound = "Organ", RhythmPattern = "Arpeggio", Bpm = 240, TimeSignature = "3/4", Key = HarmonySettings.AutoKey, ChordChangesPerBar = "Beat" },
            new LiveLatency(12345.6, 12345.6, 12345.6, 12345.6),
            new AudioCounters(9_999_999, 0, 0, 0, 0),
            new RecordingStatus(5999, 9_999_999));

        string[] lines = Rendered(widest, new DashboardContext(ReturnsToMenu: true, OnSpeakers: true), 120);

        Assert.Equal(Lines + 1, lines.Length);
        Assert.All(lines, line => Assert.InRange(line.Length, 0, 120));
        string all = string.Join('\n', lines);
        Assert.Contains($"{widestChord.Symbol}", all);
        Assert.Contains($"{HarmonyNames.Key(widestKey)} (auto)", all);
        Assert.Contains("49382.4 ms", all);
        Assert.Contains("ut 12345.6", all);
        Assert.Contains("9999999   Inspelning 99:59 (tappade 9999999)", all);
    }

    [Fact]
    public void At80Columns_EveryRowStillFitsOnOneLine()
    {
        // Ordinary values at their widest: two-digit device latencies (the built-in microphone reports ~45 ms), a
        // three-digit total, a level below the meter's floor with the gate closed.
        LiveSnapshot ordinary = Playing with
        {
            State = Playing.State with { LevelDbfs = -120, GateOpen = false },
            Latency = new LiveLatency(45, 34, 10.7, 33.3),
            Counters = new AudioCounters(12, 3, 0, 0, 0),
            Recording = new RecordingStatus(3599, 12),
        };

        string[] lines = Rendered(ordinary, FromLive, 80);

        Assert.Equal(Lines, lines.Length);
        Assert.All(lines, line => Assert.InRange(line.Length, 0, 80));
        Assert.Contains(lines, line => line.Contains("123.0 ms ✗ mål 50 · in 45.0 · kedja 34.0 · överl. 10.7 · ut 33.3"));
        Assert.Contains(lines, line => line.Contains("<-60 dBFS · grind stängd"));
    }

    [Fact]
    public void AGateThresholdBelowTheMeter_IsNotMarked()
    {
        Assert.Equal("██████████░░░░░░░░░░", LiveDashboard.Meter(-30, gateThresholdDbfs: -75, width: 20));
    }

    [Theory]
    [InlineData(-60, "░░░░░░░░░░░░░░░░░░│░")]
    [InlineData(-90, "░░░░░░░░░░░░░░░░░░│░")]
    [InlineData(-30, "██████████░░░░░░░░│░")]
    [InlineData(0, "██████████████████│█")]
    public void TheMeter_FillsWithTheLevelAndMarksTheGate(double levelDbfs, string expected)
    {
        Assert.Equal(expected, LiveDashboard.Meter(levelDbfs, gateThresholdDbfs: -6, width: 20));
    }

    private static string[] Rendered(LiveSnapshot snapshot, DashboardContext context, int width)
    {
        TestConsole console = new();
        console.Profile.Width = width;
        console.Write(LiveDashboard.Render(snapshot, context, width));
        return console.Output.TrimEnd('\n').Split('\n');
    }
}
