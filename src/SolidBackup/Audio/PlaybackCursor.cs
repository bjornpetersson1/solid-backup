namespace SolidBackup.Audio;

/// <summary>
/// Where a playback is and what it does next (E8-3), for the output's callback: a pause, a resume and a seek are
/// taken up at the next buffer with a <see cref="Crossfade"/> over it (to or from silence, or from the old place to
/// the new), so none of them clicks. The menu's thread asks with <see cref="Paused"/> and <see cref="Seek"/>; only the
/// callback moves the position. It allocates nothing.
/// </summary>
public sealed class PlaybackCursor
{
    private const int NoSeek = -1;

    private volatile bool _pauseWanted;
    private int _seekTo = NoSeek;
    private int _position;
    private bool _paused;

    /// <summary>
    /// The sample played next, counting a seek not yet taken up, so seeks in a row add up; past the samples' end while
    /// the silence after them plays.
    /// </summary>
    public int Position => Volatile.Read(ref _seekTo) is var seek and not NoSeek ? seek : Volatile.Read(ref _position);

    public bool Paused
    {
        get => _pauseWanted;
        set => _pauseWanted = value;
    }

    /// <summary>Asks to play on from <paramref name="position"/>, clamped to the samples; forward past the end does nothing.</summary>
    public void Seek(int position, int length)
    {
        int target = Math.Clamp(position, 0, length);
        if (target == length && Position >= length)
        {
            return;
        }

        Volatile.Write(ref _seekTo, target);
    }

    /// <summary>
    /// The callback's work: fills <paramref name="interleaved"/> with the next samples on every channel, silence past
    /// their end or while paused.
    /// </summary>
    public void Fill(ReadOnlySpan<float> samples, Span<float> interleaved, int channels)
    {
        int frames = interleaved.Length / channels;
        int seek = Interlocked.Exchange(ref _seekTo, NoSeek);
        bool wasPlaying = !_paused;
        bool playing = !_pauseWanted;
        int position = _position;
        int target = seek == NoSeek ? position : seek;
        for (int frame = 0; frame < frames; frame++)
        {
            float sample = (wasPlaying, playing) switch
            {
                (true, true) when seek != NoSeek => Crossfade.Mix(At(samples, position + frame), At(samples, target + frame), frame, frames),
                (true, true) => At(samples, position + frame),
                (true, false) => Crossfade.Mix(At(samples, position + frame), 0, frame, frames),
                (false, true) => Crossfade.Mix(0, At(samples, target + frame), frame, frames),
                _ => 0,
            };

            for (int channel = 0; channel < channels; channel++)
            {
                interleaved[(frame * channels) + channel] = sample;
            }
        }

        _paused = !playing;
        int next = (wasPlaying, playing) switch
        {
            (true, true) => target + frames,
            (true, false) => seek == NoSeek ? position + frames : target,
            (false, true) => target + frames,
            _ => target,
        };
        Volatile.Write(ref _position, next);
    }

    private static float At(ReadOnlySpan<float> samples, int index)
    {
        return index < samples.Length ? samples[index] : 0;
    }
}
