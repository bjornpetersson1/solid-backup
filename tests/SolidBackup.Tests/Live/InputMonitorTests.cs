using SolidBackup.Accompaniment;
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

    // C4 and E4, then a pause: the chords are quiet enough to stay below the soft limit's knee.
    private static readonly float[] Voice = Chain.Join(Chain.Tone(60, -26, 0.5), Chain.Tone(64, -26, 0.5), TestSignal.Silence(0.2));

    // The chords alone, per order of the callbacks, computed once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, float[]> ChordsAlone = new();

    public static TheoryData<string> Orders => ["in out", "in in out out", "out in", "out out in in"];

    [Theory]
    [MemberData(nameof(Orders))]
    public void TheOutput_IsTheChordsPlusTheInputAtTheLevel_DelayedByAtMostTwoBuffers(string order)
    {
        float[] chords = Chords(order);
        float[] mixed = Run(Settings, Voice, order);

        int delay = Delay(chords, mixed, Voice, gain: 1);

        Assert.InRange(delay, 0, 2 * Settings.BufferSize);
        AssertMixed(chords, mixed, Voice, gain: 1, delay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void TheLevel_ScalesTheInput(int percent)
    {
        float[] chords = Chords("in out");
        float[] mixed = Run(Settings with { MonitorLevelPercent = percent }, Voice, "in out");

        AssertMixed(chords, mixed, Voice, gain: percent / 100f, delay: 0);
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
    public void AnInputThatRunsAhead_SkipsItsOldestSamples()
    {
        InputMonitor monitor = new(bufferSize: 4, levelPercent: 100);
        for (int buffer = 1; buffer <= 5; buffer++)
        {
            monitor.Write([buffer, buffer, buffer, buffer]);
        }

        float[] output = new float[4];
        monitor.MixInto(output);

        // Buffers 1–3 are skipped; buffer 4 plays and buffer 5 waits, one buffer beyond the one being played.
        Assert.Equal(Enumerable.Repeat((float)Synthesizer.Saturate(4), 4), output);
    }

    [Fact]
    public void AnInputThatRunsBehind_PlaysSilenceForTheMissingSamples()
    {
        InputMonitor monitor = new(bufferSize: 4, levelPercent: 50);
        monitor.Write([0.2f, 0.4f]);

        float[] output = new float[4];
        monitor.MixInto(output);

        Assert.Equal([0.1f, 0.2f, 0, 0], output);
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
    public void AStalledOutput_ResumesWithTheLatestInput()
    {
        // A ring of 32 samples; twelve buffers of 4 overwrite the oldest.
        InputMonitor monitor = new(bufferSize: 4, levelPercent: 100);
        for (int buffer = 1; buffer <= 12; buffer++)
        {
            monitor.Write([buffer / 100f, buffer / 100f, buffer / 100f, buffer / 100f]);
        }

        float[] output = new float[4];
        monitor.MixInto(output);

        Assert.Equal(Enumerable.Repeat(0.11f, 4), output);
    }

    [Fact]
    public void FadingOut_RampsTheInputToSilenceOverTheBuffer()
    {
        InputMonitor monitor = new(bufferSize: 4, levelPercent: 100);
        monitor.Write([0.4f, 0.4f, 0.4f, 0.4f]);

        float[] output = new float[4];
        monitor.MixInto(output, fadeOut: true);

        Assert.Equal([0.4f, 0.3f, 0.2f, 0.1f], output);
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
        chain.OnBuffer(loud);
        chain.Render(output);

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
    private static int Delay(float[] chords, float[] mixed, float[] voice, float gain)
    {
        return Enumerable.Range(0, (2 * Settings.BufferSize) + 1).MinBy(delay => Error(chords, mixed, voice, gain, delay));
    }

    private static void AssertMixed(float[] chords, float[] mixed, float[] voice, float gain, int delay)
    {
        Assert.InRange(Error(chords, mixed, voice, gain, delay), 0, 1e-6);
    }

    // The largest difference from chords + gain × the delayed voice, over the samples below the soft limit's knee.
    private static double Error(float[] chords, float[] mixed, float[] voice, float gain, int delay)
    {
        double worst = 0;
        for (int i = 0; i < mixed.Length; i++)
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
