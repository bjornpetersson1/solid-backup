using SolidBackup.Analysis;
using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Synthesis;

namespace SolidBackup.Live;

/// <summary>
/// Monitoring (E7-3, E8-1): the microphone in the headphones with the chords. The input's thread writes each buffer
/// into a <see cref="SplicingRing"/>, and the output's thread adds it to the chords at <c>MonitorLevelPercent</c>, kept
/// within full scale by the synthesizer's soft limit.
/// <para>
/// The two callbacks arrive irregularly, in bursts, and on two clocks, so the ring is a small jitter buffer: playing
/// starts with a fade-in once a buffer and a margin wait, and a <see cref="FillWindow"/> watches the least that waited
/// before a read. When that drifts out of its band, a short stretch is repeated or dropped with a crossfade, so the
/// delay stays near a buffer and a margin, within about 5 ms. When input is late, a little of what was played is
/// repeated rather than stopping. Only when nothing at all comes has the input run dry: the monitored signal fades out
/// (what was played, backwards, so it stays continuous) and counts a gap, and fades in again once input waits.
/// Neither side allocates or locks.
/// </para>
/// </summary>
public sealed class InputMonitor
{
    // Room for several buffers and the longest delay, so a late output thread does not overflow it.
    private const int RingBuffers = 8;

    // The band, beyond one buffer, that the least waiting input before a read is kept in: corrections bring it back
    // to the target. The band is wider than the drift of 0.1 % over one window.
    private const double LowMarginMs = 0.29;
    private const double TargetMarginMs = 0.46;
    private const double HighMarginMs = 0.75;

    // The longest the monitored input should wait (E8-1): 5 ms, or three buffers when they are longer.
    private const double MaxDelayMs = 5;

    // Long enough to see a whole cycle of bursts, short enough that drift moves the fill little within one.
    private const double WindowMs = 250;

    private readonly SplicingRing _ring;
    private readonly FillWindow _window;
    private readonly float _gain;
    private readonly int _bufferSize;
    private readonly long _lowMargin;
    private readonly long _targetMargin;
    private readonly long _highMargin;
    private readonly long _startFill;
    private readonly long _startMost;

    // The input's thread counts overflows, once per stall; the output's thread owns the rest.
    private long _overflows;
    private bool _overflowing;
    private long _gaps;
    private long _corrections;

    private State _state;
    private int _fadeDone;
    private long _repeatedAt = -1;

    public InputMonitor(int bufferSize, int levelPercent, int sampleRate)
    {
        _bufferSize = bufferSize;
        _gain = levelPercent / 100f;
        _lowMargin = Duration.Samples(LowMarginMs, sampleRate);
        _targetMargin = Duration.Samples(TargetMarginMs, sampleRate);
        _highMargin = Duration.Samples(HighMarginMs, sampleRate);
        _startFill = bufferSize + _targetMargin;

        // Started at the peak of a burst, the oldest input would wait too long until the first correction.
        _startMost = Math.Max(Math.Max(Duration.Samples(MaxDelayMs, sampleRate), 3 * bufferSize) - _highMargin, _startFill);
        int crossfade = Crossfade.Length(sampleRate);
        _ring = new SplicingRing((int)Math.Max(bufferSize * RingBuffers, 4 * (bufferSize + _highMargin + crossfade)), crossfade);
        _window = new FillWindow((int)Duration.Samples(WindowMs, sampleRate));
    }

    private enum State
    {
        Waiting,
        Playing,
        FadingOut,
    }

    /// <summary>Times the input ran dry while it was being monitored, so the monitored signal faded out.</summary>
    public long Gaps => Interlocked.Read(ref _gaps);

    /// <summary>Times the ring was full (the output had stalled) and the oldest input was overwritten.</summary>
    public long Overflows => Interlocked.Read(ref _overflows);

    /// <summary>Crossfaded drift corrections: smooth by design, so they are not interruptions.</summary>
    public long Corrections => Interlocked.Read(ref _corrections);

    /// <summary>What the dashboard counts as interruptions: gaps and overflows.</summary>
    public long Interruptions => Gaps + Overflows;

    /// <summary>
    /// Whether live mode monitors: with <c>MonitorOn</c>, and only to an output known to be safe (not the built-in
    /// speakers, where the microphone would hear itself).
    /// </summary>
    public static bool Monitors(AppSettings settings, bool outputSafe)
    {
        return settings.MonitorOn && outputSafe;
    }

    /// <summary>A monitor for the settings, or null when live mode does not monitor (<see cref="Monitors"/>).</summary>
    public static InputMonitor? FromSettings(AppSettings settings, bool outputSafe)
    {
        return Monitors(settings, outputSafe) ? new InputMonitor(settings.BufferSize, settings.MonitorLevelPercent, settings.SampleRate) : null;
    }

    /// <summary>
    /// On the input's thread: the next input buffer. When the output has stalled and the ring is full, the oldest
    /// samples are overwritten and an overflow is counted; the output then resumes with the latest input.
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        bool kept = _ring.Write(samples);
        if (!kept && !_overflowing)
        {
            Interlocked.Increment(ref _overflows);
        }

        _overflowing = !kept;
    }

    /// <summary>
    /// On the output's thread: adds the waiting input to <paramref name="output"/>, which holds the chords; with
    /// <paramref name="fadeOut"/> the input fades from its level to silence over the buffer, so stopping does not click.
    /// </summary>
    public void MixInto(Span<float> output, bool fadeOut = false)
    {
        long written = _ring.Written;
        if (_ring.Waiting(written) > _ring.Capacity)
        {
            // The output stalled and the input overwrote what was to be played (an overflow): fade out what was
            // playing, then start again from the latest input.
            _ring.Leave(written, _startFill);
            _state = _state == State.Playing ? State.FadingOut : State.Waiting;
            _fadeDone = 0;
        }

        long waiting = _ring.Waiting(written);
        if (_state == State.Waiting)
        {
            if (waiting < _startFill)
            {
                return;
            }

            _ring.Leave(written, Math.Min(waiting, _startMost));
            waiting = _ring.Waiting(written);
            Start();
        }

        if (_state == State.Playing)
        {
            Keep(written, waiting, output.Length);
        }

        for (int i = 0; i < output.Length; i++)
        {
            if (!Next(written, out float sample))
            {
                break;
            }

            float gain = fadeOut ? _gain * (1 - ((float)i / output.Length)) : _gain;
            output[i] = (float)Synthesizer.Saturate(output[i] + (gain * sample));
        }

        _ring.Publish();
    }

    // Plays from the read position, fading in.
    private void Start()
    {
        _state = State.Playing;
        _fadeDone = 0;
        _window.Reset();
    }

    // Before a read: repeats a little when the input is late, fades out when it has run dry, and at the end of each
    // window corrects a fill outside the band.
    private void Keep(long written, long waiting, int length)
    {
        if (waiting < length)
        {
            if (written == _repeatedAt)
            {
                // Nothing has come since the last repeat: the input has run dry.
                FadeOut();
                return;
            }

            Correct(waiting - (_bufferSize + _lowMargin));
            _repeatedAt = written;
            return;
        }

        if (_window.Add(waiting, length, out long least) && !_ring.Splicing)
        {
            long spare = least - _bufferSize;
            if (spare < _lowMargin || spare > _highMargin)
            {
                Correct(spare - _targetMargin);
            }
        }
    }

    private void Correct(long move)
    {
        _ring.Splice(move);
        _window.Reset();
        Interlocked.Increment(ref _corrections);
    }

    private void FadeOut()
    {
        _state = State.FadingOut;
        _fadeDone = 0;
        _repeatedAt = -1;
        Interlocked.Increment(ref _gaps);
    }

    // The next monitored sample, or false once there is nothing more to play in this buffer.
    private bool Next(long written, out float sample)
    {
        if (_state == State.Playing && _ring.Waiting(written) <= 0)
        {
            // The input ran dry within the buffer.
            FadeOut();
        }

        int fade = _ring.CrossfadeLength;
        switch (_state)
        {
            case State.Playing:
                sample = _ring.Next();
                if (_fadeDone < fade)
                {
                    sample = Crossfade.Mix(0, sample, _fadeDone++, fade);
                }

                _ring.Played(sample);
                return true;

            case State.FadingOut:
                // What was played, backwards from its last sample, so the signal stays continuous while it fades.
                sample = Crossfade.Mix(_ring.PlayedAgo(_fadeDone + 1), 0, _fadeDone, fade);
                if (++_fadeDone == fade)
                {
                    _state = State.Waiting;
                }

                return true;

            default:
                sample = 0;
                return false;
        }
    }
}
