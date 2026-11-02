using SolidBackup.Accompaniment;
using SolidBackup.Synthesis;
using SolidBackup.Tests.Synthesis;

namespace SolidBackup.Tests.Accompaniment;

public class HeldPatternTests
{
    private const int BufferSize = 64;

    // Organ, whose short attack is over before the strength is compared.
    private readonly Synthesizer _synthesizer = new(Sound.Organ, 48_000);
    private readonly HeldPattern _pattern = new();
    private readonly float[] _buffer = new float[BufferSize];

    [Fact]
    public void Strike_StartsEveryToneAnewEvenTheSharedOnes()
    {
        Play(Frame("C"));

        Play(Frame("C") with { Strike = true });

        Assert.Equal(6, _synthesizer.Starts);
        Assert.Equal(3, _synthesizer.HeldVoices);
        Assert.Equal(2, _synthesizer.Sounding(48));
    }

    [Fact]
    public void StrikeOnTheFirstBuffer_StartsTheChordOnce()
    {
        Play(Frame("C") with { Strike = true });

        Assert.Equal(3, _synthesizer.Starts);
    }

    [Fact]
    public void ShapeChange_OnlyAddsOrReleasesTones()
    {
        Play(Frame("C") with { Shape = ChordShape.Sparse });

        Play(Frame("C") with { Shape = ChordShape.Full });

        Assert.Equal(4, _synthesizer.Starts);
        Assert.Equal(1, _synthesizer.Held(36));
        Assert.Equal(1, _synthesizer.Held(52));
    }

    [Fact]
    public void ChordChange_KeepsSharedTonesAndLeadsTheVoicing()
    {
        Play(Frame("C"));

        Play(Frame("Am"));

        Assert.Equal(4, _synthesizer.Starts);
        Assert.Equal(1, _synthesizer.Held(57));
        Assert.Equal(0, _synthesizer.Held(55));
    }

    [Fact]
    public void Silence_ReleasesTheChordAndTheNextStartsInCloseVoicing()
    {
        Play(Frame("C"));
        Play(Frame("C") with { Sounding = false });
        Assert.Equal(0, _synthesizer.HeldVoices);

        Play(Frame("Am"));

        Assert.Equal(1, _synthesizer.Held(64));
    }

    [Fact]
    public void Strength_IsPassedOnOnlyWhenItMovesByTheStep()
    {
        Play(Frame("C") with { Strength = 0.5 });
        long startsBefore = _synthesizer.Starts;

        Play(Frame("C") with { Strength = 0.5 * Math.Pow(10, 0.05 / 20) });
        float[] small = Render(4800);
        Play(Frame("C") with { Strength = 0.25 });
        float[] large = Render(4800);

        Assert.Equal(startsBefore, _synthesizer.Starts);
        Assert.True(large[^480..].Max(Math.Abs) < small[^480..].Max(Math.Abs) * 0.6);
    }

    private static AccompanimentFrame Frame(string symbol)
    {
        return AccompanimentFrame.Neutral(TestChords.Parse(symbol), sounding: true);
    }

    private void Play(AccompanimentFrame frame)
    {
        _pattern.Play(default, frame, _synthesizer);
        _synthesizer.Render(_buffer);
    }

    private float[] Render(int length)
    {
        float[] samples = new float[length];
        for (int start = 0; start < length; start += BufferSize)
        {
            _synthesizer.Render(samples.AsSpan(start, BufferSize));
        }

        return samples;
    }
}
