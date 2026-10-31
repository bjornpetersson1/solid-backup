# E4-4 · Pop and Modal styles

- **Issue:** [#42](https://github.com/bjornpetersson1/solid-backup/issues/42)
- **Labels:** `E4 Harmony`
- **Phase:** 4. Harmony
- **Depends on:** E4-2 (#12); builds on E4-3 (#13)
- **Status:** Approved

## Summary
The Pop and Modal harmony styles from `docs/harmony.md`, as two more rule sets in E4-2's engine: Pop with its major and minor sets, add9/sus4 colours and borrowed chords; Modal with the tonic pedal and each mode's character chord.

## Goals
- Every Pop and Modal example in `docs/harmony.md` passes as a test.
- `HarmonyStyle` `Pop` and `Modal` work in the engine and in `--harmonize`, with a fixed key and with `auto`.

## Non-goals
- Changes to the common rules or to Church, Campfire and Drone.
- Blues, Free and Jazz (E4-5, E4-6, E5-6).
- Voicing of the colours (E5).

## Acceptance Criteria
1. **Pop.**
   - Major I, V, vi, IV with priority 1, 5, 6, 4; minor i, bVII, bVI, bIII with priority 1, 7, 6, 3. Ionian, Lydian and Mixolydian count as major, the other modes as minor.
   - The add9 and sus4 colours, after *keep* and before *natural step*, only on a chord from the set; a coloured chord contains its colour note; a colour change follows the hold times.
   - Borrowed chords when nothing in the set contains the note: bVII, iv, bVI in major, V, IV in minor, in that order; a borrowed chord's successor is the tonic.
   - New chord qualities `add9`, `madd9` and `sus4` with the symbols in `docs/harmony.md` (`Cadd9`, `Amadd9`, `Csus4`).
   - Tests: examples P1–P5, and the colour, borrowed and minor rules each in at least one more case.
2. **Modal.**
   - The tonic triad and the character note and chord for each of the seven modes, as in the table in `docs/harmony.md`; with `auto`, Ionian or Aeolian.
   - Tests: examples M1–M3, and one test per mode for its character chord and its return to the tonic.
3. **Both styles in the engine.** Choosing `Pop` or `Modal` no longer gives the "not built yet" error; `--harmonize --style Pop|Modal` prints their chords (a test each).
4. **Allocation-free.** The E4-2 allocation test covers Pop and Modal: 0 bytes per call after warm-up.
5. **The checks pass.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` succeeds without audio hardware or network.

## Tasks
- [ ] The new qualities and symbols.
- [ ] The Pop rule set: sets, priorities, colours, borrowed chords.
- [ ] The Modal rule set: tonic, character notes and chords per mode.
- [ ] Tests for the examples and extra cases, the command and the allocations.

## Decisions
- **No new questions** (default): `docs/harmony.md` decides the rules; E4-2 decides the engine and settings, and E4-3 decides `auto`.
- **Colours count as chord changes for the timing rules** (from `docs/harmony.md`).

## Open Questions
- None
