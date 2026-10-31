# E4-3 · Key detection

- **Issue:** [#13](https://github.com/bjornpetersson1/solid-backup/issues/13)
- **Labels:** `E4 Harmony`
- **Phase:** 4. Harmony
- **Depends on:** E3-2 (#9), E4-1 (#11); builds on E4-2 (#12)
- **Status:** Approved

## Summary
With `Key` set to `auto`, a buffer-based, allocation-free key estimator finds the tonic and major or minor from the recent notes (Krumhansl–Kessler profiles over a decaying, duration-weighted pitch-class histogram) and hands it to E4-2's harmony engine. It plays no chord until it is sure, and changes key on a modulation within `KeyChangeSeconds` without flickering. It is measured on new generated melodies in the corpus.

## Goals
- `auto` works end to end in the engine and in `--harmonize`.
- A known key is found and kept; a modulation is followed within the configured time; a passing foreign note does not change the key.
- Ready for the audio callback in E6-1: 0 bytes allocated per call.

## Non-goals
- Estimating modes other than Ionian (major) and Aeolian (minor); a mode needs a fixed key (`docs/harmony.md`).
- Blues moving its own current key (E4-5); the estimator gives the starting key that E4-5 returns to.
- Recorded melodies from the owner; they can be added to the corpus later.
- Showing the key on the dashboard (E6-3) and the menu choice (E6-2); here the key is exposed by the engine and printed by `--harmonize`.

## Acceptance Criteria
1. **Generated melodies with a known key in the corpus.**
   - `GeneratedCorpus` gains melodies with a reference key per stretch: at least four in major and three in minor, in different keys (sharp and flat, with at least one on a black-key tonic), several starting on a note other than the tonic and at least one with a chromatic passing note; and two that modulate (C major → G major, A minor → D minor). They are built from sines with fixed seeds, like the other generated clips, and documented in `corpus/README.md`; the tool's `tones` command writes them as WAV with their reference.
2. **The key is estimated from the recent notes.**
   - Each note's pitch class is weighted by how long it sounds, in a histogram that decays exponentially with time; the key is the best correlation with the Krumhansl–Kessler major and minor profiles over all 24 keys.
   - Tests: a C major scale and an A harmonic minor scale give C major and A minor; transposing a melody transposes the key.
3. **No chord until sure.**
   - With `auto`, the engine reports no key and no chord until at least `KeyFirstSeconds` of notes have been heard and the best key leads the next by a fixed margin. Then the chord for the sounding note is chosen at once, as after silence.
   - Tests: no chord before that point; a chord at that point.
4. **A melody in a known key gives that key.**
   - For every non-modulating generated melody, the first key reported is the reference key and it never changes during the melody.
5. **A modulating melody gives the new key within `KeyChangeSeconds`.**
   - A new key replaces the current one only after leading it by the margin for a hold time; the decay and the hold time follow `KeyChangeSeconds`.
   - Tests: on both modulating melodies, the new key is reported no later than `KeyChangeSeconds` after the first note of the new key, and the old key is not reported again. This holds at the default of 4 s and at 8 s. A single foreign note, and a borrowed passage shorter than half of `KeyChangeSeconds`, do not change the key.
6. **The settings.**
   - New settings `KeyChangeSeconds` (default 4) and `KeyFirstSeconds` (default 2), validated (positive and finite, with Swedish errors and tests) and listed in the README. The default `Key` becomes `auto`.
7. **Allocation-free.** A test measures `GC.GetAllocatedBytesForCurrentThread()` across the engine's per-estimate call with `auto`, after warm-up, and asserts 0 bytes.
8. **`--harmonize` with `auto`.** `--key auto` (and the new default) prints the key when it is first found and at every change, alongside the chord changes; the E4-2 error for `auto` is removed. A test on a generated melody checks the printed keys.
9. **The checks pass.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` succeeds without audio hardware or network.

## Tasks
- [ ] The generated melodies with key references, in `GeneratedCorpus`, the `tones` command and `corpus/README.md`.
- [ ] The key estimator: decaying histogram, profile correlation, margin and hold.
- [ ] Wiring `auto` into the engine: no chord until sure, then an immediate choice; key changes.
- [ ] The settings, validation, `appsettings.json` (`Key` → `auto`) and the README.
- [ ] Tests for the criteria above, including allocations.
- [ ] `--harmonize` printing keys.

## Decisions
- **Generated melodies** (owner's choice): testable without recording, so the epic needs no stop; recorded melodies can come later as a manual check.
- **Krumhansl–Kessler profiles over a decaying histogram** (owner's choice): well known, simple, deterministic and allocation-free.
- **`KeyChangeSeconds` = 4** (owner's choice): short enough to follow a modulation, long enough not to flicker on a borrowed note. The setting is the upper bound the tests check; the decay rate and hold time derived from it, and the fixed margin, are chosen against the generated melodies and documented in code and in `corpus/README.md`.
- **No chord until sure** (owner's choice): rather a short silence at the start than chords in a wrong key. This departs from the "first note after silence at once" rule in `docs/harmony.md` only for the time before the first key; afterwards the rule holds.
- **`KeyFirstSeconds` = 2** (default): the minimum amount of sung notes before the first key, alongside the margin.
- **Sung time drives the histogram** (default): the histogram decays with time while notes sound; silence neither adds nor removes anything, so the key survives a pause between phrases.
- **The engine's `PhraseResetMs` does not reset the key** (default): it forgets the previous chord, not the estimated key.

## Open Questions
- None
