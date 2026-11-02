using SolidBackup.Configuration;
using SolidBackup.Harmony;

namespace SolidBackup.Synthesis;

/// <summary>
/// A polyphonic synthesizer with <see cref="VoiceCount"/> voices of one <see cref="Sound"/>. Tones are
/// started and released, and chords changed, by events at a sample offset from the start of the next
/// <see cref="Render"/> (an offset may lie in a later buffer), so a pattern can place each tone on its own
/// sample. Every event takes effect on its sample, against the voices as they are then. Everything is
/// allocated in the constructor; queueing events and rendering allocate nothing.
/// </summary>
public sealed class Synthesizer
{
    public const int VoiceCount = 16;

    /// <summary>Each voice's share of full scale, so five full-strength tones peak below 0.9 (about −1 dBFS).</summary>
    public const double VoiceGain = 0.15;

    /// <summary>
    /// Where the safety saturation starts. A held chord stays below it; only overlapping releases and attacks at a
    /// fast chord change can pass it, and they are bent smoothly towards 1 instead of clipping.
    /// </summary>
    public const double SaturationStart = 0.9;

    private const int EventCapacity = 256;

    private readonly Voice[] _voices;
    private readonly ToneEvent[] _events = new ToneEvent[EventCapacity];
    private readonly ChordVoicer _voicer = new();
    private readonly int[] _chordNotes = new int[ChordVoicing.MaxTones];
    private readonly int[] _voicing = new int[ChordVoicing.MaxTones];
    private int _eventCount;
    private int _chordNoteCount;
    private long _now;

    public Synthesizer(Sound sound, int sampleRate)
    {
        Preset = SoundPreset.For(sound);
        _voices = new Voice[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            _voices[i] = new Voice(Preset, sampleRate, VoiceGain);
        }
    }

    /// <summary>A synthesizer with validated settings' sound and sample rate.</summary>
    public static Synthesizer FromSettings(AppSettings settings)
    {
        return new Synthesizer(SoundIn(settings), settings.SampleRate);
    }

    /// <summary>The validated settings' sound.</summary>
    /// <exception cref="ArgumentException">The sound is not valid; validate the settings first.</exception>
    public static Sound SoundIn(AppSettings settings)
    {
        return SettingNames.TryParse(settings.Sound, out Sound sound)
            ? sound
            : throw new ArgumentException("The sound is not valid; validate the settings first.", nameof(settings));
    }

    private enum ToneEventKind
    {
        Start,
        Release,
        ReleaseAll,
        Chord,
        Strength,
    }

    public SoundPreset Preset { get; }

    /// <summary>Told about every tone that starts or is released, with its sample since the synthesizer began; for tests and measurement.</summary>
    public IToneListener? Listener { get; set; }

    /// <summary>How many tones have started since the synthesizer began; a kept common tone is no new start.</summary>
    public long Starts { get; private set; }

    /// <summary>True when no voice sounds and no event is waiting.</summary>
    public bool IsSilent => _eventCount == 0 && Array.TrueForAll(_voices, voice => voice.Stage == VoiceStage.Idle);

    /// <summary>How many voices play the note, held or releasing.</summary>
    public int Sounding(int midiNote)
    {
        int count = 0;
        foreach (Voice voice in _voices)
        {
            if (voice.Stage != VoiceStage.Idle && voice.Note == midiNote)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>How many voices hold a tone (not releasing).</summary>
    public int HeldVoices
    {
        get
        {
            int count = 0;
            foreach (Voice voice in _voices)
            {
                if (voice.IsHeld)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>How many voices hold the note (not releasing).</summary>
    public int Held(int midiNote)
    {
        int count = 0;
        foreach (Voice voice in _voices)
        {
            if (voice.IsHeld && voice.Note == midiNote)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Starts a tone with a new attack, even if the same note already sounds.</summary>
    public void StartTone(int midiNote, double strength, int offset = 0)
    {
        Enqueue(new ToneEvent(offset, ToneEventKind.Start, midiNote, strength));
    }

    /// <summary>Releases every held voice playing the note.</summary>
    public void ReleaseTone(int midiNote, int offset = 0)
    {
        Enqueue(new ToneEvent(offset, ToneEventKind.Release, midiNote, 0));
    }

    /// <summary>Releases every held voice, and forgets the chord set by <see cref="SetChord"/>.</summary>
    public void ReleaseAll(int offset = 0)
    {
        Enqueue(new ToneEvent(offset, ToneEventKind.ReleaseAll, 0, 0));
    }

    /// <summary>
    /// Changes to <paramref name="chord"/> (none releases the chord), led from the current chord with
    /// <see cref="ChordVoicing.Lead"/>, so the next chord after none starts in close voicing again. Tones in both
    /// chords keep sounding, tones only in the old chord are released, and tones only in the new chord start
    /// with an attack. A shared tone that has already died away (a sound without sustain) starts again.
    /// </summary>
    public void SetChord(Chord? chord, double strength, int offset = 0)
    {
        Enqueue(new ToneEvent(offset, ToneEventKind.Chord, 0, strength, chord));
    }

    /// <summary>Moves every held tone to a new strength, gliding over a few ms so the change does not click.</summary>
    public void SetStrength(double strength, int offset = 0)
    {
        Enqueue(new ToneEvent(offset, ToneEventKind.Strength, 0, strength));
    }

    /// <summary>Renders the next buffer, applying each event on its sample.</summary>
    public void Render(Span<float> output)
    {
        int next = 0;
        for (int i = 0; i < output.Length; i++)
        {
            while (next < _eventCount && _events[next].Offset <= i)
            {
                Apply(_events[next++]);
            }

            double sum = 0;
            foreach (Voice voice in _voices)
            {
                if (voice.Stage != VoiceStage.Idle)
                {
                    sum += voice.Next();
                }
            }

            output[i] = (float)Saturate(sum);
            _now++;
        }

        // Events for later buffers move to the front, with offsets from the next buffer's start.
        int remaining = _eventCount - next;
        for (int i = 0; i < remaining; i++)
        {
            ToneEvent pending = _events[next + i];
            _events[i] = pending with { Offset = pending.Offset - output.Length };
        }

        _eventCount = remaining;
    }

    private static double Saturate(double sample)
    {
        double magnitude = Math.Abs(sample);
        if (magnitude <= SaturationStart)
        {
            return sample;
        }

        double headroom = 1 - SaturationStart;
        return Math.CopySign(SaturationStart + (headroom * Math.Tanh((magnitude - SaturationStart) / headroom)), sample);
    }

    // Kept in offset order; events on the same sample stay in the order they were queued.
    private void Enqueue(ToneEvent toneEvent)
    {
        if (_eventCount == EventCapacity)
        {
            throw new InvalidOperationException($"More than {EventCapacity} tone events are waiting.");
        }

        int at = _eventCount;
        while (at > 0 && _events[at - 1].Offset > toneEvent.Offset)
        {
            _events[at] = _events[at - 1];
            at--;
        }

        _events[at] = toneEvent;
        _eventCount++;
    }

    private void Apply(ToneEvent toneEvent)
    {
        switch (toneEvent.Kind)
        {
            case ToneEventKind.Start:
                Start(toneEvent.Note, toneEvent.Strength);
                break;
            case ToneEventKind.Release:
                Release(toneEvent.Note);
                break;
            case ToneEventKind.ReleaseAll:
                _chordNoteCount = 0;
                _voicer.Forget();
                foreach (Voice voice in _voices)
                {
                    if (voice.IsHeld)
                    {
                        Listener?.OnRelease(_now, voice.Note);
                    }

                    voice.Release(_now);
                }

                break;
            case ToneEventKind.Chord:
                ChangeChord(toneEvent.Chord, toneEvent.Strength);
                break;
            case ToneEventKind.Strength:
                foreach (Voice voice in _voices)
                {
                    voice.SetStrength(toneEvent.Strength);
                }

                break;
        }
    }

    private void ChangeChord(Chord? chord, double strength)
    {
        int count = 0;
        if (chord is { } next)
        {
            count = _voicer.Next(next, ChordShape.Whole, _voicing);
        }
        else
        {
            _voicer.Forget();
        }

        ReadOnlySpan<int> notes = _voicing.AsSpan(0, count);
        CommonTones.Change(_chordNotes.AsSpan(0, _chordNoteCount), notes, strike: false, new ImmediateTones(this, strength));
        notes.CopyTo(_chordNotes);
        _chordNoteCount = count;
    }

    private void Start(int note, double strength)
    {
        Voice voice = FreeVoice();
        if (voice.IsHeld)
        {
            // A held voice is taken over: its tone ends here.
            Listener?.OnRelease(_now, voice.Note);
        }

        voice.Start(note, strength, _now);
        Starts++;
        Listener?.OnStart(_now, note, strength);
    }

    private void Release(int note)
    {
        foreach (Voice voice in _voices)
        {
            if (voice.IsHeld && voice.Note == note)
            {
                Listener?.OnRelease(_now, note);
                voice.Release(_now);
            }
        }
    }

    // An idle voice; else the one that has been releasing longest; else the oldest.
    private Voice FreeVoice()
    {
        Voice? releasing = null;
        Voice oldest = _voices[0];
        foreach (Voice voice in _voices)
        {
            if (voice.Stage == VoiceStage.Idle)
            {
                return voice;
            }

            if (voice.Stage == VoiceStage.Release && (releasing is null || voice.ReleasedAt < releasing.ReleasedAt))
            {
                releasing = voice;
            }

            if (voice.StartedAt < oldest.StartedAt)
            {
                oldest = voice;
            }
        }

        return releasing ?? oldest;
    }

    // A chord change applied on its sample, straight to the voices.
    private readonly struct ImmediateTones(Synthesizer synthesizer, double strength) : IToneTarget
    {
        public bool IsHeld(int note)
        {
            return synthesizer.Held(note) > 0;
        }

        public void Release(int note)
        {
            synthesizer.Release(note);
        }

        public void Start(int note)
        {
            synthesizer.Start(note, strength);
        }
    }

    private readonly record struct ToneEvent(int Offset, ToneEventKind Kind, int Note, double Strength, Chord? Chord = null);
}
