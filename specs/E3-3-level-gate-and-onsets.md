# E3-3 · Level, gate and onsets

- **Issue:** [#10](https://github.com/bjornpetersson1/solid-backup/issues/10)
- **Labels:** `E3 Analysis`
- **Phase:** 3. Analysis
- **Depends on:** E3-1 (#8)
- **Status:** Approved

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
- [ ] Write the RMS level component.
- [ ] Write the gate with hysteresis and hold.
- [ ] Write the level-rise onset detector.
- [ ] Add the four new settings (settings, validator, `appsettings.json`, README).
- [ ] Plug gate + pitch detector and the onset detector into E3-1's scorer and assert the two targets.
- [ ] Extend the report command.
- [ ] Tests: level values, gate behaviour, onsets on generated tones, settings validation, determinism, zero allocation.

## Decisions
- **"No note" means zero windows** (owner's choice): with the gate and the pitch detector combined, no window in a `silence` or `breath` segment may give a note. Stricter than the ≤ 2 % false-note target, which E3-2 meets without the gate.
- **Onsets from a level rise** (owner's choice): matches the constitution's "a sudden rise in level" and the corpus's one onset per articulated note, costs no FFT and adds no latency.
- **Every gate and onset parameter is a setting** (owner's choice): `GateHysteresisDb`, `GateHoldMs`, `OnsetThresholdDb` and `OnsetMinIntervalMs`, besides the existing `GateThresholdDbfs`, because microphones and rooms differ. The RMS window length and the onset rise time stay fixed constants.
- **Builds on E3-1 and E3-2:** the corpus, scorer, onset tolerance and onset matching rules come from E3-1; the combined "no note" check uses E3-2's detector unchanged; the report command from E3-2 is extended. E3-3 depends formally only on E3-1, so it is shipped after E3-2.
- **Switches belong to E5-2** (default): E5-2's scope is "one component per feature behind a common mechanism"; E3-3 delivers components that work alone, so E5-2 can switch them without changing them.
- **Onsets independent of the gate** (default): combining them (e.g. ignoring onsets while the gate is closed) is E5-2's decision; here the extra onsets in silence and breath are only reported.
- **Default values** (implementation): `GateThresholdDbfs` stays −50 unless the corpus shows it must change; the defaults for the new settings are chosen against the corpus and recorded here when implemented.

## Open Questions
- None
