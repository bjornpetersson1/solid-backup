using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Live;
using SolidBackup.Measurement;
using SolidBackup.Recording;
using SolidBackup.Tests.Accompaniment;
using SolidBackup.Tests.Analysis;
using SolidBackup.Tests.Audio;

namespace SolidBackup.Tests.Live;

public class LiveChainTests
{
    private static readonly CorpusClip Singing = RecordedCorpus.Load(CorpusDirectory.Locate(), "singing");

    public static TheoryData<string> Patterns => ["Held", "Arpeggio", "Strum"];

    [Theory]
    [MemberData(nameof(Patterns))]
    public void AlternatingBuffers_PlayExactlyWhatAccompanyRenders_OnARecording(string pattern)
    {
        float[] voice = Singing.Samples[..(3 * Singing.SampleRate)];
        AppSettings settings = TestSettings.Default with { SampleRate = Singing.SampleRate, RhythmPattern = pattern };

        Assert.Equal(AccompanyCommand.Accompany(voice, settings, noVoice: true), Live(voice, settings));
    }

    [Theory]
    [MemberData(nameof(Patterns))]
    public void AlternatingBuffers_PlayExactlyWhatAccompanyRenders_OnAGeneratedMelody(string pattern)
    {
        float[] voice = Chain.Join(
            Chain.Tone(60, -24, 0.6), Chain.Tone(64, -18, 0.6), TestSignal.Silence(0.3), Chain.Tone(67, -14, 0.6), Chain.Tone(65, -30, 0.6));
        AppSettings settings = Chain.Settings("gate", "onsets", "character", "dynamics") with { RhythmPattern = pattern };

        Assert.Equal(AccompanyCommand.Accompany(voice, settings, noVoice: true), Live(voice, settings));
    }

    [Fact]
    public void AStrikeDecidedBetweenTwoOutputBuffers_IsStillPlayed()
    {
        // The same note sung again 15 dB louder: an onset strikes the chord that is already sounding.
        AppSettings settings = Chain.Settings("onsets");
        float[] voice = Chain.Join(Chain.Tone(60, -36, 0.5), Chain.Tone(60, -16, 0.5));
        int size = settings.BufferSize;
        int strike = StrikeAfter(settings, voice, 0.5);
        Accompanist accompanist = new(settings);
        LiveChain chain = new(accompanist);
        float[] output = new float[size];
        for (int i = 0; i < strike; i++)
        {
            chain.OnBuffer(voice.AsSpan(i * size, size));
            chain.Render(output);
        }

        long startsBefore = accompanist.Synthesizer.Starts;
        chain.OnBuffer(voice.AsSpan(strike * size, size));
        chain.OnBuffer(voice.AsSpan((strike + 1) * size, size));
        chain.Render(output);

        Assert.True(accompanist.Synthesizer.Starts > startsBefore);
    }

    [Theory]
    [MemberData(nameof(Patterns))]
    public void BothSides_AllocateNothingPerCallback(string pattern)
    {
        float[] voice = Singing.Samples[..(2 * Singing.SampleRate)];
        AppSettings settings = TestSettings.Default with { SampleRate = Singing.SampleRate, RhythmPattern = pattern };
        using SessionRecorder recorder = new(new KeptStream(), settings.SampleRate, settings.BufferSize);
        LiveChain chain = new(new Accompanist(settings), recorder);
        float[] output = new float[settings.BufferSize];
        Run(chain, voice, output);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(chain, voice, output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ARecording_StartsTheOutputWithTheFirstBufferAfterTheInputsFirst()
    {
        AppSettings settings = Chain.Settings();
        KeptStream stream = new();
        SessionRecorder recorder = new(stream, settings.SampleRate, settings.BufferSize);
        LiveChain chain = new(new Accompanist(settings), recorder);
        float[] output = new float[settings.BufferSize];

        // The output stream starts first and plays three buffers before the input delivers.
        chain.Render(output);
        chain.Render(output);
        chain.Render(output);
        chain.OnBuffer(new float[settings.BufferSize]);
        chain.Render(output);
        recorder.Stop();

        Assert.Equal(settings.BufferSize * 4, (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(stream.Contents.AsSpan(40)));
    }

    [Theory]
    [InlineData("Pad")]
    [InlineData("Organ")]
    public void FadeOut_LetsTheChordRingOutOverTheSoundsRelease(string sound)
    {
        AppSettings settings = Chain.Settings("gate") with { Sound = sound };
        Accompanist accompanist = new(settings);
        LiveChain chain = new(accompanist);
        float[] output = new float[settings.BufferSize];
        Run(chain, Chain.Tone(60, -20, 0.5), output);
        Assert.False(chain.IsSilent);

        // The voice goes on, but after FadeOut nothing new sounds and the chord's release runs its course.
        chain.FadeOut();
        float[] stillSinging = Chain.Tone(60, -20, 2);
        int buffers = 0;
        for (int start = 0; !chain.IsSilent && start + output.Length <= stillSinging.Length; start += output.Length, buffers++)
        {
            chain.OnBuffer(stillSinging.AsSpan(start, output.Length));
            chain.Render(output);
        }

        double fadeMs = buffers * output.Length * 1000.0 / settings.SampleRate;
        Assert.True(chain.IsSilent);
        Assert.InRange(fadeMs, accompanist.Synthesizer.Preset.PlayedReleaseMs * 0.5, LiveMode.MaxFadeOut.TotalMilliseconds);
    }

    [Fact]
    public void State_FollowsTheLatestInputBuffer()
    {
        AppSettings settings = Chain.Settings("gate");
        LiveChain chain = new(new Accompanist(settings));
        Assert.False(chain.TryReadState(out _));

        float[] note = Chain.Tone(69, -20, 0.5);
        Run(chain, note, new float[settings.BufferSize]);

        Assert.True(chain.TryReadState(out LiveState state));
        Assert.Equal("A4 0 cent", state.Estimate.ToString());
        Assert.True(state.GateOpen);
        Assert.Equal(-20, state.LevelDbfs, precision: 0);
        Assert.NotNull(state.Chord);
        Assert.Equal(new Key(0, Mode.Ionian), state.Key);
    }

    // The input and the output alternate buffer by buffer, as the two callbacks do with no jitter.
    private static float[] Live(float[] voice, AppSettings settings)
    {
        int buffers = (voice.Length + settings.BufferSize - 1) / settings.BufferSize;
        LiveChain chain = new(new Accompanist(settings));
        using MemoryAudioInput input = new(voice, settings.SampleRate, settings.BufferSize);
        using MemoryAudioOutput output = new(settings.SampleRate, settings.BufferSize, buffers * settings.BufferSize);
        output.Start(chain);
        input.Start(new ThenPull(chain, output));
        return output.Samples[..voice.Length].ToArray();
    }

    // A plain loop: the allocation test measures this thread, so the loop itself must not allocate.
    private static void Run(LiveChain chain, float[] voice, float[] output)
    {
        for (int start = 0; start + output.Length <= voice.Length; start += output.Length)
        {
            chain.OnBuffer(voice.AsSpan(start, output.Length));
            chain.Render(output);
        }
    }

    // The first buffer from secondsIn on whose decision strikes; the buffer after it must not, so only a latched strike can explain new tones.
    private static int StrikeAfter(AppSettings settings, float[] voice, double secondsIn)
    {
        Accompanist reference = new(settings);
        int size = settings.BufferSize;
        bool[] strikes = TestSignal.InBuffers(voice, size, (all, offset) => reference.Decide(reference.Analyze(all.AsSpan(offset, size))).Strike);
        int first = (int)(secondsIn * settings.SampleRate / size);
        int strike = Array.IndexOf(strikes, true, first);
        Assert.True(strike > first && !strikes[strike + 1], "The louder note should strike once.");
        return strike;
    }

    private sealed class ThenPull(LiveChain chain, MemoryAudioOutput output) : IAudioInputHandler
    {
        public void OnBuffer(ReadOnlySpan<float> samples)
        {
            chain.OnBuffer(samples);
            output.Pull();
        }
    }
}
