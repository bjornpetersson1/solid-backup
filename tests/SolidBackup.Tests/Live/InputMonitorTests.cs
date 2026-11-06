using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Live;
using SolidBackup.Recording;
using SolidBackup.Synthesis;
using SolidBackup.Tests.Accompaniment;
using SolidBackup.Tests.Analysis;
using SolidBackup.Tests.Audio;
using SolidBackup.Tests.Cli;

namespace SolidBackup.Tests.Live;

/// <summary>Monitoring (E7-3): the input mixed into the live chain's output, through <see cref="InputMonitor"/>.</summary>
public class InputMonitorTests
{
    private static readonly AppSettings Settings = Chain.Settings() with { MonitorOn = true, MonitorLevelPercent = 100 };

    private static readonly int MaxDelay = (int)InputMonitor.MaxDelay(Settings.BufferSize, Settings.SampleRate);

    // The monitor fades in at its start and may correct its delay once, at the end of its first window (a quarter of a
    // second). The mix is checked before and after that correction, each part with its own delay, leaving out only the
    // fade-in and the correction's buffer and crossfade.
    private static readonly int Crossfades = Crossfade.Length(Settings.SampleRate);
    private static readonly int FirstWindowEnd = Settings.SampleRate / 4;
    private static readonly (int From, int To)[] Parts =
    [
        // The window counts whole buffers, so the correction can start in the buffer before its end.
        ((3 * Settings.BufferSize) + Crossfades, FirstWindowEnd - Settings.BufferSize),
        (FirstWindowEnd + (4 * Settings.BufferSize) + Crossfades, int.MaxValue),
    ];

    // C4 and E4, then a pause: the chords are quiet enough to stay below the soft limit's knee.
    private static readonly float[] Voice = Chain.Join(Chain.Tone(60, -26, 0.5), Chain.Tone(64, -26, 0.5), TestSignal.Silence(0.2));

    // The chords alone, per order of the callbacks, computed once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, float[]> ChordsAlone = new();

    public static TheoryData<string> Orders => ["in out", "in in out out", "out in", "out out in in"];

    [Theory]
    [MemberData(nameof(Orders))]
    public void TheOutput_IsTheChordsPlusTheInputAtTheLevel_DelayedByAtMostFiveMilliseconds(string order)
    {
        float[] chords = Chords(order);
        float[] mixed = Run(Settings, Voice, order);

        foreach ((int From, int To) part in Parts)
        {
            int delay = Delay(chords, mixed, Voice, gain: 1, part);

            Assert.InRange(delay, 0, MaxDelay);
            AssertMixed(chords, mixed, Voice, gain: 1, delay, part);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void TheLevel_ScalesTheInput(int percent)
    {
        float[] chords = Chords("in out");
        float[] mixed = Run(Settings with { MonitorLevelPercent = percent }, Voice, "in out");

        // The delay does not depend on the level, so it is found at full level.
        float[] full = Run(Settings, Voice, "in out");
        foreach ((int From, int To) part in Parts)
        {
            AssertMixed(chords, mixed, Voice, gain: percent / 100f, Delay(chords, full, Voice, gain: 1, part), part);
        }
    }

    [Fact]
    public void AFullScaleVoiceOverAFullChord_NeverGoesBeyondFullScale()
    {
        float[] loud = Chain.Join(Chain.Tone(60, 0, 1), Chain.Tone(64, -3, 1));
        AppSettings settings = TestSettings.Default with { Key = "C", MonitorOn = true, MonitorLevelPercent = 100 };

        float[] mixed = Run(settings, loud, "in out");

        Assert.All(mixed, sample => Assert.InRange(sample, -1f, 1f));
        Assert.Contains(mixed, sample => Math.Abs(sample) > 0.95f);
    }

    [Fact]
    public void Playing_WaitsForABufferAndAMargin_ThenFadesIn()
    {
        InputMonitor monitor = new(bufferSize: 64, levelPercent: 100, sampleRate: 48000);
        float[] output = new float[64];
        monitor.Write(Enumerable.Repeat(0.4f, 64).ToArray());
        monitor.MixInto(output);
        Assert.All(output, sample => Assert.Equal(0, sample));

        monitor.Write(Enumerable.Repeat(0.4f, 64).ToArray());
        monitor.MixInto(output);
        Assert.True(output[0] > 0 && output[0] < 0.01f);
        Assert.True(output.Zip(output.Skip(1)).All(pair => pair.First < pair.Second));

        monitor.Write(Enumerable.Repeat(0.4f, 64).ToArray());
        Array.Clear(output);
        monitor.MixInto(output);
        Assert.Equal(0.4f, output[^1]);
    }

    [Fact]
    public void AStalledOutput_ResumesWithTheLatestInput_CountingAnOverflow()
    {
        InputMonitor monitor = new(bufferSize: 64, levelPercent: 100, sampleRate: 48000);
        for (int buffer = 1; buffer <= 40; buffer++)
        {
            monitor.Write(Enumerable.Repeat(buffer / 100f, 64).ToArray());
        }

        float[] output = new float[64];
        monitor.MixInto(output);

        // The latest buffer, fading in as playing starts again.
        float fadeIn = 64f / (Crossfade.Length(48000) + 1);
        Assert.Equal(0.40f * fadeIn, output[^1], 1e-6f);
        Assert.True(monitor.Overflows > 0);
        Assert.Equal(monitor.Overflows, monitor.Interruptions);
    }

    [Fact]
    public void FadingOut_RampsTheInputToSilenceOverTheBuffer()
    {
        InputMonitor monitor = new(bufferSize: 64, levelPercent: 100, sampleRate: 48000);
        float[] output = new float[64];
        for (int buffer = 0; buffer < 4; buffer++)
        {
            monitor.Write(Enumerable.Repeat(0.4f, 64).ToArray());
            Array.Clear(output);
            monitor.MixInto(output);
        }

        monitor.Write(Enumerable.Repeat(0.4f, 64).ToArray());
        Array.Clear(output);
        monitor.MixInto(output, fadeOut: true);

        Assert.Equal(0.4f, output[0]);
        Assert.True(output[^1] < 0.01f);
        Assert.True(output.Zip(output.Skip(1)).All(pair => pair.First >= pair.Second));
    }

    [Fact]
    public void TheRecording_HoldsOnlyTheChordsOnItsOutputChannel()
    {
        KeptStream withMonitor = new();
        KeptStream without = new();

        float[] mixed = Run(Settings, Voice, "in out", withMonitor);
        float[] chords = Run(Settings with { MonitorOn = false }, Voice, "in out", without);

        Assert.NotEqual(chords, mixed);
        Assert.Equal(without.Contents, withMonitor.Contents);
    }

    [Fact]
    public void BothSides_AllocateNothingPerCallback()
    {
        LiveChain chain = new(new Accompanist(Settings), monitor: InputMonitor.FromSettings(Settings, outputSafe: true));

        LiveChainTests.AssertAllocatesNothing(chain, Voice, Settings);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void LiveMode_MonitorsOnlyWithMonitorOnAndToASafeOutput(bool monitorOn, bool outputSafe, bool monitoring)
    {
        LiveMode mode = new(new FakeDevices(), Settings with { MonitorOn = monitorOn }, chainDelayMs: null, outputSafe: outputSafe);

        Assert.Equal(monitoring, mode.Monitoring);
    }

    [Fact]
    public void TheSnapshot_CountsTheMonitorsInterruptions_OnlyWhileMonitoring()
    {
        LiveMode monitoring = new(new FakeDevices(), Settings, chainDelayMs: null, outputSafe: true);
        LiveMode off = new(new FakeDevices(), Settings with { MonitorOn = false }, chainDelayMs: null, outputSafe: true);

        Assert.Equal(0, monitoring.Snapshot().MonitorInterruptions);
        Assert.Null(off.Snapshot().MonitorInterruptions);

        monitoring.StopMonitoring();
        Assert.Null(monitoring.Snapshot().MonitorInterruptions);
    }

    [Fact]
    public void StoppingTheMonitoring_LeavesTheChordsAlone()
    {
        LiveChain chords = new(new Accompanist(Settings with { MonitorOn = false }));
        LiveChain stopped = new(new Accompanist(Settings), monitor: InputMonitor.FromSettings(Settings, outputSafe: true));
        stopped.StopMonitoring();
        float[] expected = new float[Settings.BufferSize];
        float[] actual = new float[Settings.BufferSize];

        for (int start = 0; start + Settings.BufferSize <= Voice.Length; start += Settings.BufferSize)
        {
            chords.OnBuffer(Voice.AsSpan(start, Settings.BufferSize));
            stopped.OnBuffer(Voice.AsSpan(start, Settings.BufferSize));
            chords.Render(expected);
            stopped.Render(actual);
            Assert.Equal(expected, actual);
        }

        Assert.False(stopped.Monitoring);
    }

    [Fact]
    public void TheFadeOut_EndsTheMonitoringAfterOneBuffer()
    {
        LiveChain chain = new(new Accompanist(Settings), monitor: InputMonitor.FromSettings(Settings, outputSafe: true));
        float[] loud = [.. Enumerable.Repeat(0.5f, Settings.BufferSize)];
        float[] output = new float[Settings.BufferSize];
        for (int buffer = 0; buffer < 4; buffer++)
        {
            chain.OnBuffer(loud);
            chain.Render(output);
        }

        chain.FadeOut();
        chain.OnBuffer(loud);
        chain.Render(output);
        Assert.True(output[0] > output[^1]);
        chain.OnBuffer(loud);
        chain.Render(output);

        Assert.All(output, sample => Assert.True(Math.Abs(sample) < 0.5f));
    }

    private static float[] Chords(string order)
    {
        return ChordsAlone.GetOrAdd(order, key => Run(Settings with { MonitorOn = false }, Voice, key));
    }

    // Runs the chain with its two callbacks in the given repeating order, e.g. "in in out out"; returns the output.
    private static float[] Run(AppSettings settings, float[] voice, string order, KeptStream? recording = null)
    {
        using SessionRecorder? recorder = recording is null ? null : new SessionRecorder(recording, settings.SampleRate, settings.BufferSize);
        LiveChain chain = new(new Accompanist(settings), recorder, InputMonitor.FromSettings(settings, outputSafe: true));
        string[] steps = order.Split(' ');
        int size = settings.BufferSize;
        List<float> output = [];
        float[] buffer = new float[size];
        int input = 0;
        while (output.Count < voice.Length)
        {
            foreach (string step in steps)
            {
                if (step == "in")
                {
                    chain.OnBuffer(input + size <= voice.Length ? voice.AsSpan(input, size) : new float[size]);
                    input += size;
                }
                else
                {
                    chain.Render(buffer);
                    output.AddRange(buffer);
                }
            }
        }

        recorder?.Stop();
        return [.. output.Take(voice.Length)];
    }

    // The shift of the input in the mixed output, in samples: the one that explains the difference best.
    private static int Delay(float[] chords, float[] mixed, float[] voice, float gain, (int From, int To) part)
    {
        int best = 0;
        double bestError = double.MaxValue;
        for (int delay = 0; delay <= MaxDelay; delay++)
        {
            double error = Error(chords, mixed, voice, gain, delay, part, bestError);
            if (error < bestError)
            {
                (best, bestError) = (delay, error);
            }
        }

        return best;
    }

    private static void AssertMixed(float[] chords, float[] mixed, float[] voice, float gain, int delay, (int From, int To) part)
    {
        Assert.InRange(Error(chords, mixed, voice, gain, delay, part), 0, 1e-6);
    }

    // The largest difference from chords + gain × the delayed voice, over the part's samples below the soft limit's
    // knee. Stops early once it is past giveUpAt, as a delay search only needs to know it is worse.
    private static double Error(
        float[] chords, float[] mixed, float[] voice, float gain, int delay, (int From, int To) part, double giveUpAt = double.MaxValue)
    {
        double worst = 0;
        for (int i = part.From; i < Math.Min(part.To, mixed.Length) && worst < giveUpAt; i++)
        {
            if (Math.Abs(mixed[i]) >= Synthesizer.SaturationStart)
            {
                continue;
            }

            float input = i >= delay ? voice[i - delay] : 0;
            worst = Math.Max(worst, Math.Abs(mixed[i] - (chords[i] + (gain * input))));
        }

        return worst;
    }
}
