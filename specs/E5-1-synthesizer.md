# E5-1 · Synthesizer

- **Issue:** [#14](https://github.com/bjornpetersson1/solid-backup/issues/14)
- **Labels:** `E5 Sound`
- **Phase:** 5. Sound
- **Depends on:** E2-2 (#6)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 635 tests passed); listening to the three sounds with `--play` is a manual check for the owner; CI on Ubuntu is checked on the PR

## Summary
The app's own polyphonic synthesizer: band-limited oscillators, an ADSR envelope per voice, three sounds (pad, organ, pluck), voice leading within C3–C5 that starts from close voicing, and click-free chord changes where common tones keep sounding. `--render` writes chords to a WAV file and `--play` plays them in the headphones, so the owner can hear the sounds before live mode exists.

## Goals
- A synthesizer that renders into a buffer in the audio callback without allocating, deterministic and testable without hardware.
- A tone-level interface (start and release a tone at a sample offset in the buffer, with a strength) that E5-2 (dynamics, chord character) and E5-5 (arpeggio, strum) can drive, plus a chord-level call for the held case.
- Three sounds that are clearly different, and chord changes without clicks.

## Non-goals
- Level features: dynamics, chord character, gate, onsets (E5-2). The synth takes a strength per tone; deciding it is E5-2's job.
- Rhythm patterns and the tempo grid (E5-5).
- Connecting the synth to the live pipeline, the menu and the dashboard (E6).
- Effects (reverb, chorus, delay), filters with resonance or modulation, stereo panning, more sounds, user-defined sounds, MIDI or samples.
- Voice leading beyond the simple nearest-note rule in criterion 1 (no voice-crossing rules, no parallel-fifth avoidance, no bass line).

## Acceptance Criteria
1. **A test renders a chord and finds the right frequencies.**
   - For each sound (Pad, Organ, Pluck), rendering C major (C3 E3 G3: 130.81, 164.81, 196.00 Hz) and Cm7 (C3 Eb3 G3 Bb3) at 48 kHz finds every chord tone's fundamental as a spectral peak within ±5 cents, using the existing `Fft` with interpolation, on the steady part of the sound (Pluck: the first 200 ms after the attack).
   - The voicing is tested on its own. The first chord (and the first after the chord is released) is in close voicing: the root lies in octave 3 (C3–B3, MIDI 48–59) and each next tone of the chord, in the order `Chord.PitchClasses` gives, is the nearest one above the previous; e.g. `Am` → A3 C4 E4, `Bm7b5` → B3 D4 F4 A4, `Cmaj7(#11)` → C3 E3 G3 B3 F#4.
   - Each later chord is led from the one before: a tone in both chords keeps its note; each new tone takes the note of its pitch class within C3–C5 nearest to a tone that left (each leaving tone used once, then any previous tone), a tie going to the lower note; e.g. C → Am gives C3 E3 A3, C → F#dim gives C3 F#3 A3. A long progression stays within C3–C5.
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
- [x] Band-limited oscillators (sine, sawtooth) and a one-pole low-pass.
- [x] ADSR envelope with a 5 ms floor on attack and release.
- [x] Voice pool of 16 with the stealing rule.
- [x] Close voicing from a `Chord`, and voice leading within C3–C5 from the previous chord.
- [x] Synth with tone-level events at sample offsets and a chord-level change that keeps common tones.
- [x] The three sounds as parameter sets.
- [x] `Sound` setting, validation, README.
- [x] Chord-symbol parser for `--chords`.
- [x] `--render` and `--play` on the command line.
- [x] Tests: frequencies, voicing, allocation, polyphony and stealing, headroom, clicks, aliasing, determinism, settings, command line.

## Decisions
- **Close voicing, root in octave 3, for the first chord** (owner's choice): the chord sits under a sung melody and rarely collides with it. No bass note an octave down.
- **Voice leading for every later chord** (owner's choice while implementing; changes criterion 1 and a non-goal): the approved spec assumed a fixed root-position voicing makes shared tones land on the same note, but it does not (C is C3 E3 G3, Am A3 C4 E4: no shared note), so the common-tone rule would almost never apply. Asked, the owner chose voice leading: shared tones stay, new tones go nearest the leaving ones, within C3–C5. A chord after none (a release) starts in close voicing again.
- **Common tones keep sounding** (owner's choice): only tones that leave or arrive have an envelope edge, which is both smooth and click-free.
- **`--render` and `--play`** (owner's choice): rendering to WAV is testable in CI; `--play` is the quick check by ear.
- **Tone-level interface** (default): E5-5 needs to start each tone of an arpeggio or strum on its own sample, and E5-2 and the accent need a strength per tone. The chord-level call is a convenience built on it.
- **Sound parameters** (default): the oscillators, envelopes and filter settings in criterion 5 are starting values; the implementation may tune them by ear as long as the criteria hold, and records the final values here.
- **16 voices, steal the longest-releasing** (default): five tones for a chord, plus room for the releases of the previous chords, especially Pad's 600 ms.
- **Headroom by fixed scaling** (default): each voice is scaled so five full-strength tones stay below 0.9; no limiter or compressor.
- **No new dependency** (default): oscillators, envelope and filter are small enough to write; the existing `Fft` and `WavWriter` are reused.
- **`--play` feeds the output directly** (default): the PortAudio output is driven by the rendered chords, not by a microphone, so `--play` needs no microphone permission.
- **Band-limited sawtooth by wavetables** (implementation): one table per octave, built once by an inverse FFT with only the harmonics at or below 18 kHz for the octave's top note, scaled to a peak of 1. Nothing reaches 20 kHz, so the aliasing criterion holds by construction; PolyBLEP would leave genuine sawtooth harmonics between 20 and 24 kHz above −40 dB. Organ's four harmonics are one more table (clean code), so every voice costs one or two table lookups per sample instead of four sines.
- **Chord changes resolve on their sample** (clean code): `SetChord` queues an event, and the voice leading and the common-tone check run when it takes effect, against the voices as they are then. Chords and tones queued ahead (E5-5's patterns) therefore lead correctly from each other.
- **Sound values as specified** (implementation): Pad two sawtooths ±7 cents, low-pass 2000 Hz, A 200 / D 300 / S 0.8 / R 600 ms; Organ harmonics 1–4 weighted 1, 0.5, 0.3, 0.2 (scaled to sum 1), A 10 / S 1 / R 50 ms; Pluck sawtooth, low-pass 3000 Hz, A 5 / D 400 / S 0 / R 200 ms. Envelopes are linear; the low-pass is one-pole. Not tuned by ear yet: listening is the owner's manual check.
- **Voice gain 0.15** (implementation): five full-strength tones peak at most 0.75, which leaves room for E5-2's extra bass root (six tones ≤ 0.9).
- **A shared tone that has died away starts again** (implementation): with Pluck (sustain 0) a "kept" tone may already be silent; it is plucked again rather than left out of the new chord.
- **Pad's frequency test resolves both oscillators** (implementation): Pad's fundamental is the centre of two oscillators 7 cents apart, which beat; a 5.5 s window resolves them, and the test checks their geometric centre within ±5 cents. Organ and Pluck are measured directly (Pluck over the first 200 ms after its attack).
- **The click test's steady reference** (implementation): for each event (start, C → Am, Am → F#dim, release), the largest neighbouring-sample step in the 20 ms after it is compared with the largest step in the steady chords on either side, from the end of the attack to the next event.
- **`--render` uses `SampleRate`** (implementation): 48 kHz by default, as the spec says, and 44.1 kHz when the setting says so.
- **Playback pulls from the rendered samples** (clean code): `--play` renders first, then `PortAudioPlayback` opens an output stream whose callback reads straight from the samples. There is one clock, so nothing is handed over or corrected for drift. `IAudioPlayback` is the small interface for this offline playback. The live path still goes only through `IAudioInput` and `IAudioOutput`.
- **`--output` works with `--play`, `--input` does not** (implementation): the device message now says so.
- **`StopKeys.WaitForStop`** (clean code): `--passthrough` and `--play` share the wait for q, Esc, Ctrl+C or a cancellation, including the Ctrl+C hookup.
- **Code review fixes:**
  - a voice taken while it still sounds fades out over 5 ms before its new tone starts, instead of stopping in one sample (a test shows the step without the fade);
  - close voicing moves a tone past C5 down an octave (a high root with a seventh and a tension), so every chord stays within C3–C5;
  - a tension that is already a chord tone is neither voiced twice nor accepted by `--chords` (`Cadd9(9)`, `Cdim(#11)`);
  - a safety saturation above 0.9 bends the sum smoothly towards 1 when releases and attacks overlap at fast chord changes, so the output never passes full scale; a held chord never reaches it, so the headroom criterion is unchanged. This departs from "no limiter" in Decisions only for those overlaps;
  - a sound that sustains below full level always decays over at least 5 ms, even with a decay of 0;
  - `--render` reports a bad path in Swedish, and `--render`/`--play` refuse more than 10 minutes of chords instead of overflowing;
  - `--play` counts output underflows and prints them; the mono-to-channels copy and the channel count are shared with `PortAudioOutput`;
  - the exact enum-name parser moved from `HarmonySettings` to `Configuration.SettingNames`, as `Sound` is not a harmony setting;
  - `Chord.Tensions` is a read-only copy beside the constant pattern check, and a test keeps them in step.
- **Not changed in clean code:** the pairwise option checks in `AppCommand` were not turned into a table of modes; E5-2 and E5-5 add options and can restructure them if they grow. Rendering stays sample by sample over the voices rather than voice by voice per segment; it is well within the budget, and the simpler loop keeps events exact.
- **Test queries on the synthesizer** (implementation): `Starts`, `Sounding(note)` and `Held(note)` let tests check the common-tone and stealing rules without reading audio.
- **Shared helpers reused** (clean code): `Pitch.Frequency` for tuning, `Duration.Samples` for millisecond lengths, `Pitch.ParabolaOffset` and `Pitch.Cents` in the test spectrum, and `Chord.Tensions` for the parser.

## Open Questions
