using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

public sealed class NoteHoldTests
{
    private const int SampleRate = 48_000;
    private const int Buffer = 64;

    // 10 ms at 48 kHz is 480 samples: 8 buffers of 64.
    private const int BuffersToHold = 8;

    [Fact]
    public void SingleStrayEstimateAnOctaveOff_DoesNotChangeTheNote()
    {
        NoteHold hold = new(SampleRate);
        Feed(hold, Note(5700), 20);

        PitchEstimate stray = hold.Process(Note(6900), Buffer);
        PitchEstimate after = hold.Process(Note(5705), Buffer);

        Assert.Equal(5700, stray.MidiCents);
        Assert.Equal(5705, after.MidiCents);
    }

    [Fact]
    public void SingleStrayEstimate_IsHeldBackWithLargeBuffersToo()
    {
        NoteHold hold = new(SampleRate);
        hold.Process(Note(5700), 512);
        hold.Process(Note(5700), 512);

        PitchEstimate stray = hold.Process(Note(6900), 512);
        PitchEstimate second = hold.Process(Note(6900), 512);

        Assert.Equal(5700, stray.MidiCents);
        Assert.Equal(6900, second.MidiCents);
    }

    [Fact]
    public void RealNoteChange_IsReportedOnceItHasHeldFor10Ms()
    {
        NoteHold hold = new(SampleRate);
        Feed(hold, Note(5700), 20);

        PitchEstimate[] outputs = Feed(hold, Note(6000), BuffersToHold);

        Assert.All(outputs[..^1], output => Assert.Equal(5700, output.MidiCents));
        Assert.Equal(6000, outputs[^1].MidiCents);
    }

    [Fact]
    public void NoteStartAndEnd_AlsoWaitForTheHold()
    {
        NoteHold hold = new(SampleRate);

        PitchEstimate[] start = Feed(hold, Note(5700), BuffersToHold);
        PitchEstimate[] end = Feed(hold, PitchEstimate.NoNote(0, 0), BuffersToHold);

        Assert.All(start[..^1], output => Assert.False(output.HasNote));
        Assert.True(start[^1].HasNote);
        Assert.All(end[..^1], output => Assert.True(output.HasNote));
        Assert.False(end[^1].HasNote);
    }

    [Fact]
    public void MovementWithinASemitone_IsPassedOnAtOnce()
    {
        NoteHold hold = new(SampleRate);
        Feed(hold, Note(5700), 20);

        Assert.Equal(5790, hold.Process(Note(5790), Buffer).MidiCents);
        Assert.Equal(5870, hold.Process(Note(5870), Buffer).MidiCents);
    }

    [Fact]
    public void HeldOutput_CarriesTheNewEstimatesTime()
    {
        NoteHold hold = new(SampleRate);
        Feed(hold, Note(5700), 20);

        PitchEstimate output = hold.Process(Note(6900) with { TimeMs = 123 }, Buffer);

        Assert.Equal((5700, 123.0), (output.MidiCents, output.TimeMs));
    }

    private static PitchEstimate Note(double cents)
    {
        return new PitchEstimate(0, true, cents, 0.95);
    }

    private static PitchEstimate[] Feed(NoteHold hold, PitchEstimate estimate, int buffers)
    {
        return Enumerable.Range(0, buffers).Select(_ => hold.Process(estimate, Buffer)).ToArray();
    }
}
