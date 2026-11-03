# E4-2 · Chord choice

- **Issue:** [#12](https://github.com/bjornpetersson1/solid-backup/issues/12)
- **Labels:** `E4 Harmony`
- **Phase:** 4. Harmony
- **Depends on:** E4-1 (#11), E3-2 (#9)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 403 tests passed)
- **Superseded in part:** E7-1 (#70) extends rule 2: with `ChordChangesPerBar` (default `2`) a ready note waits for an allowed beat of the tempo grid, and `ChordChangeSensitivity` scales both hold times. The behaviour below is `ChordChangesPerBar` `Off` with `Medium`.

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
- [x] Chord, quality, key and mode types, with symbols and pitch classes.
- [x] The settings, their validation, `appsettings.json` and the README.
- [x] The engine: per-estimate update, timing rules, phrase reset, and the shared choice steps.
- [x] The Church, Campfire and Drone chord sets and priorities.
- [x] Tests for the examples, the mode tables, both sample rates and buffer sizes, and the allocations.
- [x] The `--harmonize` option and its tests.

## Decisions
- **Per estimate and allocation-free** (owner's choice): the engine takes every `PitchEstimate` from E3-2 and gives the current chord, like the analysis components, so E6-1 can run it in the audio callback unchanged.
- **A chord-timeline command** (owner's choice): lets the owner try the styles on their own singing before there is a synthesizer.
- **Default key C Ionian, Church** (owner's choice): `auto` cannot work before E4-3, which may switch the default to `auto`.
- **Time from the estimates** (default): the hold times and phrase reset use `PitchEstimate.TimeMs`, so a WAV gives the same result every time. A note's start is the first estimate that reports it, after E3-2's own note hold.
- **Styles as separate rule sets behind one interface** (default): each style supplies its chord set, priority order and choice rule, so E4-4 to E4-6 add styles without changing the engine. Styles not yet built are rejected with an error rather than mapped to another style.
- **Setting values in English, menu names in Swedish** (from `docs/harmony.md`): `Church`, `Campfire`, `Drone`, … are shown in the menu (E6-2) as Kyrko, Lägereld, Bordun, ….
- **Scale-degree styles share one choice** (implementation): `ScaleRules` holds the common steps (keep, ordered successors, priority, no candidate); Church and Campfire only give their chord on each degree and their priority. Drone implements `IChordRules` directly. The rules take the key on every call, so E4-3 can change it without rebuilding anything.
- **Static arrays for the fixed tables** (implementation): the priority orders and chord intervals are `static readonly` arrays; a measurement showed that an abstract property returning a `ReadOnlySpan<int>` collection literal allocated on every call.
- **F# in Locrian** (implementation): `docs/harmony.md` spells every root `C Db D Eb E F F# G Ab A Bb B`, but its Locrian rows wrote `Gb`; the tables now say `F#`, matching the doc's own spelling rule (no rule changed).
- **`--harmonize` reads the file's sample rate** (implementation): the detector runs at the WAV's own rate with the settings' `BufferSize`, so a 44.1 kHz recording works without changing `SampleRate`. Its `--style`, `--key` and `--mode` are rejected without `--harmonize`, like `--input` and `--output` without `--passthrough` or `--devices`.
- **Code review fixes:** `--harmonize` together with `--passthrough` or `--devices` is rejected instead of silently dropped; the file's sample rate goes through the settings validation (44.1 or 48 kHz); a missing or empty path gives a Swedish "finns inte" message and .NET's English I/O text is no longer shown; the style and key checks live once, in `Harmonizer.Check`; the file is fed through `MemoryAudioInput` and an `IAudioInputHandler`; tonics need a capital letter like the other harmony settings; the printed note uses the chord roots' spelling (`Db4`, not `C#4`); the allocation test measures a fresh harmonizer, so every chord choice in the melody is counted; each style's chords for all 84 keys are built in its constructor, as criterion 5 says.
- **`Chord.PitchClassMask` is computed, not stored** (code review finding partly declined): it loops over at most three intervals without allocating; a stored field would go stale under `with`. `PitchClasses` stays a convenience list for display and tests, and the mask is documented as the form for the audio path.
- **PR review fixes:** `ChordRules.IsBuilt` answers which styles are built without building their tables, and `TryCreate` and `Harmonizer.Check` both use it; the tests' second sine generator was replaced by a `sampleRate` parameter on `TestSignal.Sine`.
- **Superseded by later issues** (release check): E4-3 made `auto` work and the default `Key` `auto` (criterion 2's default `C`, criterion 6's `auto` error and the "Default key C Ionian" decision no longer hold), and replaced `Harmonizer.Check` with `HasBuiltStyle`; E4-4 to E4-6 built Pop, Modal, Blues and Free, so only Jazz still gives the "not built yet" error. *Superseded by E5-6: Jazz is built, so no style gives that error; `ChordRules.IsBuilt`, `TryCreate` and `Harmonizer.HasBuiltStyle` gave way to `ChordRules.Create`, and the enum-name parser moved to `Configuration.SettingNames` (E5-1).* The code and tests follow the later specs.

## Open Questions
- None
