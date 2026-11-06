using SolidBackup.Audio;

namespace SolidBackup.Tests.Audio;

/// <summary>
/// A playback without a device: each time the player looks whether it has finished, it plays one more piece through
/// the real playback's fill step, so a test sees what would have been played, where seeks went and when it paused.
/// </summary>
public sealed class MemoryPlayback(int piece = 4800) : IAudioPlayback
{
    private readonly List<float> _played = [];
    private readonly float[] _buffer = new float[piece];
    private readonly PlaybackCursor _cursor = new();
    private float[] _samples = [];

    public IReadOnlyList<float> Played => _played;

    /// <summary>Called once the samples have all been played, e.g. to press the keys meant for what comes after.</summary>
    public Action? WhenFinished { get; init; }

    public List<int> Seeks { get; } = [];

    public bool Stopped { get; private set; }

    public bool Disposed { get; private set; }

    public string OutputDeviceName => "Hörlurar";

    public long Underflows => 0;

    public int Position => Math.Min(_cursor.Position, _samples.Length);

    public bool Paused
    {
        get => _cursor.Paused;
        set => _cursor.Paused = value;
    }

    public bool IsFinished
    {
        get
        {
            bool before = _cursor.Position >= _samples.Length;
            PlayPiece();
            bool finished = _cursor.Position >= _samples.Length;
            if (finished && !before)
            {
                WhenFinished?.Invoke();
            }

            return finished;
        }
    }

    public void Start(float[] samples)
    {
        _samples = samples;
    }

    /// <summary>Asks the cursor to seek, and notes where to, as the cursor clamps it.</summary>
    public void Seek(int position)
    {
        _cursor.Seek(position, _samples.Length);
        Seeks.Add(Math.Clamp(position, 0, _samples.Length));
    }

    public void Stop()
    {
        Stopped = true;
    }

    public void Dispose()
    {
        Disposed = true;
    }

    // The player only looks whether it has finished while it is not paused, so each look plays one piece.
    private void PlayPiece()
    {
        bool paused = Paused;
        _cursor.Fill(_samples, _buffer, channels: 1);
        if (!paused)
        {
            _played.AddRange(_buffer);
        }
    }
}
