# E4-6 · Free style

- **Issue:** [#44](https://github.com/bjornpetersson1/solid-backup/issues/44)
- **Labels:** `E4 Harmony`
- **Phase:** 4. Harmony
- **Depends on:** E4-2 (#12); builds on E4-3 (#13) and E4-5 (#43)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 521 tests passed)
- **Superseded:** E7-4 (#73) replaced Free's chords and rules: every three-tone chord instead of seventh chords with tensions, and a root rule. This spec describes Free before E7-4.

## Summary
The Free harmony style from `docs/harmony.md` as a rule set in E4-2's engine: no key, seventh chords with tensions on every root, chosen by common tones and voice leading from the previous chord.

## Goals
- Every Free example in `docs/harmony.md` passes as a test.
- Free works the same with a fixed key and with `auto`, since it ignores the key.

## Non-goals
- Changes to the common rules or to the other styles.
- Voicing of the tensions (E5).
- Jazz (E5-6), which uses seventh chords inside a key.

## Acceptance Criteria
1. **The chord vocabulary.**
   - Every root with `maj7`, `m7`, `7`, `m7b5` and `dim7` and the tensions each allows, as in the table in `docs/harmony.md`; symbols with the tension in brackets when the note is a tension (`Cmaj7(#11)`, `C7(b9)`, `Cm7(11)`).
   - Tests: the tones, tensions and symbols of every quality.
2. **The choice.**
   - After silence with no previous chord, the note as the root of a `maj7`; *keep* when the note is a tone or an allowed tension of the current chord; otherwise the ranking in `docs/harmony.md` (common tones, movement, quality, chord tone before tension, root interval), which picks exactly one chord.
   - Tests: examples F1–F3, plus cases where each step of the ranking decides.
3. **No key.**
   - Free ignores `Key` and `Mode`; the engine reports no current key. With `auto`, Free does not wait for E4-3's estimate: it chooses from the first note.
   - Tests: the same chords with two different fixed keys and with `auto`; no reported key.
4. **In the engine and the command.** Choosing `Free` no longer gives the "not built yet" error; `--harmonize --style Free` prints chords and `–` for the key (a test).
5. **Allocation-free.** All candidates are built in the constructor; the E4-2 allocation test covers Free: 0 bytes per call after warm-up.
6. **The checks pass.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` succeeds without audio hardware or network.

## Tasks
- [x] The seventh-chord qualities, tensions and symbols.
- [x] The Free rule set: start chord, keep, ranking.
- [x] Ignoring the key, including not waiting for `auto`.
- [x] Tests for the examples and extra cases, the command and the allocations.

## Decisions
- **No new questions** (default): `docs/harmony.md` decides the rules.
- **No waiting for `auto`** (default): Free has no key, so E4-3's "no chord until sure" has nothing to wait for.
- **No reported key** (default): the current key that E4-5 adds to the engine's output is empty for Free, shown as `–`.
- **A tension is part of the chord** (implementation): `Chord` gained an optional `Tension` (semitones above the root, none by default), included in its pitch classes, mask and symbol; the other styles never set it.
- **"Keep" takes a new tension** (code review, clarifying `docs/harmony.md`): the doc said both that a kept chord "stays unchanged" and that "when the note is a tension, the chord includes it" and always contains the note. The current chord now stays when the note is one of its tones or allowed tensions, and takes the note as its tension when it is one (`Cmaj7(#11)` then D gives `Cmaj7(9)`); a chord tone keeps the tension it has. `docs/harmony.md` step 2 was reworded to say so; the owner should confirm this reading.
- **Free needs no key** (implementation): `IChordRules.UsesKey` (true by default) lets the engine go on without `auto`'s estimate for Free, which also returns no `CurrentKey`; `--harmonize` shows `tonart –` and does not say that no key was found.
- **The ranking examples were found independently** (implementation): a separate Python model of the ranking found, from Cmaj7, a note where each step of the ranking is the first to decide; those cases are the extra tests.
- **Code review fixes:** a harmonizer for Free has no key estimator (before, `auto` still ran one, and its first key could cancel a pending chord change); a waiting change is never cancelled by a new key; `Chord.NoTension` is 0, so a default chord has no tension, and other tensions are rejected; the movement uses a per-choice table of distances to the previous chord; Free's qualities, tensions and tones are one table; `--harmonize` warns that `--key` and `--mode` are ignored with Free; a test shows Free gives the same chords with `auto` as with a fixed key over a whole melody.

## Open Questions
- None
