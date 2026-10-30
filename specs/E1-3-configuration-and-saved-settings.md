# E1-3 · Configuration and saved settings

- **Issue:** [#3](https://github.com/bjornpetersson1/solid-backup/issues/3)
- **Labels:** `E1 Foundation`
- **Phase:** 1. Foundation
- **Depends on:** E1-1 (#1)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 42 tests passed)

## Summary
The app's defaults come from `appsettings.json`, and a user settings file outside the repo, written by the menu (E6-2), overrides them. A missing or invalid user settings file never stops the app: it falls back to the defaults and says so in Swedish.

## Goals
- A settings model with the four settings named in the constitution: pitch range, buffer size, gate threshold, latency target.
- A loader that reads `appsettings.json`, applies the user settings file on top, and validates the result.
- A saver that writes the user settings file, for the menu to call.

## Non-goals
- The menu and its choices (E6-2), and when the app calls the saver.
- Settings for later epics (devices, key, sound, rhythm, level features, recording); each issue adds its own.
- Environment variables, command-line overrides or secrets.
- Using the settings anywhere yet (pitch range in E3-2, buffer size in E2, gate in E3-3).

## Acceptance Criteria
1. **Defaults from `appsettings.json`.**
   - `src/SolidBackup/appsettings.json` holds: lowest note `E2`, highest note `C6`, buffer size, gate threshold in dBFS, latency target `50` ms. It is copied to the build output.
   - A test loads it and gets exactly those values.
2. **A missing or invalid user settings file falls back to the defaults with a Swedish message.**
   - Missing file: the defaults are used, with no message (a first start is normal).
   - Unreadable or malformed JSON: the defaults are used, and the loader returns a Swedish message naming the file and saying the defaults are used.
   - Valid JSON with an invalid value (e.g. lowest note above highest note, a note name that does not parse, a buffer size that is not a positive power of two, a gate threshold above 0 dBFS, a latency target ≤ 0): the whole file is ignored, the defaults are used, and the Swedish message names the invalid setting.
   - The loader never throws for a bad user settings file and never overwrites it.
   - Each case is covered by a test using a temporary directory.
3. **Saved choices are read back on the next start.**
   - A test saves settings that differ from the defaults, then loads with a new loader instance pointed at the same file, and gets the saved values.
   - Settings left out of the user file keep their defaults (the user file only overrides what it contains).
4. **The user settings file is git-ignored.**
   - The file lives at `~/Library/Application Support/solid-backup/usersettings.json`, outside the repo; the folder is created on save.
   - `.gitignore` also lists `usersettings.json`, so a copy in the repo is never committed (`git check-ignore usersettings.json` succeeds).
   - The path can be injected, so tests never touch the real file.
5. **Checks pass:** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test`.

## Tasks
- [x] Settings model (pitch range as note names, buffer size, gate threshold, latency target).
- [x] `appsettings.json` with the defaults, copied to the output.
- [x] Loader: defaults → user file on top → validation → settings plus an optional Swedish message.
- [x] Saver: writes the user settings file, creating the folder.
- [x] Note-name parsing for the pitch range (e.g. `E2`, `C6`, `F#3`).
- [x] `usersettings.json` in `.gitignore`.
- [x] Tests for every case above.

## Decisions
- **Location:** `~/Library/Application Support/solid-backup/usersettings.json`, the macOS convention, independent of the working directory and of the repo clone. (Owner's choice, the issue's open question.)
- **Library:** `System.Text.Json` with a small loader of our own; no new package, and it can write the file back. (Owner's choice.)
- **Invalid value: the whole user file is ignored**, with one Swedish message that names the problem. Simple and predictable; the file is left alone until the menu saves again. (Owner's choice.)
- **Contents now: the four settings in the constitution.** Later epics add their own. (Owner's choice.)
- **Default values** (default): pitch range `E2`–`C6` and latency target `50` ms from the constitution; buffer size `256` frames and gate threshold `-50` dBFS as placeholders until E2-1 and E3-3 measure them.
- **Pitch range as note names** (default): readable in the JSON and in the menu; parsed and validated by the loader.
- **The message is returned, not printed** (default): the caller decides how to show it (wiring the loader into startup is E6-2's, together with the menu), which keeps the loader testable.
- **Shape** (implementation): `AppSettings` is a record with `LowestNote`, `HighestNote`, `BufferSize`, `GateThresholdDbfs`, `LatencyTargetMs`, the same flat names as in the JSON. `SettingsStore(defaultsPath, userSettingsPath)` has `Load()` (settings plus an optional Swedish message) and `Save()`; `BundledDefaultsPath` and `DefaultUserSettingsPath` give the real paths. `NoteName.TryParseMidiNumber` parses `E2`, `F#3`, `Bb3` to MIDI numbers 0–127.
- **Override by JSON merge** (implementation): the user file's properties are laid over the defaults' JSON before deserialising, so a setting added later needs no merge code. Property names are case-sensitive and unknown properties are ignored, so a setting removed later does not invalidate old user files.
- **A wrong JSON type counts as an invalid value** (implementation): e.g. `"BufferSize": "stor"` names `BufferSize` in the message, like any other invalid value.
- **Invalid defaults throw** (implementation): a broken `appsettings.json` is an installation error, not a user mistake, so `Load` throws `InvalidOperationException` with a Swedish message (also for a missing or malformed file) instead of falling back.
- **`Save` validates, writes only changes, and writes atomically** (implementation and code review): invalid settings (including NaN or infinite numbers) throw `ArgumentException` and write nothing. Only settings that differ from the defaults are written, so a default changed later (buffer size after E2-1, gate after E3-3) still reaches a user who never chose it. The file goes through a uniquely named temporary file that is always cleaned up.
- **Pitch range must be strictly rising** (implementation): lowest equal to highest is invalid, since a one-note range cannot track a melody.
- **Strict note names** (code review): the octave must be `-1` or a single digit, so `E+2`, `E02` and overflowing octaves are rejected.
- **Any malformed user file falls back** (code review): a duplicate property counts as malformed JSON, and any deserialisation failure counts as an invalid value, so `Load` never throws for a bad user file.
- **Unreadable-file test needs a non-root user** (code review finding judged not applicable): the test makes the file unreadable with `chmod 000`, which root ignores. Tests run as a normal user on the owner's Mac and on GitHub's runner; `[UnsupportedOSPlatform("windows")]` is only there for the platform analyzer.

## Open Questions
- None
