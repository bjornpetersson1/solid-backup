using SolidBackup.Analysis;

namespace SolidBackup.Synthesis;

public enum VoiceStage
{
    Idle,
    Attack,
    Decay,
    Sustain,
    Release,

    /// <summary>Fading out over <see cref="SoundPreset.MinEdgeMs"/> because the voice was taken for a new tone, which starts once it is silent.</summary>
    Stolen,
}

/// <summary>
/// One tone of the synthesizer: its oscillators, low-pass and a linear ADSR envelope. Every field is set
/// when the voice starts, so rendering allocates nothing. A voice taken while it still sounds fades out over
/// <see cref="SoundPreset.MinEdgeMs"/> first, so the old tone never stops with a click.
/// </summary>
internal sealed class Voice(SoundPreset preset, int sampleRate, double gain)
{
    private readonly double _attackStep = 1.0 / Duration.Samples(preset.PlayedAttackMs, sampleRate);
    private readonly double _decayStep = (1 - preset.Sustain) / Duration.Samples(preset.PlayedDecayMs, sampleRate);
    private readonly long _releaseSamples = Duration.Samples(preset.PlayedReleaseMs, sampleRate);
    private readonly long _stealSamples = Duration.Samples(SoundPreset.MinEdgeMs, sampleRate);
    private readonly double _detune = Math.Pow(2, preset.DetuneCents / 1200);
    private readonly bool _detuned = preset.DetuneCents > 0;
    private readonly bool _filtered = preset.CutoffHz > 0;
    private readonly double _cutoff = 1 - Math.Exp(-2 * Math.PI * preset.CutoffHz / sampleRate);
    private float[] _table = Wavetables.Organ;
    private double _amplitude;
    private double _level;
    private double _releaseStep;
    private double _phase;
    private double _detunedPhase;
    private double _increment;
    private double _detunedIncrement;
    private double _filterState;
    private double _pendingStrength;

    public VoiceStage Stage { get; private set; }

    public int Note { get; private set; }

    /// <summary>When the voice started or was released, in samples since the synthesizer began; the stealing rule uses them.</summary>
    public long StartedAt { get; private set; }

    public long ReleasedAt { get; private set; }

    /// <summary>True while the voice holds its note: also while a stolen voice fades out before its new tone.</summary>
    public bool IsHeld => Stage is VoiceStage.Attack or VoiceStage.Decay or VoiceStage.Sustain or VoiceStage.Stolen;

    public void Start(int note, double strength, long now)
    {
        Note = note;
        StartedAt = now;
        if (Stage != VoiceStage.Idle && _level > 0)
        {
            Stage = VoiceStage.Stolen;
            _pendingStrength = strength;
            _releaseStep = _level / _stealSamples;
            return;
        }

        Begin(strength);
    }

    /// <summary>Releases the tone; a stolen voice keeps its fade and its new tone never starts.</summary>
    public void Release(long now)
    {
        if (!IsHeld)
        {
            return;
        }

        if (Stage != VoiceStage.Stolen)
        {
            _releaseStep = _level / _releaseSamples;
        }

        ReleasedAt = now;
        Stage = VoiceStage.Release;
    }

    public double Next()
    {
        AdvanceEnvelope();
        if (Stage == VoiceStage.Idle)
        {
            return 0;
        }

        double sample = Wavetables.Lookup(_table, _phase);
        _phase = Wrap(_phase + _increment);
        if (_detuned)
        {
            sample = (sample + Wavetables.Lookup(_table, _detunedPhase)) / 2;
            _detunedPhase = Wrap(_detunedPhase + _detunedIncrement);
        }

        if (_filtered)
        {
            _filterState += _cutoff * (sample - _filterState);
            sample = _filterState;
        }

        return sample * _level * _amplitude;
    }

    private static double Wrap(double phase)
    {
        return phase >= 1 ? phase - 1 : phase;
    }

    private void Begin(double strength)
    {
        Stage = VoiceStage.Attack;
        _amplitude = gain * Math.Clamp(strength, 0, 1);
        _level = 0;
        _phase = 0;
        _detunedPhase = 0;
        _filterState = 0;
        _table = preset.Oscillator == Oscillator.Organ ? Wavetables.Organ : Wavetables.Sawtooth(Note);
        double increment = Pitch.Frequency(Note * 100) / sampleRate;
        _increment = _detuned ? increment * _detune : increment;
        _detunedIncrement = increment / _detune;
    }

    private void AdvanceEnvelope()
    {
        switch (Stage)
        {
            case VoiceStage.Attack:
                _level += _attackStep;
                if (_level >= 1)
                {
                    _level = 1;
                    Stage = preset.Sustain < 1 ? VoiceStage.Decay : VoiceStage.Sustain;
                }

                break;
            case VoiceStage.Decay:
                _level -= _decayStep;
                if (_level <= preset.Sustain)
                {
                    _level = preset.Sustain;
                    Stage = preset.Sustain > 0 ? VoiceStage.Sustain : VoiceStage.Idle;
                }

                break;
            case VoiceStage.Release:
                _level -= _releaseStep;
                if (_level <= 0)
                {
                    _level = 0;
                    Stage = VoiceStage.Idle;
                }

                break;
            case VoiceStage.Stolen:
                _level -= _releaseStep;
                if (_level <= 0)
                {
                    Begin(_pendingStrength);
                }

                break;
        }
    }
}
