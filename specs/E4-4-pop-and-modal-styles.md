# E4-4 · Pop and Modal styles

- **Issue:** [#42](https://github.com/bjornpetersson1/solid-backup/issues/42)
- **Labels:** `E4 Harmony`
- **Phase:** 4. Harmony
- **Depends on:** E4-2 (#12); builds on E4-3 (#13)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 473 tests passed)

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
- [x] The new qualities and symbols.
- [x] The Pop rule set: sets, priorities, colours, borrowed chords.
- [x] The Modal rule set: tonic, character notes and chords per mode.
- [x] Tests for the examples and extra cases, the command and the allocations.

## Decisions
- **No new questions** (default): `docs/harmony.md` decides the rules; E4-2 decides the engine and settings, and E4-3 decides `auto`.
- **Colours count as chord changes for the timing rules** (from `docs/harmony.md`).
- **Pop builds on `ScaleRules`** (implementation): `ScaleRules` gained three hooks with neutral defaults — the style's chord a coloured chord was made from, a colour for the current chord (tried after keep, only on one of the style's chords), and borrowed chords (tried before the last keep). Church and Campfire are unchanged. A borrowed chord needs no successor list: Pop's priority starts with the tonic, so the tonic follows whenever it contains the note. The borrowed chords are the parallel key's triads, built per key in the constructor. Pop's priority list interleaves the major order (1, 5, 6, 4) and the minor one (1, 7, 6, 3), since each key uses only its own degrees.
- **Modal stands alone** (implementation): it is a pedal with one character chord, not a choice among degrees, so it implements `IChordRules` directly, with each key's tonic, character note and character chord built in the constructor.
- **Code review fixes:** a test shows a Pop colour waits for the hold times; dead successor code for borrowed chords was removed; the borrowed chords and Modal's chords are tables built once; the "uncolouring" of a chord moved from the public `Chord` type into a `ScaleRules` hook that only Pop overrides; Modal does not keep a chord that is neither of the key's two (after a modulation with `auto`) and returns to the tonic instead.
- **Modal "after silence" means with no chord to keep** (code review finding declined): `docs/harmony.md`'s common rules say silence keeps the chord and that a style chooses afresh only after a phrase reset; Modal's "after silence" line is read the same way, as common rule 5 reads it. After a short silence, a note that is neither the character note nor in the tonic triad keeps the chord.
- **Locrian's character note wins over the tonic triad** (code review finding declined): in Locrian the character note (b5) is also the tonic triad's fifth. The doc's table names it as the character note, so it gives the character chord (bV); otherwise Locrian's character chord could never sound.
- **PR review fix:** the layout of the per-key tables is defined once, on `Key` (`Key.Count`, `Key.Index`, `Key.All`), and used by `ScaleRules`, `PopRules` and `ModalRules`.

## Open Questions
- None
