using SolidBackup.Configuration;
using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

/// <summary>The pitch detector on the whole corpus, with the bundled default settings (BufferSize 64).</summary>
public sealed class PitchMeasurementTests
{
    private static readonly AppSettings Defaults = new SettingsStore(SettingsStore.BundledDefaultsPath, "").Load().Settings;

    private static readonly Lazy<PitchMeasurement> Measured = new(() =>
    {
        IEnumerable<CorpusClip> clips = TestCorpus.At(Defaults.SampleRate).Concat(RecordedCorpus.Load(CorpusDirectory.Locate()));
        return PitchMeasurement.Run(clips, Defaults, parallel: true);
    });

    [Fact]
    public void EveryClip_IsMeasuredWithTheDefaultBufferSize()
    {
        Assert.Equal(64, Defaults.BufferSize);
        Assert.Equal(TestCorpus.Generated.Count + RecordingScript.Read(CorpusDirectory.Locate().ScriptPath).Count, Measured.Value.Score.Clips.Count);
    }

    [Fact]
    public void PitchAccuracy_IsAtLeast95Percent()
    {
        Assert.True(Measured.Value.Score.Total.MeetsPitchAccuracy, Measured.Value.ToMarkdown());
    }

    [Fact]
    public void OctaveErrors_AreAtMost1Percent()
    {
        Assert.True(Measured.Value.Score.Total.MeetsOctaveErrors, Measured.Value.ToMarkdown());
    }

    [Fact]
    public void FalseNotes_AreAtMost2Percent()
    {
        Assert.True(Measured.Value.Score.Total.MeetsFalseNotes, Measured.Value.ToMarkdown());
    }

    [Fact]
    public void AnalysingTheCorpus_IsFasterThanRealTime()
    {
        Assert.True(Measured.Value.AnalysisTime.TotalSeconds < Measured.Value.AudioSeconds, Measured.Value.ToMarkdown());
    }

    [Fact]
    public void Report_HasEveryClipTheTotalAndTheTargets()
    {
        string report = Measured.Value.ToMarkdown();

        Assert.All(Measured.Value.Score.Clips, clip => Assert.Contains($"| {clip.Clip} |", report));
        Assert.Contains("| **Totalt** |", report);
        Assert.Contains("- Träffsäkerhet ≥ 95 %: uppnått", report);
        Assert.Contains("- Oktavfel ≤ 1 %: uppnått", report);
        Assert.Contains("- Falska toner ≤ 2 %: uppnått", report);
        Assert.Contains("µs per buffert", report);
    }
}
