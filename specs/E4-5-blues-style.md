# E4-5 · Blues style

- **Issue:** [#43](https://github.com/bjornpetersson1/solid-backup/issues/43)
- **Labels:** `E4 Harmony`
- **Phase:** 4. Harmony
- **Depends on:** E4-2 (#12); builds on E4-3 (#13)
- **Status:** Approved

## Summary
The Blues harmony style from `docs/harmony.md` as a rule set in E4-2's engine: I7, IV7 and V7, blue notes that keep the chord, and a current blues key that moves on a foreign note and returns on a phrase reset or when the menu's or `auto`'s key changes.

## Goals
- Every Blues example in `docs/harmony.md` passes as a test.
- The engine exposes the current key, so the dashboard (E6-3) can show a moved Blues key.

## Non-goals
- Changes to the common rules or to the other styles.
- Jazz's key moves (E5-6).
- Blues scales or seventh chords beyond I7, IV7 and V7.

## Acceptance Criteria
1. **Chords and choice.**
   - I7, IV7 and V7 of the current blues key, in major and minor alike, with priority 1, 4, 5 and the common successor table. A new chord quality `7` with the symbol `C7`.
   - Tests: examples B1 and B3, and a minor key giving the same dominant seventh chords.
2. **Blue notes.**
   - The minor third, diminished fifth and minor seventh above the current blues key's tonic keep the current chord, even when another chord contains them; after silence they give I7.
   - Tests: example B2, and a blue note after silence.
3. **Moving and returning the key.**
   - A note that is in none of the three chords and is not a blue note moves the current blues key to the nearest key on the circle of fifths in which I7, IV7 or V7 contains the note, the flat side winning a tie; the chord is then chosen in the new key with the common rules.
   - The key returns to the menu's key (or the current `auto` estimate) on a phrase reset or when that key changes.
   - The engine reports the current key with every chord.
   - Tests: examples B4–B6, a change of the `auto` estimate returning the key, and the reported current key in each case.
4. **In the engine and the command.** Choosing `Blues` no longer gives the "not built yet" error; `--harmonize --style Blues` prints chords and key moves (a test).
5. **Allocation-free.** The E4-2 allocation test covers Blues, including a key move: 0 bytes per call after warm-up.
6. **The checks pass.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` succeeds without audio hardware or network.

## Tasks
- [ ] The `7` quality and symbol.
- [ ] The Blues rule set: chords, blue notes, key moves and returns.
- [ ] The current key in the engine's output.
- [ ] Tests for the examples and extra cases, the command and the allocations.

## Decisions
- **No new questions** (default): `docs/harmony.md` decides the rules.
- **The current key is part of the engine's output for every style** (default): for the other styles it equals the menu's or `auto`'s key, so E6-3 reads one value.

## Open Questions
- None
