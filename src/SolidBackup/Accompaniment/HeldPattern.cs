using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// The chord held: a new chord or shape takes over at once, tones it shares with the one before keep sounding, and a
/// strike starts every tone anew. The strength follows the frame, sent on only when it has moved by
/// <see cref="StrengthStepDb"/>, as the synthesizer glides between steps. After the chord stops, the next starts in
/// close voicing again.
/// </summary>
public sealed class HeldPattern : IRhythmPattern
{
    /// <summary>The smallest change in strength that is passed on; smaller steps are inaudible.</summary>
    public const double StrengthStepDb = 0.1;

    private readonly ChordTones _tones = new();
    private readonly int[] _sounding = new int[ChordVoicing.MaxShapedTones];
    private int _soundingCount;
    private bool _playing;
    private Chord _chord;
    private ChordShape _shape;
    private double _strength;

    public void Play(in AnalysisFrame analysis, in AccompanimentFrame frame, Synthesizer synthesizer)
    {
        if (!frame.Sounding || frame.Chord is not { } chord)
        {
            if (_playing)
            {
                Stop(synthesizer);
            }

            return;
        }

        bool strike = frame.Strike && _playing;
        if (!_playing)
        {
            _strength = frame.Strength;
        }

        if (!_playing || strike || chord != _chord || frame.Shape != _shape)
        {
            Change(chord, frame.Shape, strike, frame.Strength, synthesizer);
        }

        if (Math.Abs(20 * Math.Log10(frame.Strength / _strength)) >= StrengthStepDb)
        {
            synthesizer.SetStrength(frame.Strength);
            _strength = frame.Strength;
        }
    }

    private void Change(Chord chord, ChordShape shape, bool strike, double strength, Synthesizer synthesizer)
    {
        ReadOnlySpan<int> notes = _tones.For(chord, shape);
        CommonTones.Change(_sounding.AsSpan(0, _soundingCount), notes, strike, new QueuedTones(synthesizer, strength));
        notes.CopyTo(_sounding);
        _soundingCount = notes.Length;
        _playing = true;
        _chord = chord;
        _shape = shape;
    }

    private void Stop(Synthesizer synthesizer)
    {
        foreach (int note in _sounding.AsSpan(0, _soundingCount))
        {
            synthesizer.ReleaseTone(note);
        }

        _soundingCount = 0;
        _playing = false;
        _tones.Forget();
    }

    // Tones queued on the synthesizer at the start of the next buffer.
    private readonly struct QueuedTones(Synthesizer synthesizer, double strength) : IToneTarget
    {
        public bool IsHeld(int note)
        {
            return synthesizer.Held(note) > 0;
        }

        public void Release(int note)
        {
            synthesizer.ReleaseTone(note);
        }

        public void Start(int note)
        {
            synthesizer.StartTone(note, strength);
        }
    }
}
