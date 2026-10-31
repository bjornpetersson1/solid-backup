# E3-3 · Level, gate and onsets

- **Issue:** [#10](https://github.com/bjornpetersson1/solid-backup/issues/10)
- **Labels:** `E3 Analysis`
- **Phase:** 3. Analysis
- **Depends on:** E3-1 (#8)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 285 tests passed; 0 notes in silence and breath with the gate on, 96.58 % of onsets within 20 ms); CI on Ubuntu is checked on the PR

## Summary
Three separate, buffer-based level components: an RMS level meter in dBFS, a noise gate with hysteresis and a configurable threshold, and a level-rise onset detector. Measured on the E3-1 corpus: with the gate and E3-2's detector, silence and breath give no note, and at least 90 % of the reference onsets are found within 20 ms.

## Goals
- Level, gate and onsets as independent components that E5-2 can switch on and off and combine.
- All their parameters in `appsettings.json`, so they can be tuned to a microphone without a rebuild.
- The onset target in Measurement met on the corpus.

## Non-goals
- Using level, gate or onsets to shape the sound, the common switch mechanism and the on/off settings per feature (E5-2).
- Wiring the components into the live pipeline, the level meter on the dashboard (E6), or the menu (E6-2).
- Onsets from a pitch change without a level rise (legato); the corpus reference has one onset per articulated note.
- Spectral onset detection.
- Changing E3-2's detector; its own energy floor stays separate from the gate.

## Acceptance Criteria
1. **RMS level in dBFS.**
   - A level component reports, after every buffer, the RMS level in dBFS over a short sliding window (fixed length, documented), with digital silence reported as a fixed floor rather than −∞.
   - Tests: a full-scale sine reads −3.0 dBFS ± 0.1, a sine at −20 dBFS reads −23.0 ± 0.1, and silence reads the floor.
2. **A noise gate with hysteresis and a configurable threshold.**
   - The gate opens when the level reaches `GateThresholdDbfs` (existing setting) and closes only when the level has stayed below `GateThresholdDbfs − GateHysteresisDb` for `GateHoldMs`.
   - Tests: a level hovering just around the threshold does not make the gate chatter; a level dropping below the close threshold for less than the hold time keeps it open; a longer drop closes it; a changed threshold moves where it opens.
3. **Onset detection.**
   - An onset component reports an onset, with the time it refers to, when the level rises by at least `OnsetThresholdDb` within a short fixed time, and never two onsets closer than `OnsetMinIntervalMs`.
   - Tests on generated tones: a note starting from silence gives one onset within 20 ms of its start; a steady note gives none; repeated notes faster than the minimum interval give one onset per interval.
4. **The new parameters are settings.**
   - `GateHysteresisDb`, `GateHoldMs`, `OnsetThresholdDb` and `OnsetMinIntervalMs` are added to `AppSettings` and `appsettings.json`, validated with Swedish errors and tests (each must be positive and within a documented sensible range), and listed in the README settings table.
5. **Silence and breath noise in the corpus trigger no note.**
   - With default settings, the gate and E3-2's pitch detector combined (a note counts only when the gate is open and the detector reports a note), no window in any `silence` or `breath` segment of the corpus, generated or recorded, gives a note. Measured with E3-1's scorer; a test asserts zero.
6. **≥ 90 % of the reference onsets are found within 20 ms.**
   - Measured with E3-1's scorer over every onset in the corpus, generated and recorded, with default settings; a test asserts the target. Extra onsets are reported, with their count in silence and breath segments.
7. **Each component is separate, pure, deterministic and allocation-free.**
   - Level, gate and onsets are separate components with no dependency on each other beyond the gate and the onset detector reading a level value; each can be used without the others.
   - Running the same input twice gives identical output (a test), and a test asserts 0 bytes allocated per buffer after warm-up for each component; no locking, logging or I/O (code review).
8. **The corpus report includes level, gate and onsets.**
   - The corpus tool's report command (E3-2) also prints, per clip and in total, notes in silence and breath with the gate on, onset accuracy with its counts, and extra onsets; the figures are included in the PR and in this spec's Decisions when implemented.
9. **The canonical check passes.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root, on macOS and in CI on Ubuntu.

## Tasks
- [x] Write the RMS level component.
- [x] Write the gate with hysteresis and hold.
- [x] Write the level-rise onset detector.
- [x] Add the four new settings (settings, validator, `appsettings.json`, README).
- [x] Plug gate + pitch detector and the onset detector into E3-1's scorer and assert the two targets.
- [x] Extend the report command.
- [x] Tests: level values, gate behaviour, onsets on generated tones, settings validation, determinism, zero allocation.

## Decisions
- **"No note" means zero windows** (owner's choice): with the gate and the pitch detector combined, no window in a `silence` or `breath` segment may give a note. Stricter than the ≤ 2 % false-note target, which E3-2 meets without the gate.
- **Onsets from a level rise** (owner's choice): matches the constitution's "a sudden rise in level" and the corpus's one onset per articulated note, costs no FFT and adds no latency.
- **Every gate and onset parameter is a setting** (owner's choice): `GateHysteresisDb`, `GateHoldMs`, `OnsetThresholdDb` and `OnsetMinIntervalMs`, besides the existing `GateThresholdDbfs`, because microphones and rooms differ. The RMS window length and the onset rise time stay fixed constants.
- **Builds on E3-1 and E3-2:** the corpus, scorer, onset tolerance and onset matching rules come from E3-1; the combined "no note" check uses E3-2's detector unchanged; the report command from E3-2 is extended. E3-3 depends formally only on E3-1, so it is shipped after E3-2.
- **What E3-1 and E3-2 delivered, as used here** (spec check before implementing): criterion 5 reads `ScoreCounts.NotesInSilenceOrBreath`, which E3-1 added for it, under the scorer's rules (windows within 30 ms of a segment boundary and `unclear` attacks, decays and tails are left out); reference onsets exist only for notes in the range (E2–C7 by default); E3-2's report command is `report` in the corpus tool (`PitchMeasurement`, renamed `CorpusMeasurement` here).
- **Switches belong to E5-2** (default): E5-2's scope is "one component per feature behind a common mechanism"; E3-3 delivers components that work alone, so E5-2 can switch them without changing them.
- **Onsets independent of the gate** (default): combining them (e.g. ignoring onsets while the gate is closed) is E5-2's decision; here the extra onsets in silence and breath are only reported.
- **Default values, chosen against the corpus** (implementation): `GateThresholdDbfs` −42 (was −50), `GateHysteresisDb` 3, `GateHoldMs` 20, `OnsetThresholdDb` 15, `OnsetMinIntervalMs` 100. The corpus showed the gate had to change. With −50 / 6 dB / 50 ms, 100 windows in silence still gave a note: the faint, still pitched tails of notes at −51 to −57 dBFS, which the gate held open. −45 / 3 / 20 left 1, and −45 / 3 / 10 and −42 / 3 / 20 left none; −42 / 3 / 20 was chosen (in the clean-code pass, over −45 / 3 / 10) because it lies inside that zero region rather than on its edge and keeps a hold longer than the level window. The quietest real note in the corpus is about −32 dBFS, 10 dB above the threshold. The values are tuned on the corpus the test asserts on, so they are a starting point for the owner's microphone, not a proof for every room. A lower onset threshold (10 or 12 dB) or a longer rise window (30 or 50 ms) did not raise onset accuracy and added extra onsets, so 15 dB within 20 ms stays.
- **Constants** (implementation): the level is the RMS over the last 10 ms (`LevelMeter.WindowMs`), with −120 dBFS for digital silence; an onset is a rise of `OnsetThresholdDb` above the lowest level of the last 20 ms (`OnsetDetector.RiseMs`), fired when the rise begins and not again while it lasts, timed at the middle of the level window. Until 10 ms of input has arrived the level is the RMS over all of it, so the level starts at the room's own level instead of rising from nothing, which would otherwise read as an onset at the start.
- **One measurement pass for all of E3** (implementation): `DetectorReport` gained `GateOpen` (default true). The scorer counts notes in silence and breath only while the gate is open, and the other pitch measures ignore the gate, so one run gives E3-2's and E3-3's figures. `ScoreCounts` gained `ExtraOnsetsInSilenceOrBreath`, and `MeasurementTargets` gained `MaxNotesInSilenceOrBreath` (0). E3-2's `PitchMeasurement` and `PitchCorpusDetector` became `CorpusMeasurement` and `AnalysisCorpusDetector`, which feed every buffer to the pitch detector, the level meter, the gate and the onset detector, made from the settings. The report has a second table for level, gate and onsets.
- **Measurement figures** (Release build, owner's Mac, BufferSize 64, default settings): **0** notes in 48 401 silence and breath windows with the gate on; onsets found within 20 ms **96.58 %** (254 of 263); the gate cuts off **0** correct notes. Per clip, all generated notes and every sung onset (19 of 19) are found, but only 5 of 14 whistled onsets: the owner's whistled attacks start with a breathy rise of about 13 dB in 20 ms and reach the tone 30–45 ms later, so the detector fires too early or not at all. There are 25 extra onsets, 21 of them in silence or breath (6 in the breathing clip). The pitch figures are unchanged from E3-2: 99.35 % / 0.00 % / 0.19 %. The analysis time with all four components is 105–120 µs per buffer between runs.
- **Clean-code pass** (implementation): one `Duration.Samples` helper converts ms to samples for the hold, gate and onset timings; `NoiseGate`'s state is private; the onset counting is one loop; the report and setting text take the 20 ms from `MeasurementTargets` and `OnsetDetector`; the pitch detector tests use the shared `TestSignal`.
- **Code review fixes:**
  - The onset detector always compares with the previous buffer's level, so buffers longer than the 20 ms rise time (1024 and up) still find onsets.
  - An onset is timed at the middle of the buffer that crossed the rise, or of the level window if that is shorter, since a sudden attack has only just entered the level window.
  - The gate's hold runs from the first low reading, so one low reading never closes it, whatever the buffer length.
  - The validator rejects a gate whose close level lies at or below the meter's −120 dBFS floor, where it could never close.
  - The level meter recomputes its running sum once per window, so rounding cannot build up over a long session.
  - The gate's tests now also cover determinism and allocation.
  - In the measurement, each pitch estimate is paired with the gate's state at the moment the estimate refers to, not the latest one.
  - The report counts the correct notes the gate cuts off (`NotesCutByGate`), the other side of the zero-notes trade-off: it is 0 at −42 dBFS.

## Open Questions
- None
