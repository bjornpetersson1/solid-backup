using System.Numerics;

using SolidBackup.Audio;

namespace SolidBackup.Recording;

/// <summary>
/// Records a session's input (left) and output (right) to a stereo WAV file. The audio thread only copies each
/// buffer into a ring allocated up front; a background thread writes from the ring to the file. When the writer has
/// fallen so far behind that the ring is full, the audio thread drops that buffer and counts an overrun: it never
/// waits for the disk. Stopping writes what is left and finishes the file.
/// </summary>
public sealed class SessionRecorder : IAudioTap, IDisposable
{
    /// <summary>The least audio the ring holds, so a slow disk has this long to catch up.</summary>
    public const double RingSeconds = 2;

    // Long enough to write in large pieces, far shorter than the ring. The audio thread never signals the writer,
    // since setting an event could make a system call.
    private static readonly TimeSpan IdleWait = TimeSpan.FromMilliseconds(50);

    private readonly StereoWavWriter _writer;
    private readonly float[] _ring;
    private readonly int _mask;
    private readonly int _sampleRate;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stopping = new();
    private long _written;
    private long _read;
    private long _overruns;
    private Exception? _failure;
    private bool _stopped;

    /// <param name="destination">Where the WAV file goes; writable and seekable, closed when the recorder stops.</param>
    public SessionRecorder(Stream destination, int sampleRate, int bufferSize)
    {
        _sampleRate = sampleRate;
        _ring = new float[BitOperations.RoundUpToPowerOf2((uint)Math.Max(sampleRate * RingSeconds * 2, bufferSize * 2 * 8))];
        _mask = _ring.Length - 1;
        _writer = new StereoWavWriter(destination, sampleRate, _ring.Length);
        _thread = new Thread(WriteUntilStopped) { IsBackground = true, Name = "Recording writer" };
        _thread.Start();
    }

    /// <summary>Buffers dropped from the recording because the writer had fallen behind.</summary>
    public long Overruns => Interlocked.Read(ref _overruns);

    /// <summary>True when the recording reached the WAV format's size limit (about 6 hours) and the rest was not saved.</summary>
    public bool IsFull => _writer.IsFull;

    /// <summary>The recorded length so far, in seconds.</summary>
    public double Seconds => _writer.Frames / (double)_sampleRate;

    /// <summary>Called on the audio thread: copies the buffer pair into the ring, or drops it when the ring is full.</summary>
    public void OnBuffer(ReadOnlySpan<float> input, ReadOnlySpan<float> output)
    {
        int frames = Math.Min(input.Length, output.Length);
        long writeAt = _written;
        if (writeAt + (frames * 2) - Volatile.Read(ref _read) > _ring.Length)
        {
            Interlocked.Increment(ref _overruns);
            return;
        }

        for (int frame = 0; frame < frames; frame++)
        {
            _ring[(writeAt + (frame * 2)) & _mask] = input[frame];
            _ring[(writeAt + (frame * 2) + 1) & _mask] = output[frame];
        }

        Volatile.Write(ref _written, writeAt + (frames * 2));
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

    // Writes everything in the ring, as the one or two pieces it lies in.
    private void WriteAvailable()
    {
        long readAt = _read;
        int available = (int)(Volatile.Read(ref _written) - readAt);
        if (available == 0)
        {
            return;
        }

        int start = (int)(readAt & _mask);
        int first = Math.Min(available, _ring.Length - start);
        _writer.Write(_ring.AsSpan(start, first));
        if (available > first)
        {
            _writer.Write(_ring.AsSpan(0, available - first));
        }

        Volatile.Write(ref _read, readAt + available);
    }
}
