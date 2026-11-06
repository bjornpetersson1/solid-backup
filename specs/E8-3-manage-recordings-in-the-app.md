# E8-3 · Manage recordings in the app

- **Issue:** [#83](https://github.com/bjornpetersson1/solid-backup/issues/83)
- **Labels:** `E8 Playtest 2`
- **Phase:** 8. Playtest 2
- **Depends on:** none open (builds on E5-3 (#16), E6-2 (#20))
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 1200 tests passed); finding, hearing, pausing, seeking, renaming and deleting a live recording from the menu is a manual check for the owner

## Summary
A menu item, **Inspelningar**, lists the recordings in the recordings folder. From it you can listen to a recording (choosing the microphone, the chords or both, with pause and seeking), rename it or delete it, all without leaving the app.

## Goals
- List the recordings with name, date and length, newest first.
- Play a recording on the chosen output device. Choose the channels: both mixed to the middle, the microphone only, or the chords only. Pause, seek and stop.
- Delete with a confirmation, and rename with checks. Every error is a Swedish message, not a crash.

## Non-goals
- Editing audio (cutting, trimming, normalising), exporting to other formats.
- Folders other than `RecordingFolder`, and browsing the file system.
- Importing audio clips (E8-4).
- Playing back while live mode runs.
- Moving deleted recordings to the macOS trash.
- The crackle in recorded files (E8-2).

## Acceptance Criteria
1. **The list shows every recording in the recordings folder, newest first, and an empty list gives a clear message.**
   - The **Inspelningar** item in the main menu (Swedish) lists every `*.wav` in `RecordingFolder` (from the settings). Each row shows its name without `.wav`, its date and time (the file's last write time, local time) and its length in minutes and seconds. They are newest first, and a **Tillbaka** choice returns to the main menu.
   - An empty or missing folder shows `Inga inspelningar i <mapp>.` and goes back to the menu.
   - A file that cannot be read as WAV is listed with its length shown as `oläsbar`, and can still be renamed or deleted.
   - Menu test with a temporary folder: order, texts, empty case.
2. **Playback sends the file's samples unchanged to the output (tested with an in-memory playback), with a channel choice, pause, seeking and stop.**
   - Choosing a recording, then **Lyssna**, asks which channels to hear:
     - **Båda** (default): left and right averaged to mono;
     - **Bara mikrofonen**: left;
     - **Bara ackorden**: right.
   - A mono file skips the question and plays as it is.
   - An in-memory `IAudioPlayback` receives exactly the chosen channel's samples (or the average), in order: a test for each choice.
   - During playback a line shows position and length (`0:12 / 1:30`) and the keys:
     - Space pauses and resumes; while paused no samples are played;
     - ← and → seek 5 s back and forward, clamped to the start and the end;
     - q/Esc stop and return to the recording's choices.
   - Playback also ends by itself at the end of the file.
   - Tests drive the keys through the existing key-reading abstraction and assert the positions the playback plays from.
   - Playback goes to the output device chosen in the settings. The sample rate is the file's. A rate the device refuses gives a Swedish error.
3. **Delete removes the file only after confirmation. Rename changes the file name and refuses invalid characters and taken names, with a Swedish error.**
   - **Ta bort** asks `Ta bort <namn>? (j/n)`, with "no" as the default. "Yes" deletes the file and shows the list again; "no" leaves it.
   - **Byt namn** asks for the new name without `.wav` (added automatically), starting on the current name. Each of these is refused with a Swedish message and asked again:
     - an empty or blank name;
     - a name with `/`, `\`, `:` or another character not valid in a file name, or `.` / `..`;
     - a name already used by another recording in the folder.

     An empty answer after a refusal, or Esc, cancels.
   - Tests with a temporary folder cover delete with yes and no, a valid rename, each kind of refusal, and cancel.
4. **A missing or unreadable file gives a Swedish error, not a crash.**
   - A file deleted or made unreadable after the list was shown gives a Swedish error from **Lyssna**, **Byt namn** and **Ta bort**, and the list is read again.
   - An I/O or permission error on delete or rename gives a Swedish error.
   - Tests cover each.
5. **Nothing else changes.**
   - The main menu's other items, live mode and recording behave as before.
   - The playback callback allocates nothing (allocation test for the in-memory and the PortAudio playback's fill step).
   - `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` passes.
6. **Manual check (owner):** a recording made live can be found in **Inspelningar**, heard with each channel choice, paused, seeked, renamed and deleted from the menu.

## Tasks
- [x] A recordings list for `RecordingFolder`: name, date, length, unreadable files, sorted newest first.
- [x] Read one channel or the average of a stereo WAV file (`WavReader` today averages to mono only).
- [x] Extend playback with pause, a position and seeking: `IAudioPlayback` and `PortAudioPlayback`, plus an in-memory playback for tests. `synth --play` keeps working.
- [x] A playback screen: position line, keys (space, ←/→, q/Esc), end of file.
- [x] Delete with confirmation and rename with checks, with the Swedish texts.
- [x] The **Inspelningar** item in the main menu.
- [x] README: the menu paragraph mentions **Inspelningar**.

## Decisions
- **Choose channels at playback** (owner, recommended): both mixed to the middle by default, which sounds like live. The microphone or the chords alone are there for listening for faults (e.g. E8-2's crackle).
- **Pause and seek** (owner): space pauses, ←/→ seek 5 s, q/Esc stop. This needs a position and pause in `IAudioPlayback`, which `synth --play` does not use.
- **Only the recordings folder** (owner, recommended): every `*.wav` in `RecordingFolder`. E8-4 saves its results there too, so they show up here.
- **".wav" added automatically** (owner, recommended): you type only the name. Names that are empty, have invalid characters or are taken are refused.
- **The date is the file's last write time** (default): it also works for renamed files and files not named by the recorder.
- **Playback to the chosen output, also the built-in speakers** (default): playing a file feeds no microphone, so the speaker warning for live mode does not apply.
- **Delete without a trash can** (default): the file is removed with `File.Delete` after the confirmation. Moving it to the macOS trash would need a native call. It goes in Non-goals unless the owner wants it.

- **Where it lives** (implementation): `Recording.RecordingLibrary` lists, renames and deletes, and returns Swedish messages instead of throwing. `Cli.RecordingsMenu` is the sub-menu and `Cli.PlaybackScreen` the player with its position line. `MainMenu` takes how to open it (`Action<AppSettings>`, like its other seams) and shows **Inspelningar** after Inspelning.
- **Reading** (implementation): `WavReader.Read(path, WavChannel)` reads the left or the right channel, or both averaged. `WavReader.ReadInfo` gives the format and length from the header, so the list does not decode each file.
- **Playback** (implementation): `IAudioPlayback` has `Position`, `Paused` and `Seek`. Both `PortAudioPlayback` and the tests' `MemoryPlayback` play through `Audio.PlaybackCursor`. The cursor takes up a pause, a resume or a seek at the next buffer with a crossfade over it, so none of them clicks; a click from the player could otherwise not be told from crackle in the file. Seeks not yet taken up count in its position, so several in a row add up. Forward past the end does nothing. It allocates nothing (tested).
- **Renaming reads its own line** (code review): Spectre's text prompt has no Esc, and it reads its default value as markup, so a name with `[` crashed. Byt namn shows the current name escaped, then reads the new name key by key. Esc cancels, as does an empty answer or the same name.
- **Clean code:** reading a recording reuses `RecordingFiles.Read`, now with a channel, so its errors are the commands' (`Filen <path> kunde inte läsas…`). q stops the player by the character typed, as `StopKeys` does, and the key help is built from `SeekSeconds`. The player's poll interval can be set, so the tests do not wait.
- **Code review fixes:**
  - After listening, the recording's choices come back (as criterion 2 says), so another channel can be heard straight away. After renaming or deleting, the list comes back.
  - A recordings folder that cannot be read is a Swedish error (`Inspelningsmappen <mapp> kunde inte läsas.`).
  - The channel choices are an ordered list, with Båda first.
  - The stop keys are `StopKeys.IsStopKey`, shared with `synth --play`.
- **Declined code review findings:**
  - The name is checked twice when renaming, once in the menu to ask again and once in `RecordingLibrary.Rename`. `Rename` is public and must not move a file to a bad name on its own.
  - A recording is decoded whole before playing, as `WavReader` does everywhere. A recording of more than about 3 hours (2 GB of audio) is listed but gives the reader's Swedish error when played. Streaming playback is out of this issue's scope.
- **Ctrl+C** (implementation): in **Inspelningar**, as in the rest of the menu, Ctrl+C ends the app. Nothing is open that needs saving, and the playback closes with the process.
- **Ta bort asks `Ta bort <namn>? [j/n] (n)`** (implementation): Spectre's confirmation prompt with j and n, "no" as the default.

## Open Questions
- None.
