# E5-3 · Recording

- **Issue:** [#16](https://github.com/bjornpetersson1/solid-backup/issues/16)
- **Labels:** `E5 Sound`
- **Phase:** 5. Sound
- **Depends on:** E2-2 (#6)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 815 tests passed); playing a recording in QuickTime and a few minutes of `--passthrough --record` without dropouts are manual checks for the owner; CI on Ubuntu is checked on the PR

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
- [x] A streaming stereo 16-bit WAV writer with a header finished on close.
- [x] The recorder: a ring buffer fed from the audio thread, a writer thread, the overrun counter, start and stop.
- [x] Attach it to the pass-through pipeline (input and output buffers) behind `--record`.
- [x] `RecordingFolder`, file naming, `.gitignore`.
- [x] README: `--record`, the setting, where the files go.
- [x] Tests: file format and round trip, header after a stop, allocation, a slow writer, naming, folder errors, command line.

## Decisions
- **One stereo file, voice left and chords right** (owner's choice): one file to keep track of, always in sync.
- **A folder in the repo, `recordings/`, git-ignored** (owner's choice), as a setting so it can be moved.
- **Try it with `--passthrough --record`** (owner's choice): the output is the microphone itself there, so both channels carry the voice; in E6 the right channel carries the chords.
- **16-bit PCM** (default): plays everywhere and matches the corpus; the analysis has no use for more depth here.
- **Drop recorded buffers rather than wait** (default): the audio must never wait for the disk; a lost recording buffer is counted, a dropout in the sound is never risked.
- **The extension of the existing `WavWriter`** (default): it writes whole mono files in one go; a streaming stereo writer is added beside it rather than changing it.
- **Builds on E2-2:** the pipeline's input and output buffers, the hand-over pattern and the counters.
- **The recorder taps the pipeline** (implementation): `AudioPipeline` takes an optional `IAudioTap` that sees each input buffer and what went to the output; `SessionRecorder` is one. E6-1 attaches it the same way. Named `SessionRecorder`, as Spectre.Console has a `Recorder`.
- **Placeholder sizes are 0xFFFFFFFF** (implementation): the streaming convention for "to the end of the file", which the app's `WavReader` (E2-2) and common players accept, so a file is readable while it grows; closing writes the real sizes.
- **`recordings/` is ignored wherever the app runs, except the corpus** (code review): `RecordingFolder` is relative to the current directory, so `.gitignore` ignores any `recordings/` folder and re-includes `corpus/recordings/`, whose clips are tracked. The file keeps its CRLF line endings.
- **The recording is created once the devices are open** (implementation): before the audio starts, as the criterion asks, but after a device error could stop the run, so a failed start leaves no empty file.
- **A write failure is reported at the stop** (implementation): the writer thread keeps the error; stopping finishes what it can and the command reports "Inspelningen kunde inte skrivas." with exit code 1.
- **Ring of 2 s** (implementation): the ring is rounded up to a power of two (2^18 floats, about 2.7 s of stereo at 48 kHz).
- **Clean code:** the writer drains the ring straight from its one or two pieces and sleeps 50 ms in between (the audio thread never signals it, since that could make a system call); both WAV writers share one header and PCM conversion (`Pcm16Wav`); a write failure is a `RecordingFailedException`; the open/finish flow and its messages are `Cli.RecordingSession`, which E6-1's live command reuses; the recorder takes its rate and buffer size from the opened input, the same source as the pipeline.

- **Superseded by E6-1:** the recorder has one ring per side (each at least 2 s of mono audio), fed from the input's and the output's threads in live mode, or from one thread through `OnBuffer`; dropped buffers become silence in their place, the writer drops the lead of a side on a faster clock, and overruns count a pair dropped together once (`specs/E6-1-live-pipeline.md`).

## Open Questions
- **Code review fixes:**
  - a session whose audio fails to start throws its recording away (the file is deleted and nothing claims it was saved);
  - the pipeline is stopped in a `finally`, and the file is finished even when stopping the audio fails;
  - a recording that cannot write its header closes and deletes the file, and says nothing about recording;
  - any exception from the destination is a failed recording, reported at the stop, never a crash on the writer thread; `Dispose` never throws, so it cannot hide another exception;
  - a recording stops writing at the WAV format's 4 GB limit (about 6 hours of 48 kHz stereo) and says so, instead of wrapping its sizes;
  - an idle writer makes no empty writes; the shared WAV helper is `Pcm16Wav` (the reader already has a `WavFormat`); the writer thread is named in English.
- **Ctrl+C while the audio is starting** (code review finding declined): Ctrl+C becomes a clean stop only once the audio runs, so that it can still end the process while macOS's permission dialog blocks the start (E2-2). A recording ended there has no samples and keeps its "to the end of the file" sizes, which is still a valid, empty WAV that readers accept, so nothing is lost.
