# E3-2 · Pitch detection

- **Issue:** [#9](https://github.com/bjornpetersson1/solid-backup/issues/9)
- **Labels:** `E3 Analysis`
- **Phase:** 3. Analysis
- **Depends on:** E3-1 (#8)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 246 tests passed; pitch accuracy 99.35 %, octave errors 0.00 %, false notes 0.19 % on the corpus); CI on Ubuntu is checked on the PR

## Summary
A monophonic YIN pitch detector that gives, for every input buffer, either no note or the note, its deviation in cents and a confidence value, within the configured range (E2–C7 by default). It is measured on the E3-1 corpus with E3-1's scorer and must reach the pitch targets in Measurement.

## Goals
- A pure, deterministic, buffer-based detector that is fast enough and allocation-free for the audio callback, so E4 and E6 can use it live.
- The pitch accuracy, octave error and false note targets met on the whole corpus.
- A measurement report the owner can read and rerun.

## Non-goals
- Level in dBFS, the noise gate and onset detection (E3-3). The detector has only its own fixed energy floor for near-silence, not the configurable gate.
- Wiring the detector into the live pipeline, `--passthrough` or the dashboard (E6). The `A4 +12 cent` display format is provided but not shown anywhere yet.
- Choosing chords or a key from the notes (E4).
- Polyphonic input, or notes outside the configured range.
- Look-ahead smoothing (pYIN/HMM) that would add latency.

## Acceptance Criteria
1. **A YIN pitch detector gives note, cents and confidence per buffer.**
   - It takes mono buffers of any length up to `BufferSize` at the configured `SampleRate`, keeps its own history of the most recent samples, and after every buffer reports either "no note" or: the frequency, the pitch in MIDI cents, the nearest note name with octave (e.g. `A4`), the deviation in cents (−50 to +50) and a confidence from 0 to 1.
   - Each report carries the time it refers to (the centre of its analysis window), which E3-1's scorer uses.
   - The difference function is computed with an FFT written in the project (no new package), so a fresh estimate is made every buffer.
   - A test on generated sines and sawtooths gives the right note within ±10 cents across E2–C7 at 48 kHz and at 44.1 kHz.
2. **The detector stays within the configured range.**
   - It searches only between `LowestNote` and `HighestNote` from the settings (E2 and C7 by default), and reports "no note" for a tone well outside the range; tests check both ends and a narrowed range.
3. **It decides "no note" by confidence, with a short hysteresis.**
   - A note is reported only when the confidence is at or above `PitchConfidenceThreshold`, a new setting in `appsettings.json` (validated to lie between 0 and 1, with a Swedish error and a test; listed in the README settings table).
   - Input below a fixed, very low energy floor (digital silence) is always "no note".
   - A change to a different note is reported only once it has been stable for a short, fixed time; tests show that a single stray buffer an octave off does not change the reported note, and that a real note change is reported within that time.
4. **It is pure, deterministic and allocation-free.**
   - Running the same input twice gives identical reports (a test).
   - A test measures `GC.GetAllocatedBytesForCurrentThread()` across the per-buffer call after warm-up and asserts 0 bytes; all buffers, including the FFT's, are allocated in the constructor.
   - The detector does no locking, logging or I/O (checked in code review).
5. **Pitch accuracy ≥ 95 % (±50 cents) on the corpus.**
   - Measured by E3-1's scorer over all generated and recorded clips together, with the default settings and `BufferSize` 64; a test asserts the target.
6. **Octave errors ≤ 1 %.** Same run and test.
7. **False notes ≤ 2 % on unvoiced windows.** Same run and test, over `silence`, `breath` and `noise` segments (`out-of-range` and `unclear` segments are left out by E3-1's rules).
8. **A measurement report on the corpus.**
   - A command in the corpus tool runs the detector over the corpus and prints a markdown report: per clip and in total, pitch accuracy, octave errors and false notes with their window counts, whether each target is met, and the average analysis time per buffer.
   - The report's figures are included in the PR and in this spec's Decisions when implemented.
   - A test checks that analysing the whole corpus runs faster than real time (total analysis time below the audio's duration); the time per buffer on the owner's Mac is reported, not asserted.
9. **The canonical check passes.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root, on macOS and in CI on Ubuntu.

## Tasks
- [x] Write the FFT and the YIN difference function, cumulative mean normalisation, threshold search and parabolic interpolation.
- [x] Write the pitch report type with note name, cents and confidence, and the note-name formatting.
- [x] Add the range from `LowestNote`/`HighestNote` and the new `PitchConfidenceThreshold` setting (settings, validator, `appsettings.json`, README).
- [x] Add the energy floor and the note-change hysteresis.
- [x] Plug the detector into E3-1's scorer and assert the three pitch targets.
- [x] Add the report command to the corpus tool.
- [x] Tests: generated tones at both sample rates, range ends, hysteresis, determinism, zero allocation, setting validation, faster than real time.

## Decisions
- **YIN** (owner's choice): named in the constitution, robust on voice and gives a natural confidence (1 − the normalised difference at the chosen lag). Own implementation, no package.
- **A fresh estimate every buffer, FFT-based** (owner's choice): the difference function is computed through an FFT so a new estimate every 1.3 ms is cheap enough; no hop adds latency. The FFT is written in the project because .NET 8 has none built in.
- **Confidence threshold plus a short note-change hysteresis** (owner's choice): the threshold is a setting so it can be tuned against the corpus; the hysteresis time and the energy floor are fixed constants chosen against the corpus and documented in code and in `corpus/README.md`. The configurable noise gate stays in E3-3, so the two features can be switched separately.
- **The report is a tool command, with the figures in the PR** (owner's choice): no committed results file to keep in sync.
- **Builds on E3-1:** the corpus, the reference model, the scorer, its window and boundary rules and the targets come from E3-1 and are not redefined here; the report command is added to the E3-1 corpus tool.
- **Window length** (default): long enough for two periods of the lowest configured note (about 25 ms for E2), rounded up to a power of two for the FFT; the exact size and the resulting analysis delay are documented. The window length sets the floor on the detector's latency, which the latency budget in E6-1 accounts for.
- **Range E2–C7, updated after E3-1** (owner's choice in E3-1): the owner's whistling lies at about C#6–C7, so E3-1 raised the default `HighestNote` to C7 and the corpus covers E2–C7; this spec's range and tests follow. E3-1 also added the `unclear` segment kind, left out of every measure, and reports per clip through `ScoreCounts`.
- **Cents relative to the nearest equal-tempered note at A4 = 440 Hz** (default).
- **`PitchConfidenceThreshold` default 0.8** (implementation): meets all three targets on the corpus with margin; valid values are 0 to 1.
- **Window and delay** (implementation): the window is two periods of the lowest note plus a sample, 1204 samples at 48 kHz for E2 (25.1 ms), so an estimate refers to a moment 12.5 ms back. The FFT is the next power of two (2048), zero-padded; every product in the correlation lies inside the window, so it never wraps. The correlation comes from one complex FFT that carries the integration window and the whole window as its real and imaginary parts, and one inverse.
- **Constants** (implementation): YIN's dip threshold is 1 − `PitchConfidenceThreshold` (0.2 by default): the first lag below it is taken and followed down to its local minimum, and without such a dip there is no note, so the confidence of every reported note is at or above the setting; an energy floor of −80 dBFS over the window; a ±50 cent margin around the range; a hold (`NoteHold`) of 10 ms and at least two estimates before a change of note, or between a note and no note, is reported, while movement within a semitone of the reported pitch passes at once. They are documented in code and in `corpus/README.md`.
- **The hysteresis is its own class, `NoteHold`** (implementation): pure and testable with constructed estimates; the detector feeds it every raw estimate.
- **Measurement figures** (Release build, owner's Mac, BufferSize 64, default settings): pitch accuracy **99.35 %** (60 733 of 61 133 voiced windows), octave errors **0.00 %** (2), false notes **0.19 %** (100 of 53 291 unvoiced windows). Per recording: singing 96.28 % / 0.03 % / 0.11 %, whistle 98.00 % / 0 % / 0.97 %, silence and breathing 0 false notes; the generated clips 99.99–100 %. Analysis time **104.5 µs per buffer** (7.8 % of a 1.33 ms buffer); a Debug build is about four times slower.
- **Two corrections to E3-1's references and rules** (finding while measuring): (1) the quiet, still periodic tail of a whistled or sung note after its loud stretch was labelled `silence`, so a detector hearing the tail correctly was charged with false notes (5.3 % on the whistle); the labeller now marks a note's periodic lead-in and tail `unclear`, and the references were relabelled (the note segments themselves did not change). (2) A note that drifts past the range's edge (the whistled C7 at 15.3 s reaches C7 +61) counted as missed although the detector rightly gives no note above C7 +50; the scorer now leaves out voiced windows whose reference pitch lies nearer a note outside the range. Both are documented in `corpus/README.md`, and the owner approved both after code review raised that they changed the measurement during implementation.
- **The corpus is scored in parallel in tests** (implementation): `CorpusScorer.Score` takes `parallel`; the tests use it (the whole corpus takes about 25 s in a Debug build instead of about 80 s), the report command does not, so its time per buffer is not inflated by other threads. Each clip has its own detector, so the figures are the same either way.
- **Scale tests split by waveform** (implementation): the sine and sawtooth scales at 48 and 44.1 kHz are two test classes, so xUnit runs them in parallel.
- **Clean-code pass** (implementation): the frequency/cents conversion lives once, in `Analysis.Pitch`, used by the detector and the corpus code; `PitchEstimate.Frequency` is derived from the cents; the FFT's inverse is unscaled and the 1/N is folded into the cross spectrum; only the FFT arrays' tails are cleared; the tests build the generated corpus once per sample rate. Not done: a half-size real-output inverse FFT (about 20 µs a buffer for noticeably more code, with the detector already at 8 % of real time), and moving the parallelism out of `CorpusScorer` into its callers (it would need a per-clip scoring API for no gain now).
- **Code review fixes:** the dip threshold follows `PitchConfidenceThreshold` and a window without a dip gives no note (before, the global minimum could pass the threshold at a subharmonic); `NoteHold` also needs two estimates, so a single stray estimate is held back with buffers of 512 or more too; the labeller searches an onset back to the previous note's loud stretch, not to the end of its quiet tail; a voiced window without a reference pitch counts as wrong instead of being dropped; the per-window range rule uses `CorpusRange.ContainsPitch`, which rounds halfway as `PitchEstimate.MidiNote` does; `PitchEstimate.Frequency` is 0 for no note.
- **The lag search starts below the range's top** (code review finding declined): criterion 2 says the detector searches only between `LowestNote` and `HighestNote`. It reports only pitches in that range, but its dip search starts at the shortest lags: starting at the top note's period would make a tone above the range show up as the subharmonic that falls inside it (a C8 as C7), which is the case criterion 2's test for a tone well outside the range rules out.
- **The per-window range rule stays in the scorer** (code review finding declined): splitting a drifting note's reference at the range edge would break the one-to-one match between reference notes and the note list that the corpus test relies on; the rule is documented in `corpus/README.md`.

## Open Questions
- None
