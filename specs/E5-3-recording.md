# E5-3 · Recording

- **Issue:** [#16](https://github.com/bjornpetersson1/solid-backup/issues/16)
- **Labels:** `E5 Sound`
- **Phase:** 5. Sound
- **Depends on:** E2-2 (#6)
- **Status:** Approved

## Summary
Record a session's input (the voice) and output (the chords) to one stereo WAV file, written off the audio thread through a lock-free hand-over, so recording can never cause a dropout. `--passthrough --record` lets the owner try it before live mode exists.

## Goals
- A recorder the audio callback can feed without allocating, locking or doing I/O.
- One file per session that any audio program can play, with the voice and the chords in sync.

## Non-goals
- Playback in the app.
- Recording in live mode and the menu's recording switch (E6-1, E6-2); this issue builds the recorder and uses it with `--passthrough`.
- Other formats (MP3, AAC, 24-bit, float), compression, a separate mix file, splitting long sessions into several files.
- Resampling; the file has the session's sample rate.

## Acceptance Criteria
1. **A recording can be played back in another program.**
   - The file is a standard 16-bit PCM stereo WAV at the session's sample rate: the left channel is the input as the analysis receives it, the right channel the output as it is sent to the output device. Both channels start on the same buffer, so they stay in sync.
   - The header is written so the file is valid while recording (placeholder sizes) and has the right sizes when it is closed; a test reads a closed file back with the app's own `WavReader` and with a check of the raw RIFF header fields, and gets the samples that were written (within 16-bit quantisation).
   - The file is closed and its header finished when the session stops, also on `Ctrl+C`, and also when the audio fails while running.
   - Opening the file in QuickTime is a manual check for the owner.
2. **Recording does not cause dropouts.**
   - The audio thread only copies each buffer into a single-producer, single-consumer ring buffer allocated before the start; a background thread writes from it to the file. A test measures 0 bytes allocated per buffer on the audio-thread side, and the code has no `lock`, I/O or logging there.
   - If the writer falls behind and the ring is full, the audio thread drops the recorded buffer and counts a recording overrun; it never waits. A test with a deliberately slow writer shows that the audio side never blocks and that the overruns are counted.
   - The ring holds at least 2 s of stereo audio.
   - The overrun count is printed at the end of `--passthrough --record` together with the other counters, in Swedish.
   - An hour without dropouts while recording is checked live in E6-1; here it is a manual check for the owner with `--passthrough --record` for a few minutes, with all dropout and overrun counts 0.
3. **Where recordings go.**
   - The setting `RecordingFolder` (default `recordings`, relative to the current directory, or an absolute path) in `AppSettings` and `appsettings.json`, listed in the README. The folder is created if it is missing.
   - The file is named after the local start time, `yyyy-MM-dd_HH-mm-ss.wav`; if that name exists, `_2`, `_3` … is added.
   - `recordings/` is in `.gitignore`.
   - A folder that cannot be created or written gives a Swedish error before the audio starts, and exit code 1.
4. **The owner can try it now.**
   - `--record` together with `--passthrough` records the session. On start the app prints the file's path; at the end it prints the path and the length in seconds. `--record` without `--passthrough` gives a Swedish error and exit code 1.
   - `--help` lists `--record` with a Swedish description; tests check the help text and the error.
   - The recorder is a separate component that the live pipeline (E6-1) attaches the same way.
5. **The canonical check passes.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root, on macOS and in CI on Ubuntu.

## Tasks
- [ ] A streaming stereo 16-bit WAV writer with a header finished on close.
- [ ] The recorder: a ring buffer fed from the audio thread, a writer thread, the overrun counter, start and stop.
- [ ] Attach it to the pass-through pipeline (input and output buffers) behind `--record`.
- [ ] `RecordingFolder`, file naming, `.gitignore`.
- [ ] README: `--record`, the setting, where the files go.
- [ ] Tests: file format and round trip, header after a stop, allocation, a slow writer, naming, folder errors, command line.

## Decisions
- **One stereo file, voice left and chords right** (owner's choice): one file to keep track of, always in sync.
- **A folder in the repo, `recordings/`, git-ignored** (owner's choice), as a setting so it can be moved.
- **Try it with `--passthrough --record`** (owner's choice): the output is the microphone itself there, so both channels carry the voice; in E6 the right channel carries the chords.
- **16-bit PCM** (default): plays everywhere and matches the corpus; the analysis has no use for more depth here.
- **Drop recorded buffers rather than wait** (default): the audio must never wait for the disk; a lost recording buffer is counted, a dropout in the sound is never risked.
- **The extension of the existing `WavWriter`** (default): it writes whole mono files in one go; a streaming stereo writer is added beside it rather than changing it.
- **Builds on E2-2:** the pipeline's input and output buffers, the hand-over pattern and the counters.

## Open Questions
