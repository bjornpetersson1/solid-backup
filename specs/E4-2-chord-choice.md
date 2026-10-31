# E4-2 · Chord choice

- **Issue:** [#12](https://github.com/bjornpetersson1/solid-backup/issues/12)
- **Labels:** `E4 Harmony`
- **Phase:** 4. Harmony
- **Depends on:** E4-1 (#11), E3-2 (#9)
- **Status:** Approved

## Summary
The harmony engine from `docs/harmony.md`: a buffer-based, allocation-free component that turns E3-2's pitch estimates into the current chord, following the common rules (candidates, keep, natural step, priority, hold times, phrase reset), with the styles Church (all seven modes), Campfire and Drone, their settings, and a command that prints the chord timeline for a WAV file.

## Goals
- Every timing, Church, Campfire and Drone example in `docs/harmony.md` passes as a test.
- An engine that E4-3 to E4-6 and E5-6 extend with a key source and further styles, without changing the common rules.
- Ready for the audio callback in E6-1: 0 bytes allocated per call.

## Non-goals
- Key detection and `auto` (E4-3): the engine takes the key as given. `Key` accepts `auto` in the settings, but the engine and the command need a fixed key until E4-3.
- The styles Pop, Modal (E4-4), Blues (E4-5), Free (E4-6) and Jazz (E5-6); choosing one of them gives a Swedish "not built yet" error, not a silent fallback.
- Voicing, register, sound, dynamics, the gate and onset-triggered change (E5).
- The menu choices for style, key and mode (E6-2) and the dashboard (E6-3).

## Acceptance Criteria
1. **Chords and keys as data.**
   - A chord is a root pitch class, a quality, its pitch classes and a symbol spelled as `docs/harmony.md` says (`C`, `Am`, `Bdim`, `C5`, …). A key is a tonic and one of the seven modes.
   - Tests: symbols and pitch classes for every quality used here, and for every root spelling `C Db D Eb E F F# G Ab A Bb B`.
2. **The settings.**
   - New settings in `appsettings.json`: `HarmonyStyle` (`Church`, default), `Key` (a tonic like `C`, `F#`, `Bb`, or `auto`; default `C`), `Mode` (`Ionian`, default, to `Locrian`), `MinNoteForChangeMs` (120), `MinChordDurationMs` (250), `PhraseResetMs` (2000).
   - Each is validated, with a Swedish error and a test per rule: a known style, a valid tonic or `auto`, a known mode, and positive, finite times. All are listed in the README settings table.
3. **The common rules.**
   - Per pitch estimate, the engine applies the rules in `docs/harmony.md` → Common rules: an immediate choice after silence, the two hold times while singing, silence keeping the chord, phrase reset, and the candidate, keep, natural step, priority and no-candidate steps.
   - Tests: the timing examples T1–T6, fed to the engine as sequences of pitch estimates at buffer intervals (64 frames at 48 kHz), and the same results at 44.1 kHz and with `BufferSize` 512.
4. **Church, Campfire and Drone.**
   - Church: the chord tables for all seven modes as in `docs/harmony.md`, strict Aeolian, and the Locrian tonic exception. Campfire: the major and minor sets. Drone: tonic root and fifth.
   - Tests: examples K1–K7, L1–L3 and D1, plus one test per mode that checks the chord set against the table.
5. **Allocation-free.**
   - All chords are built in the constructor. A test measures `GC.GetAllocatedBytesForCurrentThread()` across the per-estimate call after warm-up, over a melody with chord changes, and asserts 0 bytes.
6. **A chord timeline for a WAV file.**
   - A new command-line option (`--harmonize <wav>`, with optional `--style`, `--key`, `--mode` overriding the settings) runs E3-2's detector and the engine over the file and prints one line per chord change (time, note, chord), with Swedish messages and errors (missing file, a style not built yet, `auto` before E4-3).
   - Tests: the command on a generated WAV prints the expected chords, and the error cases give exit code 1 with a Swedish message.
7. **The checks pass.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` succeeds without audio hardware or network.

## Tasks
- [ ] Chord, quality, key and mode types, with symbols and pitch classes.
- [ ] The settings, their validation, `appsettings.json` and the README.
- [ ] The engine: per-estimate update, timing rules, phrase reset, and the shared choice steps.
- [ ] The Church, Campfire and Drone chord sets and priorities.
- [ ] Tests for the examples, the mode tables, both sample rates and buffer sizes, and the allocations.
- [ ] The `--harmonize` option and its tests.

## Decisions
- **Per estimate and allocation-free** (owner's choice): the engine takes every `PitchEstimate` from E3-2 and gives the current chord, like the analysis components, so E6-1 can run it in the audio callback unchanged.
- **A chord-timeline command** (owner's choice): lets the owner try the styles on their own singing before there is a synthesizer.
- **Default key C Ionian, Church** (owner's choice): `auto` cannot work before E4-3, which may switch the default to `auto`.
- **Time from the estimates** (default): the hold times and phrase reset use `PitchEstimate.TimeMs`, so a WAV gives the same result every time. A note's start is the first estimate that reports it, after E3-2's own note hold.
- **Styles as separate rule sets behind one interface** (default): each style supplies its chord set, priority order and choice rule, so E4-4 to E4-6 add styles without changing the engine. Styles not yet built are rejected with an error rather than mapped to another style.
- **Setting values in English, menu names in Swedish** (from `docs/harmony.md`): `Church`, `Campfire`, `Drone`, … are shown in the menu (E6-2) as Kyrko, Lägereld, Bordun, ….

## Open Questions
- None
