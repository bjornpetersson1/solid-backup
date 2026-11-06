using System.Numerics;

namespace SolidBackup.Audio;

/// <summary>
/// A ring from one producer thread to one consumer thread, allocated up front, that the consumer reads sample by
/// sample and can move without a click (E8-1, E8-2): <see cref="Splice"/> drops or repeats a stretch with a
/// <see cref="Crossfade"/>. A drop fades out of what would have come next; a repeat fades out of what was just read,
/// backwards, so it needs no input that has not arrived. No locks: each side owns one counter.
/// </summary>
public sealed class SplicingRing
{
    private readonly float[] _samples;
    private readonly int _mask;

    // The samples as last read, after any crossfade: a repeat, and a caller's fade-out, play them backwards.
    private readonly float[] _read;
    private readonly int _readMask;
    private readonly int _crossfadeLength;

    // The producer writes _written; the consumer owns the rest and hands _position over as _published once per buffer.
    private long _written;
    private long _published;
    private long _position;
    private long _readCount;
    private bool _spliceBack;
    private long _spliceFrom;
    private int _spliceDone;

    /// <param name="least">The least number of samples the ring holds; rounded up to a power of two.</param>
    public SplicingRing(int least, int crossfadeLength)
    {
        _samples = new float[(int)BitOperations.RoundUpToPowerOf2((uint)least)];
        _mask = _samples.Length - 1;
        _crossfadeLength = crossfadeLength;
        _read = new float[(int)BitOperations.RoundUpToPowerOf2((uint)(2 * crossfadeLength))];
        _readMask = _read.Length - 1;
        _spliceDone = crossfadeLength;
    }

    public int Capacity => _samples.Length;

    public int CrossfadeLength => _crossfadeLength;

    /// <summary>Consumer: the samples written so far, read once per buffer.</summary>
    public long Written => Volatile.Read(ref _written);

    /// <summary>Consumer: whether a splice is still crossfading.</summary>
    public bool Splicing => _spliceDone < _crossfadeLength;

    /// <summary>
    /// Producer: appends the samples. When the consumer has fallen a whole ring behind, the oldest unread samples are
    /// overwritten and false is returned.
    /// </summary>
    public bool Write(ReadOnlySpan<float> samples)
    {
        long written = _written;
        bool kept = written + samples.Length - Volatile.Read(ref _published) <= _samples.Length;
        int start = (int)(written & _mask);
        int first = Math.Min(samples.Length, _samples.Length - start);
        samples[..first].CopyTo(_samples.AsSpan(start));
        samples[first..].CopyTo(_samples);
        Volatile.Write(ref _written, written + samples.Length);
        return kept;
    }

    /// <summary>Consumer: the samples waiting, given <see cref="Written"/>.</summary>
    public long Waiting(long written)
    {
        return written - _position;
    }

    /// <summary>Consumer: moves the read position so <paramref name="waiting"/> samples wait, without a crossfade.</summary>
    public void Leave(long written, long waiting)
    {
        _position = written - waiting;
        _spliceDone = _crossfadeLength;
    }

    /// <summary>
    /// Consumer: moves the read position ahead (a drop) or back (a repeat), crossfading from where it was. A drop
    /// crossfades out of the samples it skips, so it is limited to what waits beyond one crossfade, given
    /// <see cref="Written"/>; with less waiting there is nothing to drop. False when the position did not move.
    /// </summary>
    public bool Splice(long move, long written)
    {
        move = Math.Min(move, Math.Max(0, Waiting(written) - _crossfadeLength));
        if (move == 0)
        {
            return false;
        }

        _spliceBack = move < 0;
        _spliceFrom = _spliceBack ? _readCount - 1 : _position;
        _position += move;
        _spliceDone = 0;
        return true;
    }

    /// <summary>Consumer: the next sample; call only while <see cref="Waiting"/> is above zero.</summary>
    public float Next()
    {
        float sample = _samples[_position & _mask];
        _position++;
        if (_spliceDone < _crossfadeLength)
        {
            sample = Crossfade.Mix(SpliceSample(), sample, _spliceDone, _crossfadeLength);
            _spliceDone++;
        }

        return sample;
    }

    /// <summary>Consumer: notes a sample as read, after the caller's own fades, so repeats and fade-outs continue from it.</summary>
    public void Played(float sample)
    {
        _read[_readCount & _readMask] = sample;
        _readCount++;
    }

    /// <summary>Consumer: the sample played <paramref name="age"/> samples ago (1 for the last), up to <see cref="CrossfadeLength"/>.</summary>
    public float PlayedAgo(int age)
    {
        return _read[(_readCount - age) & _readMask];
    }

    /// <summary>Consumer: publishes the read position to the producer, once per buffer.</summary>
    public void Publish()
    {
        Volatile.Write(ref _published, _position);
    }

    // The side a splice fades out of: what was played, backwards, or the samples that would have come next.
    private float SpliceSample()
    {
        float sample = _spliceBack ? _read[_spliceFrom & _readMask] : _samples[_spliceFrom & _mask];
        _spliceFrom += _spliceBack ? -1 : 1;
        return sample;
    }
}
