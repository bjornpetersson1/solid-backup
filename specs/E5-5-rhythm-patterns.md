# E5-5 · Rhythm patterns

- **Issue:** [#18](https://github.com/bjornpetersson1/solid-backup/issues/18)
- **Labels:** `E5 Sound`
- **Phase:** 5. Sound
- **Depends on:** E5-4 (#17), E5-1 (#14); builds on E5-2 (#15)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 860 tests passed); listening to the patterns against a metronome is a manual check for the owner; CI on Ubuntu is checked on the PR
- **Superseded in part:** with `Autoplay` on (E7-2, #71) the pattern does not stop after `PhraseResetMs` and the gate does not silence it; with it off, as below.

## Summary
The tempo grid and the three patterns from `docs/rhythm.md` (Held, Arpeggio, Strum) between E5-2's level features and E5-1's synthesizer, with the settings `RhythmPattern`, `Bpm` and `TimeSignature`, and the examples R1–R12 as tests.

## Goals
- A drift-free, allocation-free tempo grid that E5-6 (Jazz) can also use.
- The three patterns, placing every tone on its sample.
- The patterns audible through `--render` and `--accompany`.

## Non-goals
- Tap tempo, tempo from onsets, live tempo following (decided against in E5-4).
- The menu (E6-2) and the live pipeline (E6-1).
- Jazz's chord loop (E5-6).
- Other patterns, swing, other time signatures than 4/4 and 3/4, other note values.

## Acceptance Criteria
1. **A rendered pattern has its notes on the expected samples within 1 ms.**
   - Tests render each pattern through E5-1's synth and read the tone starts and releases from the synth's events, and check them against the expected sample positions within 1 ms (48 samples at 48 kHz). In practice they are exact (`docs/rhythm.md`, "No drift").
   - A ten-minute render at 70 BPM, 48 kHz, has its last beat on the exactly rounded sample (no drift).
2. **The examples R1–R12 in `docs/rhythm.md` are tests that pass**, one test per example, named after it.
3. **The tempo grid.**
   - A pure function from a sample position, sample rate, `Bpm` and `TimeSignature` to bar, beat and step, as in `docs/rhythm.md`: origin at sample 0, step *n* at the exact time rounded to the nearest sample.
   - It allocates nothing (a test).
4. **The patterns and their rules from `docs/rhythm.md`.**
   - Held changes at once; Arpeggio and Strum use a new chord from their next step, also the first chord after silence; the downbeat accent (full strength, 0.8 on other steps); Strum's 10 ms spread from the lowest tone; Arpeggio's tone by step in bar; the stop after `PhraseResetMs` of silence; the grid keeps running through a stop.
   - With E5-2's features: the gate closing releases the sounding tones at once in every pattern; dynamics set each tone's strength when it starts (Held: continuously), and the accent multiplies it; chord character sets the tones the pattern plays from its next step; an onset's change without hold times applies, but in Arpeggio and Strum its extra strike is not played, because the pattern decides when tones start.
   - Rendering with every pattern allocates nothing per buffer after warm-up (a test).
5. **Settings.**
   - `RhythmPattern` (`Held`, `Arpeggio`, `Strum`; default `Held`), `Bpm` (whole number 40–240; default 90) and `TimeSignature` (`4/4`, `3/4`; default `4/4`) in `AppSettings` and `appsettings.json`, validated with Swedish errors and tests, and listed in the README.
6. **The owner can hear the patterns.**
   - `--render`, `--play` and `--accompany` take `--pattern <Held|Arpeggio|Strum>`, `--bpm <40–240>` and `--time-signature <4/4|3/4>`, defaulting to the settings. With `--render` and `--play`, each chord in `--chords` lasts `--chord-ms` from the start, and the pattern plays it on the grid.
   - Invalid values give Swedish errors and exit code 1; `--help` lists the options; tests cover them.
   - Listening (even eighths, steady tempo against a metronome, the accent) is a manual check for the owner.
7. **The canonical check passes.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root, on macOS and in CI on Ubuntu.

## Tasks
- [x] The tempo grid.
- [x] The pattern component between the level features and the synth: Held, Arpeggio, Strum, the accent, start and stop.
- [x] Settings, validation, README.
- [x] `--pattern`, `--bpm`, `--time-signature` on `--render`, `--play` and `--accompany`.
- [x] Tests: R1–R12, the grid, drift over ten minutes, the interplay with each level feature, allocation, settings, command line.

## Decisions
- **Everything about tempo and patterns comes from `docs/rhythm.md`** (approved in E5-4); this spec adds no rules of its own beyond the two below.
- **An onset's extra strike is not played in Arpeggio and Strum** (default): `docs/rhythm.md` says onsets do not touch the grid, and E5-2's strike is a held-chord behaviour. The onset still lets the chord change without hold times, from the next step.
- **Chord character applies from the next step** (default), like a chord change, so the pattern never cuts a tone mid-step.
- **Tests read the synth's tone events** (default), not the audio, so positions are exact and independent of the sound's attack.
- **Builds on E5-1** (tone-level events at sample offsets, voicing) **and E5-2** (the accompaniment per buffer, the offline chain, `--accompany`).

## Open Questions
- **Arpeggio and Strum use the voice-led voicing** (owner's choice while implementing): `docs/rhythm.md`'s R5 assumed F in close voicing (F A C), but since E5-1 a chord is led from the one before, so F after C is C3 F3 A3. Asked, the owner kept the voice leading in every pattern; R5 now reads "at 32000 A, at 48000 C", and `docs/rhythm.md` says a bar starts on the chord's lowest tone, not always its root. After a stop, the next chord is in close voicing again.
- **Patterns sit behind E5-2's `IRhythmPattern` seam** (implementation): `HeldPattern` (unchanged), `ArpeggioPattern` and `StrumPattern`, chosen by `RhythmPatterns.Create` from the settings. They read the buffer's end sample from `AnalysisFrame`, place tones with the synthesizer's sample offsets (a strum's later tones may fall in the next buffer), and share `ChordTones`, which voices a chord again only when it or its shape changes. The tempo grid is `Rhythm.TempoGrid`, ready for E5-6.
- **The phrase stop comes from the chain** (implementation): E5-2's chain already turns a chord silent after `PhraseResetMs` (`Harmonizer.InPhrase`); a pattern only releases its tones when told nothing sounds. R11 and R12 run through the whole chain with the gate off.
- **`--render` and `--play` play through the patterns** (implementation): `ChordSequence` moved to `Accompaniment` and renders in pieces that start on each chord change, so every pattern sees each chord from its first sample on the same grid as live.
- **Tone events for tests** (implementation): the synthesizer tells an optional `IToneListener` the sample of every tone start and release; the tests read positions from it, so they are exact and independent of the sound.
- **Overrides as one record** (implementation): `RhythmOverrides` carries `--pattern`, `--bpm` and `--time-signature` to `--render`, `--play` and `--accompany`.
- **Clean code:** `TempoGrid.FromSettings` builds the grid in one place (E5-6's harmony uses it too), and `TempoGrid.StepsIn` walks the steps of a buffer without allocating, so the patterns no longer repeat that loop. `HeldPattern` shares `ChordTones` with the other patterns, `AnalysisFrame.Position` builds a frame for rendering, the time signatures are one table, and the ten-minute drift test checks the grid directly plus the last minute played.
- **Code review fixes:**
  - when a strum stops (the gate closes, the phrase ends, `--render` reaches its end) while its later tones are still waiting to start, each such tone is released on its own start, so none hangs;
  - Arpeggio and Strum voice a chord only on a step, so a chord the harmony passes through between steps does not move the voice leading;
  - `--render` and `--play` strike each listed chord where it begins, so a repeated chord is heard again with a sound that dies away (Pluck), as before E5-5;
  - the synthesizer tells its listener when it takes over a held voice, so no tone ends unreported;
  - the strum spread uses `Duration.Samples`, and one sample-to-ms helper serves the chain and rendering.
- **Strength of a sounding tone in Arpeggio and Strum** (code review finding declined): `docs/rhythm.md` says dynamics set each tone's strength when it starts and only Held follows the level continuously, so a strum keeps its strength until the next beat, as a struck chord does.
