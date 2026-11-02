# E6-1 · Live pipeline

- **Issue:** [#19](https://github.com/bjornpetersson1/solid-backup/issues/19)
- **Labels:** `E6 Live mode`
- **Phase:** 6. Live mode
- **Depends on:** E5-2 (#15), E5-3 (#16), E5-5 (#18), E4-3 (#13)
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 941 tests passed); the latency target on a low-latency microphone and an hour without dropouts are manual checks for the owner

## Summary
Live mode: the microphone's input runs through analysis, the level features and the harmony on the input stream's thread, and the rhythm pattern and the synthesizer play the chords on the output stream's thread, in real time. The two sides and the dashboard meet only through lock-free hand-overs, and the app measures its own latency and dropouts. `--live` runs it until the menu (E6-2) and the dashboard (E6-3) exist.

## Goals
- The whole chain playing live, with the same behaviour as `--accompany` on a recording.
- No way for the console, the GC or input jitter and clock drift to cause a dropout in the chords.
- A latency figure and a dropout count that the dashboard (E6-3) can show and the owner can check against the targets.
- One live-mode entry point that E6-2's menu calls.

## Non-goals
- The menu and its choices (E6-2); here the settings come from the saved settings plus the one-run options of `--live`.
- The dashboard (E6-3). E6-1 provides the snapshot it reads and prints only a plain status line.
- Changing settings while playing.
- Acoustic end-to-end latency (a loopback cable); latency uses PortAudio's reported device latencies, as in Measurement.
- Resampling to follow clock drift, echo cancellation, more than one input channel.
- Changing `--passthrough`, which keeps its sample hand-over.

## Acceptance Criteria
1. **The chain runs live, split over the two streams.**
   - On each input buffer, the input thread runs the analysis (pitch, level, gate, onsets), the level features that are on and the harmony, and publishes the result (chord, sounding, strike, shape, strength) to the output side.
   - On each output buffer, the output thread takes the latest published result, plays it through the rhythm pattern and renders the synthesizer straight into the output buffer. No samples are handed over between the streams.
   - A strike (a new onset or chord change) published by the input is played by the next output buffer even when several input buffers arrive before it (input comes in bursts); a test publishes two input results, the first with a strike, before one output buffer and sees the strike played.
   - The rhythm pattern's grid counts the output stream's samples, so sample 0 of the output when live mode starts is beat 1 of bar 1 (`docs/rhythm.md`).
   - With an in-memory input and output that alternate buffer by buffer, the live chain gives the same output, sample for sample, as `--accompany --no-voice` on the same WAV with the same settings, delayed by at most one buffer (a test with a corpus clip and with a generated melody).
2. **The latency target (< 50 ms) is met on the owner's Mac.**
   - Latency = the input stream's reported input latency + the chain delay + one buffer for the hand-over + the output stream's reported output latency. The device latencies are read from the opened PortAudio streams.
   - The chain delay is measured when live mode starts: a generated tone (A3 sine at −20 dBFS after 500 ms of silence) runs offline through a fresh copy of the chain with the chosen settings, but with the Held pattern (the step wait of Arpeggio and Strum is not latency, `docs/rhythm.md`). It is the time from the tone's first sample to the first output sample above −60 dBFS, i.e. when the chord's envelope has started; the attack is the sound's character, not delay. The run allocates its buffers before live audio starts and takes under 100 ms.
   - If the tone never produces a chord (e.g. a gate threshold above −20 dBFS), live mode still starts and the chain delay is shown as unknown (`?`), in Swedish.
   - Tests: the chain delay is deterministic for given settings, starts on the same sample for Pad, Organ and Pluck, with the Pad's slow attack reaching −60 dBFS at most `SoundPreset.MinEdgeMs` (5 ms) after the others, and with the defaults lies between half the pitch window and 50 ms.
   - The parts and the total are in the snapshot (criterion 4), are printed on the status line and at the end, and the total is marked as over target when it is ≥ `LatencyTargetMs`.
   - Meeting < 50 ms on the owner's Mac with Held and a low-latency microphone is a manual check for the owner; with the built-in microphone the target is expected to be missed (it reports ~45 ms alone, README "Audio on macOS").
3. **An hour of play gives no dropouts.**
   - The dropout count is Measurement's definition: input overflows and output underflows reported by PortAudio. A live-mode result that cannot be handed over is never a dropout, since the output always plays the latest result it has.
   - Neither callback allocates, locks or does I/O: a test runs the input side and the output side with the default settings and every level feature on, over a corpus clip and with every rhythm pattern, and measures 0 bytes allocated per callback after the first; the code has no `lock`, I/O or logging on either side.
   - The analysis time per input buffer and the render time per output buffer are measured on the owner's Mac (Release build, defaults) and recorded in Decisions; together they stay under one buffer's duration.
   - Recording (`--record`) writes the input from the input thread and the output from the output thread through lock-free rings without waiting; the file still has the voice left and the chords right, starting together, and recording overruns are counted and printed separately (they are not dropouts).
   - An hour of play with the count staying at 0, with and without recording, is a manual check for the owner.
4. **Lock-free hand-over of state to the dashboard.**
   - After each input buffer the input side publishes a snapshot: the note estimate (note name and cents, or none), the level in dBFS, whether the gate is open, the chord, the current key (or none, e.g. for Free), which level features are on, the rhythm pattern, the latency parts and total, and the dropout counts. The output side adds its own counts.
   - A reader on another thread gets a consistent snapshot without locking, and never blocks or slows the writer: if a write happens while it reads, it retries or takes the previous one. A test reads snapshots on another thread while a writer publishes as fast as it can, and every snapshot read is one that was written (no torn fields).
   - Publishing allocates nothing (part of the allocation test in criterion 3).
5. **`--live` runs live mode.**
   - `--live` opens the saved devices (`--input`/`--output` override them for the run), runs the same microphone and speaker checks as `--passthrough`, starts live mode and runs until `q`, `Esc` or `Ctrl+C`. Every way of stopping closes the audio, finishes a recording and prints a Swedish summary: dropouts, recording overruns if recording, and the latency parts.
   - The one-run options `--style`, `--key`, `--mode`, `--sound`, `--features`, `--pattern`, `--bpm`, `--time-signature` and `--record` work with `--live` as with `--accompany`/`--passthrough`; they are never saved. Combining `--live` with another mode, or giving an option that does not belong to it, gives the existing Swedish errors and exit code 1.
   - While running, one Swedish status line shows the note, the chord, the key, the latency and the dropout count, refreshed about twice a second from the snapshot (no Spectre live display; the dashboard is E6-3).
   - Without an interactive terminal, `--live` gives a Swedish error and exit code 1, like `--passthrough`.
   - Live mode is one entry point (settings in, exit code out) that E6-2's menu can call; the start-up steps (checks, opening the devices, recording) are shared with `--passthrough` rather than copied.
   - The README lists `--live` and its options, and `docs/manual-testing.md` E6 step 2 and 3 say how to run them with `--live` until the menu exists.

## Tasks
- [x] Split `Accompanist` into an input side (analysis, features, harmony) and an output side (pattern, synthesizer), keeping `--accompany` and its tests unchanged in behaviour.
- [x] A lock-free single-writer hand-over of the latest accompaniment result from the input to the output side, latching strikes until played.
- [x] A pull model for the output: the output callback asks a renderer for its buffer (PortAudio and in-memory outputs), beside the existing write model used by `--passthrough`.
- [x] Read the reported input and output latencies from the opened PortAudio streams.
- [x] The chain-delay measurement on a generated tone.
- [x] The live snapshot and its lock-free hand-over.
- [x] Recording from two threads (input ring and output ring) into the same stereo file.
- [x] `--live` with its one-run options, status line and summary; shared start-up with `--passthrough`.
- [x] Tests: equivalence with `--accompany`, strike latching, allocation, snapshot consistency, chain delay, CLI options and errors.
- [x] README and `docs/manual-testing.md`.
- [ ] Mark E6-1 done in `Constitution/Requirements-and-goal.md`'s E6 list only after the owner's manual checks — leave it unchecked here.

## Decisions
- **`--live` before the menu** (owner's choice): live mode can be tried and measured now, with the same one-run options as `--accompany`; E6-2's menu calls the same entry point and E6-3 replaces the status line with the dashboard.
- **Split chain, no sample hand-over** (owner's choice): analysis, features and harmony on the input callback; pattern and synthesizer on the output callback; only the latest decision crosses over. E2-2 measured ~5 hand-over underruns and ~6 drift corrections a minute with the sample hand-over, each audible, against a 0-dropout target over an hour; with the split, input jitter and clock drift can only delay a decision by a buffer, never cut the sound. It also puts the rhythm grid on the output clock, as `docs/rhythm.md` says. This settles E2-2's deferred "no larger cushion yet" for live mode.
- **The harmony's own grid (Jazz) counts input samples** (default): Jazz times its chords on the analysis side, the patterns on the output side. Both start with live mode, so they differ by the start offset of the two streams plus drift (well under a buffer per minute), which is far below a beat.
- **Latency's "analysis" is the measured chain delay** (owner's choice): measured on a generated tone with the chosen settings when live mode starts, so it includes the pitch window, the note hold, the gate, the onset wait and the harmony as they are configured, plus one buffer for the hand-over. It is deterministic and needs no hardware, so it is tested.
- **"Heard" is when the chord's envelope starts** (owner's choice): the first output sample above −60 dBFS. With a 200 ms attack the Pad would otherwise miss the target by design; the attack is part of the sound.
- **Dropouts as in Measurement** (default): PortAudio's input overflows and output underflows. The sample hand-over's counters do not exist in live mode; recording overruns are counted and shown, but are not dropouts in the sound.
- **Snapshot published per input buffer** (default): the input side owns most of the state; the reader polls it a few times a second. A sequence counter (seqlock) on a preallocated struct, so neither side allocates or blocks.
- **Recording from two threads** (default, follows from the split): input and output each get a ring; the writer thread interleaves them. Both start on the first buffer of their stream.

- **Measured with a fixed key under `auto`** (implementation): with the key `auto`, no chord sounds until the estimate has `KeyFirstSeconds` (2 s) of notes, so the tone would never be heard within the measurement. That wait happens once at the start of a session and is not latency; the delay is therefore measured in C Ionian when the key is `auto`.
- **The Pad is heard a little later** (owner's choice while implementing): the spec first said the delay is the same for every sound, but at −60 dBFS the Pad's 200 ms attack is heard ~3 ms after Organ and Pluck (34.0 ms against 30.7–30.8 ms with the defaults). The owner kept −60 dBFS; the test asserts the Pad is at most `SoundPreset.MinEdgeMs` (5 ms) behind, and criterion 2 says so.
- **Callback times** (measured on the owner's Mac, Intel Core i5-8257U, Release, defaults, the singing recording, 24 000 buffers per pattern): the input side (analysis, features, harmony) **111 µs** on average, at most 315–373 µs; the output side (pattern, synthesizer) **1.4–2.7 µs** on average, at most 59–505 µs. A buffer lasts 1 333 µs, so both together stay well under it.
- **Smoke test on real devices** (implementation, 3 s, built-in microphone and speakers): input 44.5 ms + chain 34.0 ms + hand-over 1.3 ms + output 10.0 ms = **89.9 ms**, 0 dropouts. With the built-in microphone the target is missed, as expected; since the chain alone takes 34 ms, meeting 50 ms with headphones (~5 ms) needs a microphone that reports under ~10 ms.
- **Shared start-up** (implementation, E2-3's deferred finding): `Cli.AudioSession` does the microphone check, opening the devices, the speaker warning, the recording and the ordered shut-down for both `--passthrough` and `--live`; each brings an `IAudioActivity`. The one-run options are `Cli.SettingOverrides`, shared by `--accompany` and `--live`.
- **Structure** (implementation): `Live.LiveChain` is the split chain (`IAudioInputHandler` and the new `IAudioRenderer`), `Live.LiveMode` runs it on opened devices and gives `Live.LiveSnapshot`s, `Audio.SnapshotSlot<T>` is the seqlock used for both decisions and state. `Accompanist.Play` is now `Decide` (input side) plus `Perform` (output side). `IAudioOutput.Start(IAudioRenderer?)` is the pull model, and `HandOverBuffer` is the renderer when none is given, so the output's callback has one path; `IAudioDevices.Latency` reads the reported latencies through `Pa_GetStreamInfo`, as the E2-1 spike did.
- **Status line in place** (implementation): it rewrites one terminal line with `\r` and an erase-to-end code through Spectre's `WriteAnsi`, since Spectre would wrap or escape the control characters in plain text.
- **Clean code:** the chain's delay is measured before the devices and the recording open; the key on the status line uses the Swedish `HarmonyNames.Key`; the tone's frequency comes from `Pitch.Frequency`; `LevelFeatures.NamesOn` replaces three copies of the same filter; `AccompanyOptions` holds a `SettingOverrides` instead of repeating its fields; the recorder owes dropped silence through one counter and interleaves in pieces of 8 192 frames instead of a buffer the size of both rings. Kept as they are: the snapshot's features and pattern (criterion 4 asks for them, E6-3 shows them); reading the latencies through `Pa_GetStreamInfo` (the figures E2-1 measured and the README reports; the callback's time info would give a different, per-buffer measure); measuring in C Ionian under `auto` rather than warming the chain up past the key estimate (that would take several seconds of analysis, far over the 100 ms budget).

- **Code review fixes:** the output's recording starts with the first output buffer after the input's first, so the channels start together although the output stream opens first; dropped recording buffers become silence written by the audio side in their place (not where the writer happens to be); the writer drops the lead of a side on a faster clock once it has stayed more than a buffer ahead for 0.5 s, so an hour's recording does not drift apart; recording overruns count a pair dropped together once (`--passthrough --record` keeps its old count); the status line is cut to the terminal's width so it can always be rewritten in place; the latency read tolerates a binding without the private stream field (it reports 0 instead of failing the stream); the chain's delay is measured after the microphone check and gives up after 150 ms of tone, within the 100 ms budget also when no chord comes; `AudioStreams` holds the start and stop order shared by `AudioPipeline` and live mode; `HandOverBuffer` implements `IAudioRenderer.Render` explicitly, so `Read` stays its one public read; the latency target is printed with its decimals.

- **Verification figures** (implementation): `ChainDelay.MeasureMs` takes 22.5 ms in Release with the defaults and 37.3 ms when no chord comes (gate threshold −6 dBFS). Its timing test runs in a collection of its own, since live mode measures before its audio starts, with nothing else competing for the CPU; under the parallel test run it measured the other classes' load (up to 209 ms). The recording equivalence tests use 3 s of the singing recording, so the suite's own timing tests are not starved.

- **PR review fixes:** `NoFeatures` and `UnknownFeatureMessage` moved from `AccompanyCommand` to `SettingOverrides`, which both commands share; a help test for `--live`; the summaries of `HarmonySetup` and `AccompanyCommand` no longer speak of live mode as still to come.

- **Superseded by E6-2:** `LiveCommand.Play` returns a `LiveExit` (exit code, interrupted by Ctrl+C); q and Esc let the chord ring out before the audio stops; `--live` also records when `RecordOn` is on; `AudioSession` handles Ctrl+C once a recording is open (`specs/E6-2-menu.md`).
- **Superseded by E6-3:** the status line (criterion 5 and the "Status line in place" decision) is replaced by the dashboard, redrawn in place every 200 ms; `LiveSnapshot` carries the run's settings and the recording's status instead of the feature names and the pattern; activities start from an `AudioStart` with the speaker warning (`specs/E6-3-live-dashboard.md`).

## Open Questions
