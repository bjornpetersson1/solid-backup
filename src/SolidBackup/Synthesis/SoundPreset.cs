namespace SolidBackup.Synthesis;

public enum Oscillator
{
    /// <summary>A band-limited sawtooth from <see cref="Wavetables.Sawtooth"/>.</summary>
    Sawtooth,

    /// <summary>Harmonics 1–4 mixed like an organ's drawbars, from <see cref="Wavetables.Organ"/>.</summary>
    Organ,
}

/// <summary>
/// What a sound is made of: the oscillator, an optional second oscillator detuned up and down, an
/// optional one-pole low-pass, and the ADSR envelope. Attack and release are never shorter than
/// <see cref="MinEdgeMs"/>, so no tone starts or stops with a click.
/// </summary>
public sealed record SoundPreset(
    Oscillator Oscillator,
    double DetuneCents,
    double CutoffHz,
    double AttackMs,
    double DecayMs,
    double Sustain,
    double ReleaseMs)
{
    public const double MinEdgeMs = 5;

    public static SoundPreset For(Sound sound)
    {
        return sound switch
        {
            Sound.Pad => new(Oscillator.Sawtooth, DetuneCents: 7, CutoffHz: 2000, AttackMs: 200, DecayMs: 300, Sustain: 0.8, ReleaseMs: 600),
            Sound.Organ => new(Oscillator.Organ, DetuneCents: 0, CutoffHz: 0, AttackMs: 10, DecayMs: 0, Sustain: 1, ReleaseMs: 50),
            Sound.Pluck => new(Oscillator.Sawtooth, DetuneCents: 0, CutoffHz: 3000, AttackMs: 5, DecayMs: 400, Sustain: 0, ReleaseMs: 200),
            _ => throw new ArgumentOutOfRangeException(nameof(sound)),
        };
    }

    /// <summary>The release as it is played, never shorter than <see cref="MinEdgeMs"/>.</summary>
    public double PlayedReleaseMs => Math.Max(ReleaseMs, MinEdgeMs);

    public double PlayedAttackMs => Math.Max(AttackMs, MinEdgeMs);

    /// <summary>The decay as it is played: a sound that sustains below full level never drops to it in less than <see cref="MinEdgeMs"/>.</summary>
    public double PlayedDecayMs => Math.Max(DecayMs, MinEdgeMs);
}
