using SolidBackup.Accompaniment;
using SolidBackup.Configuration;
using SolidBackup.Live;
using SolidBackup.Recording;
using SolidBackup.Tests.Accompaniment;
using SolidBackup.Tests.Analysis;
using SolidBackup.Tests.Audio;

namespace SolidBackup.Tests.Recording;

/// <summary>
/// E8-2: the chords in a recording are what the output played. The input's callbacks come in bursts, bunched after
/// each onset, and on another clock than the output's, and the writer wakes every 50 ms; none of that may cut or jump
/// the recorded chords, or let the two channels drift apart.
/// </summary>
public class RecordedChordsTests
{
    private static readonly AppSettings Settings = Chain.Settings();
    private static readonly int BufferSize = Settings.BufferSize;

    // How many output buffers pass between two passes of the writer, as with its 50 ms wait.
    private static readonly int WriterEvery = Settings.SampleRate / 20 / BufferSize;

    // A melody with an onset every 0.4 s, so chords change and strike throughout, over a little deterministic noise,
    // so that every stretch of the recorded input can be found again in the voice.
    private const double NoteSeconds = 0.4;
    private static readonly float[] Voice = Noisy(Chain.Join(
        [.. Enumerable.Range(0, 15).Select(i => Chain.Tone(new[] { 60, 64, 67, 65, 62, 69 }[i % 6], -20, NoteSeconds))]));

    // Long enough for 0.1 % of drift to pass the recorder's tolerance of 10 ms a few times.
    private static readonly float[] LongVoice = Noisy(Chain.Join(Voice, Voice, Voice, Voice));

    // A crossfade between two parts of the same signal stays within this many times its own largest step.
    private const double StepBound = 1.5;

    // E6-1's bound on how far the two channels may drift apart (SessionRecorderTests: within 12 buffers).
    private const int InStepBuffers = 12;

    private const float Quantum = 1f / 32767;

    [Theory]
    [InlineData(1, 7)]
    [InlineData(4, 7)]
    [InlineData(4, 81)]
    public void WithoutDrift_TheRecordedChordsAreWhatTheOutputPlayed(int maxBurst, int seed)
    {
        Recorded recorded = Record(Voice, inputSpeed: 1.0, maxBurst, seed);

        Assert.Equal(0, recorded.Corrections);
        float[] chords = TestWav.Pcm16Channel(recorded.File, 1);
        int start = Start(recorded.Played, chords);
        int differs = Enumerable.Range(0, chords.Length).FirstOrDefault(i => Math.Abs(chords[i] - recorded.Played[start + i]) > Quantum, -1);
        Assert.Equal(-1, differs);
    }

    [Theory]
    [InlineData(1.001, 81)]
    [InlineData(0.999, 81)]
    public void WithDrift_TheRecordedChordsOnlyEverCrossfade_AndStayInStepWithTheInput(double inputSpeed, int seed)
    {
        Recorded recorded = Record(LongVoice, inputSpeed, maxBurst: 4, seed);

        Assert.True(recorded.Corrections > 0);
        float[] chords = TestWav.Pcm16Channel(recorded.File, 1);
        float[] voice = TestWav.Pcm16Channel(recorded.File, 0);
        int start = Start(recorded.Played, chords);
        AssertStepsWithinTheOutputsOwn(chords, recorded.Played, start);
        AssertInStep(voice, chords, recorded.Played, start, inputSpeed);
    }

    private static float[] Noisy(float[] samples)
    {
        Random random = new(82);
        return [.. samples.Select(sample => sample + (float)((random.NextDouble() - 0.5) * 0.004))];
    }

    // In each stretch of 10 ms, the recorded chords step no further than 1.5 × the largest step the output played
    // over the same stretch (widened by how far the drops may have moved the recording against the output).
    private static void AssertStepsWithinTheOutputsOwn(float[] chords, float[] played, int start)
    {
        int stretch = Settings.SampleRate / 100;
        int moved = InStepBuffers * BufferSize;
        for (int from = 0; from + stretch <= chords.Length; from += stretch)
        {
            int playedFrom = Math.Max(0, start + from - moved);
            int playedTo = Math.Min(played.Length, start + from + stretch + (4 * moved));
            double own = TestSignal.LargestStep(played, playedFrom, playedTo);
            double recordedStep = TestSignal.LargestStep(chords, from, from + stretch);
            Assert.True(recordedStep <= (StepBound * own) + Quantum, $"At recorded frame {from}: a step of {recordedStep} where the output's own was {own}.");
        }
    }

    // Every half second, which input sample and which output sample are recorded side by side: on the output's clock
    // the two stay within E6-1's bound of each other.
    private static void AssertInStep(float[] voice, float[] chords, float[] played, int start, double inputSpeed)
    {
        const int Match = 32;
        double? first = null;
        for (int frame = Settings.SampleRate; frame + Match < chords.Length; frame += Settings.SampleRate / 2)
        {
            int input = Find(LongVoice, voice, frame, Match, near: (int)(frame * inputSpeed), range: 4 * Settings.SampleRate);
            int output = Find(played, chords, frame, Match, near: start + frame, range: InStepBuffers * BufferSize * 4);
            if (input < 0 || output < 0)
            {
                // Within a crossfade the recorded samples are a mix and match neither side.
                continue;
            }

            double lag = output - (input / inputSpeed);
            first ??= lag;
            Assert.True(Math.Abs(lag - first.Value) <= InStepBuffers * BufferSize, $"At recorded frame {frame} the channels are {lag - first.Value:0} samples further apart.");
        }

        Assert.NotNull(first);
    }

    // Where in source the recorded samples from frame on are found, searching outwards from near; −1 if nowhere.
    private static int Find(float[] source, float[] recorded, int frame, int length, int near, int range)
    {
        for (int distance = 0; distance <= range; distance++)
        {
            foreach (int at in new[] { near - distance, near + distance })
            {
                if (at >= 0 && at + length <= source.Length
                    && Enumerable.Range(0, length).All(i => Math.Abs(source[at + i] - recorded[frame + i]) <= Quantum))
                {
                    return at;
                }
            }
        }

        return -1;
    }

    // Runs the live chain with a recorder on an irregular schedule, with a burst after every onset.
    private static Recorded Record(float[] voice, double inputSpeed, int maxBurst, int seed)
    {
        KeptStream stream = new();
        SessionRecorder recorder = new(stream, Settings.SampleRate, BufferSize, background: false);
        LiveChain chain = new(new Accompanist(Settings), recorder);
        int outputBuffers = (int)(voice.Length / inputSpeed / BufferSize) - (2 * maxBurst);
        int noteBuffers = (int)(NoteSeconds * Settings.SampleRate / BufferSize);
        HashSet<int> onsets = [.. Enumerable.Range(0, voice.Length / BufferSize / noteBuffers).Select(note => note * noteBuffers)];
        List<float> played = [];
        float[] buffer = new float[BufferSize];
        int read = 0;
        int outputs = 0;
        foreach (bool input in CallbackSchedule.Irregular(outputBuffers, inputSpeed, maxBurst, seed, burstsFrom: onsets))
        {
            if (input)
            {
                if (read + BufferSize <= voice.Length)
                {
                    chain.OnBuffer(voice.AsSpan(read, BufferSize));
                    read += BufferSize;
                }

                continue;
            }

            chain.Render(buffer);
            played.AddRange(buffer);
            if (++outputs % WriterEvery == 0)
            {
                recorder.Drain();
            }
        }

        recorder.Stop();
        return new Recorded([.. played], stream.Contents, recorder.DriftCorrections);
    }

    // The recording starts with the first output buffer after the input's first: where that is in what was played.
    private static int Start(float[] played, float[] chords)
    {
        int firstSound = Array.FindIndex(chords, sample => sample != 0);
        return Enumerable.Range(0, 8).Select(buffers => buffers * BufferSize)
            .First(offset => Enumerable.Range(firstSound, 64).All(i => Math.Abs(chords[i] - played[offset + i]) <= Quantum));
    }

    private sealed record Recorded(float[] Played, byte[] File, long Corrections);
}
