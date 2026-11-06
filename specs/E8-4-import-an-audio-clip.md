# E8-4 · Import an audio clip

- **Issue:** [#84](https://github.com/bjornpetersson1/solid-backup/issues/84)
- **Labels:** `E8 Playtest 2`
- **Phase:** 8. Playtest 2
- **Depends on:** E8-3 (#83) for the recordings list and playback; builds on E6-2 (#20) and the `accompany` command
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 1244 tests passed, the afconvert integration test included on macOS); importing an iPhone voice memo and hearing it accompanied is a manual check for the owner

## Summary
A menu item, **Importera klipp**, takes an audio file by path: a WAV file, or anything macOS can convert, such as an iPhone voice memo. It then lets you choose, for this run only, the settings and features to apply, and runs the clip through the same chain as live mode. The result is saved as a recording (clip left, chords right), so it can be heard at once and later under **Inspelningar** (E8-3).

## Goals
- Import WAV in any common rate, bit depth and channel count, and M4A/AAC, MP3, AIFF and CAF through macOS's built-in `afconvert`.
- Choose the run's settings starting from the saved ones, without changing them: key, style, sound, rhythm, chord changes, level features and autoplay.
- Offline processing through the same pure chain as `accompany` and live mode, so the result is what live mode would play.
- Save the result as a stereo recording, then play it straight away with E8-3's player.

## Non-goals
- Polyphonic input, or separating a melody from a full mix (the clip must be one melody line, as live).
- Editing or trimming the clip, choosing a part of it.
- Monitoring, devices and buffer size as run settings (they are about live audio).
- Changing the `accompany` command's behaviour or options.
- Clips longer than 20 minutes (see Decisions).

## Acceptance Criteria
1. **A WAV file in any common sample rate, bit depth and channel count is imported. Formats not supported give a Swedish error, not a crash.**
   - The path is typed or dragged in from Finder. Surrounding quotes and backslash-escaped spaces are handled, and `~` is expanded.
   - WAV:
     - 16-, 24- and 32-bit PCM and 32-bit float, mono or stereo, at 8–96 kHz, are read;
     - stereo is averaged to mono;
     - the file's own sample rate is used, as in `accompany`.

     Tests cover each format with generated files.
   - `.m4a`, `.aac`, `.mp3`, `.aif`/`.aiff` and `.caf` are converted with `/usr/bin/afconvert` to a temporary mono WAV at `SampleRate` (from the settings). The conversion runs locally, with no network, and the temporary file is deleted afterwards. This is the only process the app starts. Tests use a fake converter. One integration test runs `afconvert` on a generated AIFF and is skipped when `afconvert` is missing.
   - Each of these gives a Swedish error and returns to the menu, tested:
     - a missing file;
     - an unknown extension;
     - a WAV with more than 2 channels or an unsupported encoding;
     - an `afconvert` failure, including `afconvert` missing;
     - an empty clip;
     - a clip over 20 minutes.
2. **Each feature can be switched on or off for the run, and switching one off changes the result only in that feature (tested on a corpus clip).**
   - After the file, a run menu (Swedish) shows the run's settings and lets you change, in the main menu's prompts: key, style, sound, rhythm (pattern, tempo, meter), chord changes, level features and autoplay. **Kör** starts the run and **Avbryt** goes back.
   - The saved settings file is unchanged after a run (test).
   - On a corpus clip, a test shows for each level feature and for autoplay that switching it off in the run menu gives the same result as `accompany` with only that feature off, and that the result differs from the run with it on.
3. **The result for a clip equals what `accompany` gives with the same settings.**
   - The saved file is stereo 16-bit at the clip's rate, so the recordings player can choose channels: the clip on the left, the chords on the right.
   - The right channel equals `accompany --no-voice` with the same settings, within 16-bit rounding, and the left equals the clip (test).
4. **The result can be heard in the app and saved; the saved file plays in another program.**
   - The result is saved in `RecordingFolder`, named `<clip name>_<yyyy-MM-dd_HH-mm-ss>.wav` (with `_2`, … if it exists), and the path is shown in Swedish.
   - The app then offers **Lyssna**, using E8-3's player (channel choice, pause, seek), and the result appears under **Inspelningar**.
   - The header test shows a standard stereo PCM WAV.
   - Rendering runs off any audio thread, with a Swedish progress line, and Esc cancels it without leaving a file.
5. **Nothing else changes.**
   - Live mode, the main menu's other items, `accompany` and E8-3 behave as before.
   - `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` passes, and the tests use no network.
6. **Manual check (owner):** a melody recorded on another device (e.g. an iPhone voice memo) is imported, accompanied with chosen settings and heard from the menu.

## Tasks
- [x] Path parsing (quotes, escaped spaces, `~`).
- [x] WAV import for 16/24/32-bit PCM and 32-bit float at any rate, mono or stereo, extending `WavReader` where needed.
- [x] A converter abstraction with an `afconvert` implementation (temporary file, deleted after) and a fake for tests.
- [x] A run-settings menu that reuses the main menu's prompts on a copy of the settings and does not save.
- [x] Rendering through `AccompanyCommand.Accompany`'s chain, with progress and cancel, into a stereo result (clip, chords).
- [x] Save to `RecordingFolder` with a unique name, then offer E8-3's playback.
- [x] The **Importera klipp** item in the main menu, and the Swedish texts.
- [x] README: the menu paragraph mentions **Importera klipp** and the supported formats.

## Decisions
- **WAV plus what macOS can read** (owner, recommended): `/usr/bin/afconvert` ships with macOS and runs locally. No new NuGet dependency and no network call. It is the only external process the app starts.
- **Path typed or dragged in** (owner, recommended): dragging a file from Finder into the terminal pastes its path. No file-browser UI.
- **Run settings only for the run** (owner, recommended): the run starts from the saved settings and does not write them, so live mode is unaffected.
- **Saved as a recording** (owner, recommended): stereo, clip left and chords right, in `RecordingFolder`, so E8-3's list and channel choice work on it. This departs from `accompany`'s mono output, but the right channel equals `accompany --no-voice`.
- **The clip's own rate for WAV, `SampleRate` for converted files** (default): this matches `accompany`, which uses the recording's rate. Converted files get the app's configured rate.
- **At most 20 minutes** (default): the whole clip and result are held in memory (about 0.5 GB of floats at 96 kHz stereo for 20 minutes). Longer clips are refused with a Swedish message.
- **The result's name** (default): the clip's name plus a timestamp, so imports are recognisable in the list and never overwrite anything.

- **Where it lives** (implementation): `Import.ClipPath` parses the typed path. `Import.ClipImporter` loads WAV, or converts through an `Import.IAudioConverter` (`AfconvertConverter`, or a fake in tests); every failure is an `ImportFailedException` with a Swedish message. `Import.ClipRecording` saves the result through `Recording.RecordingFile` (now with a name prefix) and `StereoWavWriter`. `Cli.ImportMenu` is the menu, and `MainMenu` opens it as an `Action<AppSettings>`, after Inspelningar.
- **The main menu's prompts are shared** (implementation): key, style, sound, rhythm, chord changes, level features and autoplay moved from `MainMenu` to `Cli.SettingPrompts`, with the rows of the settings table. The run menu uses the same prompts and rows.
- **Rendering** (implementation): `AccompanyCommand.Accompany` has an overload with a progress callback and a cancellation, through a tap on the pipeline that stops the memory input once cancelled. The menu runs it on a worker thread and shows `Spelar in ackorden till klippet … 42% (Esc avbryter)`. Keys pressed before the run starts count too, so Esc is never lost to a quick run. Nothing is saved when it is cancelled.
- **WAV formats** (implementation): `WavReader` now also reads 24- and 32-bit PCM, so imports and every other WAV read take them.
- **afconvert** (implementation): `afconvert -f WAVE -d LEI16@<SampleRate> -c 1 <clip> <temporary>.wav`, with a 2-minute timeout. A missing program, a failure (with afconvert's own message) or a timeout is a Swedish error. The temporary file is deleted in every case. The integration test is an `AfconvertFact`, skipped where `/usr/bin/afconvert` is missing, as on CI's Linux.
- **Tests on the corpus** (implementation): the menu tests import the first six seconds of the corpus's singing followed by two seconds of silence, in C Ionian with every level feature and autoplay on, so each switch has something to change.

- **WAV at another rate is converted** (owner, during code review): the app runs only at 44.1 and 48 kHz (`SettingsValidator.SupportedSampleRates`), so a WAV at any other rate could be loaded but never run. A WAV at 44.1 or 48 kHz is read at its own rate, as before. Any other WAV goes through afconvert to `SampleRate`, like the other formats, and its result is saved at that rate. This departs from criterion 1's "the file's own sample rate is used" for those rates.
- **Code review fixes:**
  - A WAV's length is checked from its header before it is decoded, so a clip of hours is refused at once, not after loading it into memory.
  - From the path on, Ctrl+C cancels the import rather than ending the app; the main menu's quit is off meanwhile. A conversion is stopped and its temporary file deleted, and nothing half-written is left.
  - A conversion runs on a worker thread with `Läser in klippet … (Esc avbryter)`, and Esc stops it. A WAV at the app's rate is read at once, without it.
  - Any failure of the worker that renders the chords is a Swedish error that returns to the menu, not a crash.
  - The progress line ends at 100 %.
  - The result's name keeps at most 80 characters of the clip's name.
  - `ClipPath` keeps an escaped trailing space and undoes the shell's `'\''` inside single quotes.
  - Any process error from afconvert, or a temporary file that cannot be deleted, is a Swedish error or is ignored, and never escapes the menu.
  - The afconvert check for the integration test is in `AfconvertFact` only.
  - Conversion through the menu has no test of its own: while converting, the menu reads every key looking for Esc, and Spectre's test console cannot wait for keys pressed later. `ClipImporterTests` covers the conversion, and the menu's worker loop is the one the rendering tests drive.
- **Clean code:**
  - Cancellation and progress go through `MemoryAudioInput`, which already owns the loop over the buffers, rather than a tap reaching back into the input.
  - `StopKeys.Pressed` is shared.
  - `RecordingLibrary.Entry` builds a list entry for a path, in the list and after a save.
  - afconvert's standard output is not read, so the timeout can act on a hung conversion.
  - The tests run the menu's flow on a two-second clip and compute the all-on chords once.
- **Declined clean-code findings:**
  - **Shared "Filen … finns inte / kunde inte läsas" messages with `RecordingFiles`:** that helper is in `Cli` and the importer is not, and the importer names the clip chosen rather than its temporary WAV.
  - **Dropping `AfconvertConverter`'s `program` parameter:** the test of a missing afconvert uses it.
  - **Replacing the keys the tests press from the `now` callback with a gated console input:** Spectre's `TestConsole` takes its input as it is. Saving the result is the one moment between rendering, which reads every key looking for Esc, and the prompt after it, and the callback is commented as such.

## Open Questions
- None.
