# E5-1 · Synthesizer

- **Issue:** [#14](https://github.com/bjornpetersson1/solid-backup/issues/14)
- **Labels:** `E5 Sound`
- **Phase:** 5. Sound
- **Depends on:** E2-2 (#6)
- **Status:** Approved

## Summary
The app's own polyphonic synthesizer: band-limited oscillators, an ADSR envelope per voice, three sounds (pad, organ, pluck), close voicing of a chord around C3–C5, and click-free chord changes where common tones keep sounding. `--render` writes chords to a WAV file and `--play` plays them in the headphones, so the owner can hear the sounds before live mode exists.

## Goals
- A synthesizer that renders into a buffer in the audio callback without allocating, deterministic and testable without hardware.
- A tone-level interface (start and release a tone at a sample offset in the buffer, with a strength) that E5-2 (dynamics, chord character) and E5-5 (arpeggio, strum) can drive, plus a chord-level call for the held case.
- Three sounds that are clearly different, and chord changes without clicks.

## Non-goals
- Level features: dynamics, chord character, gate, onsets (E5-2). The synth takes a strength per tone; deciding it is E5-2's job.
- Rhythm patterns and the tempo grid (E5-5).
- Connecting the synth to the live pipeline, the menu and the dashboard (E6).
- Effects (reverb, chorus, delay), filters with resonance or modulation, stereo panning, more sounds, user-defined sounds, MIDI or samples.
- Voice leading between chords beyond keeping common tones (the voicing is fixed per chord, see Decisions).

## Acceptance Criteria
1. **A test renders a chord and finds the right frequencies.**
   - For each sound (Pad, Organ, Pluck), rendering C major (C3 E3 G3: 130.81, 164.81, 196.00 Hz) and Cm7 (C3 Eb3 G3 Bb3) at 48 kHz finds every chord tone's fundamental as a spectral peak within ±5 cents, using the existing `Fft` with interpolation, on the steady part of the sound (Pluck: the first 200 ms after the attack).
   - The voicing is tested on its own: the root lies in octave 3 (C3–B3, MIDI 48–59) and each next tone of the chord, in the order `Chord.PitchClasses` gives, is the nearest one above the previous; e.g. `Am` → A3 C4 E4, `Bm7b5` → B3 D4 F4 A4, `Cmaj7(#11)` → C3 E3 G3 B3 F#4.
   - Tuning is equal temperament with A4 = 440 Hz.
2. **Rendering in the audio callback allocates nothing (verified by a test).**
   - A test measures `GC.GetAllocatedBytesForCurrentThread()` across rendering after warm-up, with tone starts, releases and chord changes inside the measured buffers, for each sound, and asserts 0 bytes.
   - The render path contains no `lock`, logging, console or I/O.
3. **Polyphony for a chord.**
   - The synth has 16 voices, allocated up front. A chord of up to five tones (a seventh chord with a tension) sounds in full.
   - When every voice is busy, a new tone takes the voice that has been releasing longest; if none is releasing, the oldest voice. A test fills the voices and checks which is taken.
   - The sum of a five-tone chord at full strength never exceeds 0.9 in absolute value (about −1 dBFS) for any sound; a test renders one and checks the peak.
4. **No clicks at chord changes.**
   - Every attack and release is at least 5 ms long, whatever the sound.
   - Chord change rule: tones in both chords keep sounding without a new attack; tones only in the old chord are released; tones only in the new chord start with an attack. A test checks C → Am (C and E keep sounding, G released, A started).
   - For each sound, a test renders C → Am and C → F#dim (no common tones), from silence and back to silence, and checks that the largest difference between neighbouring samples within 20 ms after each event (start, change, release) is no larger than the largest difference during the steady chords.
5. **Three sounds: pad, organ, pluck.**
   - `Pad`: two band-limited sawtooth oscillators detuned ±7 cents through a gentle low-pass; slow attack (200 ms) and long release (600 ms).
   - `Organ`: sines at harmonics 1, 2, 3 and 4; fast attack and release (10 ms and 50 ms), full sustain.
   - `Pluck`: a band-limited sawtooth through a low-pass, a 5 ms attack, a 400 ms decay to silence (sustain 0) and a 200 ms release.
   - The oscillators are band-limited (no audible aliasing on a C5 sawtooth: a test checks that the energy above 20 kHz at 48 kHz is at least 40 dB below the fundamental).
   - The sound is the setting `Sound` (`Pad`, `Organ`, `Pluck`; default `Pad`) in `AppSettings` and `appsettings.json`, validated with a Swedish error and a test, and listed in the README settings table.
   - Rendering is deterministic: the same events give the same samples, bit for bit (a test renders twice).
6. **The owner can hear the synth.**
   - `--render <wav>` writes the chords to a 48 kHz mono 16-bit WAV file. `--play` plays them on the output device (the setting or `--output`) and stops at the end, or on `q`, `Esc` or `Ctrl+C`, with a Swedish line saying how to stop.
   - Both take `--sound <Pad|Organ|Pluck>` (default from the settings), `--chords "<symbols>"` (default `"C Am F G"`, symbols as in `docs/harmony.md`, e.g. `Cmaj7(#11)`) and `--chord-ms <ms>` (default 1000), and end with the last chord's release.
   - An unknown sound or chord symbol, or a `--chord-ms` outside 100–10000, gives a Swedish error and exit code 1.
   - `--render` and `--play` cannot be combined with each other or with `--passthrough`, `--devices` or `--harmonize`; doing so gives a Swedish error.
   - `--help` lists the new options with Swedish descriptions; tests check the help text, a `--render` run (the file is valid WAV with the expected length) and the errors.
   - The README "Run" section has examples. Listening to the three sounds with `--play` is a manual check for the owner.
7. **The canonical check passes.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root, on macOS and in CI on Ubuntu.

## Tasks
- [ ] Band-limited oscillators (sine, sawtooth) and a one-pole low-pass.
- [ ] ADSR envelope with a 5 ms floor on attack and release.
- [ ] Voice pool of 16 with the stealing rule.
- [ ] Close voicing from a `Chord` around C3–C5.
- [ ] Synth with tone-level events at sample offsets and a chord-level change that keeps common tones.
- [ ] The three sounds as parameter sets.
- [ ] `Sound` setting, validation, README.
- [ ] Chord-symbol parser for `--chords`.
- [ ] `--render` and `--play` on the command line.
- [ ] Tests: frequencies, voicing, allocation, polyphony and stealing, headroom, clicks, aliasing, determinism, settings, command line.

## Decisions
- **Close voicing, root in octave 3** (owner's choice): the chord sits under a sung melody and rarely collides with it. No bass note an octave down, and no voice leading between chords; the fixed voicing makes common tones land on the same frequency, which the chord-change rule needs.
- **Common tones keep sounding** (owner's choice): only tones that leave or arrive have an envelope edge, which is both smooth and click-free.
- **`--render` and `--play`** (owner's choice): rendering to WAV is testable in CI; `--play` is the quick check by ear.
- **Tone-level interface** (default): E5-5 needs to start each tone of an arpeggio or strum on its own sample, and E5-2 and the accent need a strength per tone. The chord-level call is a convenience built on it.
- **Sound parameters** (default): the oscillators, envelopes and filter settings in criterion 5 are starting values; the implementation may tune them by ear as long as the criteria hold, and records the final values here.
- **16 voices, steal the longest-releasing** (default): five tones for a chord, plus room for the releases of the previous chords, especially Pad's 600 ms.
- **Headroom by fixed scaling** (default): each voice is scaled so five full-strength tones stay below 0.9; no limiter or compressor.
- **No new dependency** (default): oscillators, envelope and filter are small enough to write; the existing `Fft` and `WavWriter` are reused.
- **`--play` feeds the output directly** (default): the PortAudio output is driven by the rendered chords, not by a microphone, so `--play` needs no microphone permission.

## Open Questions
