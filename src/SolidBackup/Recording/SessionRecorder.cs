using System.Numerics;

using SolidBackup.Audio;

namespace SolidBackup.Recording;

/// <summary>
/// Records a session's input (left) and output (right) to a stereo WAV file. Each side has a ring allocated up front,
/// fed from its own audio thread (<see cref="OnInput"/>, <see cref="OnOutput"/>) or from one (<see cref="OnBuffer"/>);
/// a background thread interleaves the two and writes them to the file. When the writer has fallen so far behind
/// that a ring is full, that side's buffer is dropped and counted as an overrun: the audio never waits for the disk.
/// The side then writes silence in the dropped buffer's place as soon as there is room, so the two channels stay in
/// step. Two devices run on two clocks, so the writer also drops frames from a side that stays ahead (as
/// <see cref="HandOverBuffer"/> does for pass-through). Stopping writes what is left and finishes the file.
/// </summary>
public sealed class SessionRecorder : IAudioTap, IDisposable
{
    /// <summary>The least audio each ring holds, so a slow disk has this long to catch up.</summary>
    public const double RingSeconds = 2;

    // The most frames interleaved and written at once; more waiting is written in several pieces.
    private const int WriteFrames = 8192;

    // How long one side must stay ahead of the other before the writer drops the lead; shorter leads are input bursts.
    private const double DriftWindowSeconds = 0.5;

    // Long enough to write in large pieces, far shorter than the ring. The audio thread never signals the writer,
    // since setting an event could make a system call.
    private static readonly TimeSpan IdleWait = TimeSpan.FromMilliseconds(50);

    private readonly StereoWavWriter _writer;
    private readonly Ring _input;
    private readonly Ring _output;
    private readonly float[] _interleaved;
    private readonly int _sampleRate;
    private readonly int _bufferSize;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stopping = new();
    private readonly long _driftWindowFrames;
    private long _windowStart;
    private long _windowMinimumLead = long.MaxValue;
    private long _windowMaximumLead = long.MinValue;
    private Exception? _failure;
    private bool _stopped;

    /// <param name="destination">Where the WAV file goes; writable and seekable, closed when the recorder stops.</param>
    public SessionRecorder(Stream destination, int sampleRate, int bufferSize)
    {
        _sampleRate = sampleRate;
        _bufferSize = bufferSize;
        int ringLength = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(sampleRate * RingSeconds, bufferSize * 8));
        _input = new Ring(ringLength);
        _output = new Ring(ringLength);
        _interleaved = new float[WriteFrames * 2];
        _driftWindowFrames = (long)(sampleRate * DriftWindowSeconds);
        _writer = new StereoWavWriter(destination, sampleRate, _interleaved.Length);
        _thread = new Thread(WriteUntilStopped) { IsBackground = true, Name = "Recording writer" };
        _thread.Start();
    }

    /// <summary>
    /// Buffers dropped from the recording because the writer had fallen behind: the most either side dropped, so a
    /// pair dropped together (<see cref="OnBuffer"/>) counts once.
    /// </summary>
    public long Overruns => Math.Max(_input.Overruns, _output.Overruns);

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
        _thread.Join();
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

    // Drain, sleep until there is more or the recording stops, and drain once more at the end.
    private void WriteUntilStopped()
    {
        try
        {
            do
            {
                CorrectDrift();
                WriteAvailable();
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

    // A side that has stayed more than a buffer ahead of the other for a whole window runs on a faster clock; its
    // lead beyond one buffer is dropped, so the channels never drift apart by more than that.
    private void CorrectDrift()
    {
        long lead = (long)_input.Available - _output.Available;
        _windowMinimumLead = Math.Min(_windowMinimumLead, lead);
        _windowMaximumLead = Math.Max(_windowMaximumLead, lead);
        if (_input.Written - _windowStart < _driftWindowFrames)
        {
            return;
        }

        if (_windowMinimumLead > _bufferSize)
        {
            _input.Skip((int)(_windowMinimumLead - _bufferSize));
        }
        else if (_windowMaximumLead < -_bufferSize)
        {
            _output.Skip((int)(-_windowMaximumLead - _bufferSize));
        }

        _windowStart = _input.Written;
        _windowMinimumLead = long.MaxValue;
        _windowMaximumLead = long.MinValue;
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

    // One side's samples, from its one audio thread to the writer thread.
    private sealed class Ring(int length)
    {
        private readonly float[] _samples = new float[length];
        private readonly int _mask = length - 1;
        private long _written;
        private long _read;
        private long _overruns;

        // The audio thread's own: frames dropped that still need silence in their place.
        private long _silenceOwed;

        public long Overruns => Interlocked.Read(ref _overruns);

        // Writer thread: the frames written so far, and those waiting.
        public long Written => Volatile.Read(ref _written);

        public int Available => (int)(Volatile.Read(ref _written) - _read);

        /// <summary>
        /// Audio thread: first as much of the owed silence as fits, then the buffer if there is room for all of it;
        /// a buffer that does not fit is dropped and owed as silence.
        /// </summary>
        public void Write(ReadOnlySpan<float> samples)
        {
            long writeAt = _written;
            long room = _samples.Length - (writeAt - Volatile.Read(ref _read));
            int silence = (int)Math.Min(_silenceOwed, room);
            if (silence > 0)
            {
                Copy(writeAt, default, silence);
                writeAt += silence;
                room -= silence;
                _silenceOwed -= silence;
            }

            if (_silenceOwed == 0 && samples.Length <= room)
            {
                Copy(writeAt, samples, samples.Length);
                writeAt += samples.Length;
            }
            else
            {
                Interlocked.Increment(ref _overruns);
                _silenceOwed += samples.Length;
            }

            Volatile.Write(ref _written, writeAt);
        }

        /// <summary>Writer thread: copies <paramref name="frames"/> frames into every second slot of <paramref name="interleaved"/> from <paramref name="channel"/>.</summary>
        public void ReadInto(float[] interleaved, int channel, int frames)
        {
            long readAt = _read;
            for (int frame = 0; frame < frames; frame++, readAt++)
            {
                interleaved[(frame * 2) + channel] = _samples[readAt & _mask];
            }

            Volatile.Write(ref _read, readAt);
        }

        /// <summary>Writer thread: drops up to <paramref name="frames"/> waiting frames.</summary>
        public void Skip(int frames)
        {
            Volatile.Write(ref _read, _read + Math.Min(frames, Available));
        }

        // Copies the samples, or silence for an empty span, into the ring at a write position.
        private void Copy(long writeAt, ReadOnlySpan<float> samples, int count)
        {
            int start = (int)(writeAt & _mask);
            int first = Math.Min(count, _samples.Length - start);
            if (samples.IsEmpty)
            {
                _samples.AsSpan(start, first).Clear();
                _samples.AsSpan(0, count - first).Clear();
                return;
            }

            samples[..first].CopyTo(_samples.AsSpan(start));
            samples[first..count].CopyTo(_samples);
        }
    }
}
