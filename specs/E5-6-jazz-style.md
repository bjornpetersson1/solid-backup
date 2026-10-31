# E5-6 · Jazz style

- **Issue:** [#45](https://github.com/bjornpetersson1/solid-backup/issues/45)
- **Labels:** `E5 Sound`
- **Phase:** 5. Sound
- **Depends on:** E4-2 (#12), E5-4 (#17); builds on E4-5 (#43) and E5-5 (#18)
- **Status:** Approved

## Summary
The Jazz harmony style: diatonic seventh chords, secondary dominants, II–V–I into a new key on the second foreign note, and a tonic loop I–VI–II–V, two beats per chord on the tempo grid, that never stands still. The detailed rules and examples J1–J10 are added to `docs/harmony.md` with this spec.

## Goals
- The Jazz rules from `docs/harmony.md` as a rule set in E4-2's engine.
- Time-driven chord changes in the harmony (the loop and the II–V–I), on E5-5's tempo grid, without breaking the event-driven styles.

## Non-goals
- Other jazz devices: tritone substitutions, diminished passing chords, altered dominants, extensions beyond V7's 11th in the loop, walking bass, swing.
- Modes beyond major and minor (Jazz follows the common rule: Ionian, Lydian, Mixolydian count as major, the rest as minor).
- The menu (E6-2).

## Acceptance Criteria
1. **`docs/harmony.md` has the detailed Jazz rules with examples, approved by the owner.**
   - The Jazz section and the examples J1–J10 are in `docs/harmony.md`, approved in this spec's approval round; `docs/rhythm.md`'s Jazz paragraph matches.
2. **The Jazz examples are tests that pass.** One test per example J1–J10, named after it, through the harmony with the tempo grid (120 BPM unless the row says otherwise), checking the chord symbols, their times and the current key.
3. **The rules beyond the examples.**
   - Tests for: every chord of the major and minor sets; the secondary-dominant priority (each chromatic note on C major gives the expected dominant: C# → A7, D# → B7, F# → D7, G# → E7, Bb → C7); the modulation's nearest key with the flat side winning a tie; the key returning on a phrase reset and on an `auto` key change; a loop change never waiting for `MinChordDurationMs`; the loop's phase in 3/4.
   - The harmony gets the time even when no new note arrives, so the loop and the II–V–I advance in silence. The existing styles give exactly the same chords as before: the whole existing harmony test suite passes unchanged.
4. **Jazz is selectable.** `HarmonyStyle` accepts `Jazz`, `--style Jazz` works with `--harmonize` and `--accompany` (the grid from `Bpm` and `TimeSignature`, overridable with `--bpm` and `--time-signature` there), and the README lists the style. `--harmonize` prints the loop's changes and the key changes.
5. **No allocation.** Processing notes and time with Jazz allocates nothing after warm-up (a test, as for the other styles).
6. **The canonical check passes.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root, on macOS and in CI on Ubuntu.

## Tasks
- [ ] The rules and examples in `docs/harmony.md` and the Jazz paragraph in `docs/rhythm.md` (written with this spec).
- [ ] A time input to the harmony for styles that need it, from the tempo grid.
- [ ] The Jazz rule set: chord sets, choice, secondary dominants, modulation with II–V–I, the tonic loop, the key's return.
- [ ] `--style Jazz` in `--harmonize` and `--accompany`; README.
- [ ] Tests: J1–J10, the rules in criterion 3, allocation.

## Decisions
- **Two beats per loop chord, on every other beat of the grid** (owner's choice); the second chord on the first such beat at least one beat after the tonic chord.
- **The loop starts as soon as the melody is on the tonic** (owner's choice), replacing the earlier direction "while the melody rests or holds".
- **The loop never stops in silence** (owner's choice); only a new non-tonic note stops it. Hence the exception to `docs/rhythm.md`'s stop after `PhraseResetMs`.
- **A held tonic becomes V7's 11th** (owner's choice), so every loop chord contains the note.
- **Secondary dominant first, II–V–I on the second foreign note in a phrase** (owner's choice): on a major key every chromatic note fits a secondary dominant, so without a second-note rule the key would never move.
- **Minor: natural minor with the harmonic-minor V7** (owner's choice).
- **Secondary-dominant priority by target 5, 4, 6, 2, 3, 7** (default): the default priority without the tonic, so the strongest resolution wins.
- **The II–V–I is played through** (default): one chord per beat regardless of the melody, so the cadence is heard; it is the one place where Jazz does not contain the note.
- **Nearest key by scale** (default): as Blues moves by the circle of fifths, Jazz moves to the nearest key whose scale contains the note, the flat side winning a tie.
- **Builds on E4-5** (a style that moves the current key and returns on a phrase reset) **and E5-5** (the tempo grid).

## Open Questions
