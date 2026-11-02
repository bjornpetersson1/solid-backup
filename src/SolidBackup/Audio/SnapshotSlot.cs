namespace SolidBackup.Audio;

/// <summary>
/// Lock-free hand-over of the latest value from one writer thread to one reader thread (a seqlock). The writer never
/// waits; a read that overlaps a write fails instead of returning a torn value, and the reader keeps its last one.
/// Neither side allocates.
/// </summary>
public sealed class SnapshotSlot<T>
    where T : struct
{
    private T _value;
    private long _sequence;

    public void Publish(in T value)
    {
        // Odd while writing; the full fences keep the value's stores between the two increments.
        Interlocked.Increment(ref _sequence);
        _value = value;
        Interlocked.Increment(ref _sequence);
    }

    /// <summary>The latest value, or false when none has been published or a write overlapped the read.</summary>
    public bool TryRead(out T value)
    {
        long before = Volatile.Read(ref _sequence);
        value = _value;
        Interlocked.MemoryBarrier();
        return before > 0 && (before & 1) == 0 && Volatile.Read(ref _sequence) == before;
    }
}
