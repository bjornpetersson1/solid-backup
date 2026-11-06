using SolidBackup.Analysis;
using SolidBackup.Audio;

namespace SolidBackup.Recording;

/// <summary>
/// Records a session's input (left) and output (right) to a stereo WAV file. Each side has a ring allocated up front,
/// fed from its own audio thread (<see cref="OnInput"/>, <see cref="OnOutput"/>) or from one (<see cref="OnBuffer"/>);
/// a background thread interleaves the two and writes them to the file. When the writer has fallen so far behind
/// that a ring is full, that side's buffer is dropped and counted as an overrun: the audio never waits for the disk.
/// The side then writes silence in the dropped buffer's place as soon as there is room, so the two channels stay in
/// step. Two devices run on two clocks, so the writer also drops frames from a side that stays well ahead, with a
/// crossfade (E8-2): a hard skip would click in the recording. Stopping writes what is left and finishes the file.
/// </summary>
public sealed class SessionRecorder : IAudioTap, IDisposable
{
    /// <summary>The least audio each ring holds, so a slow disk has this long to catch up.</summary>
    public const double RingSeconds = 2;

    // The most frames interleaved and written at once; more waiting is written in several pieces.
    private const int WriteFrames = 8192;

    // How long one side must stay ahead of the other before the writer drops the lead; shorter leads are bursts.
    private const double DriftWindowMs = 500;

    // How far ahead one side may stay before its lead is dropped: beyond the bursts in which callbacks arrive (E8-2),
    // so only a clock that runs faster leads to a correction.
    private const double DriftToleranceMs = 10;

    // Long enough to write in large pieces, far shorter than the ring. The audio thread never signals the writer,
    // since setting an event could make a system call.
    private static readonly TimeSpan IdleWait = TimeSpan.FromMilliseconds(50);

    private readonly StereoWavWriter _writer;
    private readonly Ring _input;
    private readonly Ring _output;
    private readonly float[] _interleaved;
    private readonly int _sampleRate;
    private readonly int _bufferSize;
    private readonly Thread? _thread;
    private readonly ManualResetEventSlim _stopping = new();
    private readonly long _driftTolerance;

    // The least lead of the input over a window, and the least lead of the output.
    private readonly FillWindow _inputLead;
    private readonly FillWindow _outputLead;
    private long _measuredUpTo;
    private long _driftCorrections;
    private Exception? _failure;
    private bool _stopped;

    /// <param name="destination">Where the WAV file goes; writable and seekable, closed when the recorder stops.</param>
    /// <param name="background">
    /// Write on a background thread; without it the caller writes with <see cref="Drain"/>, so a test decides when the
    /// writer runs.
    /// </param>
    public SessionRecorder(Stream destination, int sampleRate, int bufferSize, bool background = true)
    {
        _sampleRate = sampleRate;
        _bufferSize = bufferSize;
        int ringLength = (int)Math.Max(sampleRate * RingSeconds, bufferSize * 8);
        int crossfade = Crossfade.Length(sampleRate);
        _input = new Ring(ringLength, crossfade);
        _output = new Ring(ringLength, crossfade);
        _interleaved = new float[WriteFrames * 2];
        int driftWindow = (int)Duration.Samples(DriftWindowMs, sampleRate);
        _inputLead = new FillWindow(driftWindow);
        _outputLead = new FillWindow(driftWindow);
        _driftTolerance = Math.Max(bufferSize, Duration.Samples(DriftToleranceMs, sampleRate));
        _writer = new StereoWavWriter(destination, sampleRate, _interleaved.Length);
        if (background)
        {
            _thread = new Thread(WriteUntilStopped) { IsBackground = true, Name = "Recording writer" };
            _thread.Start();
        }
    }

    /// <summary>
    /// Buffers dropped from the recording because the writer had fallen behind: the most either side dropped, so a
    /// pair dropped together (<see cref="OnBuffer"/>) counts once.
    /// </summary>
    public long Overruns => Math.Max(_input.Overruns, _output.Overruns);

    /// <summary>Crossfaded drops that kept the two channels in step.</summary>
    public long DriftCorrections => Interlocked.Read(ref _driftCorrections);

    /// <summary>True when the recording reached the WAV format's size limit (about 6 hours) and the rest was not saved.</summary>
    public bool IsFull => _writer.IsFull;

    /// <summary>The recorded length so far, in seconds.</summary>
    public double Seconds => _writer.Frames / (double)_sampleRate;

    /// <summary>Called on the one audio thread that sees both: records the buffer pair.</summary>
    public void OnBuffer(ReadOnlySpan<float> input, ReadOnlySpan<float> output)
    {
        int frames = Math.Min(input.Length, output.Length);
        OnInput(input[..frames]);
        OnOutput(output[..frames]);
    }

    /// <summary>Called on the input's audio thread: copies the buffer into the input ring, or drops it when the ring is full.</summary>
    public void OnInput(ReadOnlySpan<float> input)
    {
        _input.Write(input);
    }

    /// <summary>Called on the output's audio thread: copies the buffer into the output ring, or drops it when the ring is full.</summary>
    public void OnOutput(ReadOnlySpan<float> output)
    {
        _output.Write(output);
    }

    /// <summary>Writes what is left and finishes the file; calling it again does nothing.</summary>
    /// <exception cref="RecordingFailedException">Writing the file failed.</exception>
    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _stopping.Set();
        if (_thread is null)
        {
            WriteRemaining();
        }
        else
        {
            _thread.Join();
        }

        _stopping.Dispose();
        try
        {
            _writer.Dispose();
        }
        catch (Exception exception)
        {
            // Whatever the destination threw, the recording failed; Stop reports it as such.
            _failure ??= exception;
        }

        if (_failure is { } failure)
        {
            throw new RecordingFailedException(failure);
        }
    }

    /// <summary>Stops like <see cref="Stop"/> but never throws, so it cannot hide the exception that is unwinding; call <see cref="Stop"/> to learn whether the file was written.</summary>
    public void Dispose()
    {
        try
        {
            Stop();
        }
        catch (RecordingFailedException)
        {
        }
    }

    /// <summary>
    /// One pass of the writer, for a recorder made without a background thread: keeps the channels in step and writes
    /// what both sides have.
    /// </summary>
    /// <exception cref="InvalidOperationException">The recorder writes on its own thread, or has stopped.</exception>
    public void Drain()
    {
        if (_thread is not null || _stopped)
        {
            throw new InvalidOperationException("Drain is only for a recorder without a background thread that has not stopped.");
        }

        Pass();
    }

    private void Pass()
    {
        CorrectDrift();
        WriteAvailable();
    }

    // Without a background thread: what is left, failing like the thread would.
    private void WriteRemaining()
    {
        try
        {
            WriteAvailable();
        }
        catch (Exception exception)
        {
            _failure = exception;
        }
    }

    // Drain, sleep until there is more or the recording stops, and drain once more at the end.
    private void WriteUntilStopped()
    {
        try
        {
            do
            {
                Pass();
            }
            while (!_stopping.Wait(IdleWait));

            WriteAvailable();
        }
        catch (Exception exception)
        {
            // A background thread must not end the app; Stop reports the failure.
            _failure = exception;
        }
    }

    // A side that has stayed more than the tolerance ahead of the other for a whole window runs on a faster clock; its
    // lead beyond one buffer is dropped with a crossfade, so the channels never drift apart by more than that.
    private void CorrectDrift()
    {
        long written = _input.Written;
        int frames = (int)(written - _measuredUpTo);
        _measuredUpTo = written;
        long lead = (long)_input.Available - _output.Available;

        // Both windows count the same frames, so they end together.
        _outputLead.Add(-lead, frames, out long outputLead);
        if (!_inputLead.Add(lead, frames, out long inputLead))
        {
            return;
        }

        // A drop still crossfading must play out before the next, or its own move would become a jump.
        if (_input.Splicing || _output.Splicing)
        {
            return;
        }

        if (inputLead > _driftTolerance)
        {
            Drop(_input, inputLead - _bufferSize);
        }
        else if (outputLead > _driftTolerance)
        {
            Drop(_output, outputLead - _bufferSize);
        }
    }

    private void Drop(Ring side, long frames)
    {
        if (side.Drop(frames))
        {
            Interlocked.Increment(ref _driftCorrections);
        }
    }

    // Writes the frames both sides have, as left/right pairs, in pieces of at most WriteFrames.
    private void WriteAvailable()
    {
        int frames;
        while ((frames = Math.Min(WriteFrames, Math.Min(_input.Available, _output.Available))) > 0)
        {
            _input.ReadInto(_interleaved, 0, frames);
            _output.ReadInto(_interleaved, 1, frames);
            _writer.Write(_interleaved.AsSpan(0, frames * 2));
        }
    }

    // One side's samples, from its one audio thread to the writer thread, through a splicing ring so the writer can
    // drop a lead without a click.
    private sealed class Ring(int length, int crossfade)
    {
        private readonly SplicingRing _samples = new(length, crossfade);
        private long _overruns;

        // The audio thread's own: frames dropped that still need silence in their place.
        private long _silenceOwed;

        public long Overruns => Interlocked.Read(ref _overruns);

        // Writer thread: the frames written so far, and those waiting.
        public long Written => _samples.Written;

        public int Available => (int)_samples.Waiting(_samples.Written);

        public bool Splicing => _samples.Splicing;

        /// <summary>
        /// Audio thread: first as much of the owed silence as fits, then the buffer if there is room for all of it;
        /// a buffer that does not fit is dropped and owed as silence.
        /// </summary>
        public void Write(ReadOnlySpan<float> samples)
        {
            long room = _samples.Room;
            int silence = (int)Math.Min(_silenceOwed, room);
            if (silence > 0)
            {
                _samples.WriteSilence(silence);
                _silenceOwed -= silence;
                room -= silence;
            }

            if (_silenceOwed == 0 && samples.Length <= room)
            {
                _samples.Write(samples);
                return;
            }

            Interlocked.Increment(ref _overruns);
            _silenceOwed += samples.Length;
        }

        /// <summary>Writer thread: copies <paramref name="frames"/> frames into every second slot of <paramref name="interleaved"/> from <paramref name="channel"/>.</summary>
        public void ReadInto(float[] interleaved, int channel, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                interleaved[(frame * 2) + channel] = _samples.Next();
            }

            _samples.Publish();
        }

        /// <summary>Writer thread: drops up to <paramref name="frames"/> waiting frames with a crossfade; false if none.</summary>
        public bool Drop(long frames)
        {
            return _samples.Splice(frames, _samples.Written);
        }
    }
}
