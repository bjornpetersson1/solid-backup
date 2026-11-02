# E6-1 · Live pipeline

- **Issue:** [#19](https://github.com/bjornpetersson1/solid-backup/issues/19)
- **Labels:** `E6 Live mode`
- **Phase:** 6. Live mode
- **Depends on:** E5-2 (#15), E5-3 (#16), E5-5 (#18), E4-3 (#13)
- **Status:** Approved

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
   - Tests: the chain delay is deterministic for given settings, is the same for Pad, Organ and Pluck (their envelopes all start at once), and with the defaults lies between half the pitch window and 50 ms.
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
- [ ] Split `Accompanist` into an input side (analysis, features, harmony) and an output side (pattern, synthesizer), keeping `--accompany` and its tests unchanged in behaviour.
- [ ] A lock-free single-writer hand-over of the latest accompaniment result from the input to the output side, latching strikes until played.
- [ ] A pull model for the output: the output callback asks a renderer for its buffer (PortAudio and in-memory outputs), beside the existing write model used by `--passthrough`.
- [ ] Read the reported input and output latencies from the opened PortAudio streams.
- [ ] The chain-delay measurement on a generated tone.
- [ ] The live snapshot and its lock-free hand-over.
- [ ] Recording from two threads (input ring and output ring) into the same stereo file.
- [ ] `--live` with its one-run options, status line and summary; shared start-up with `--passthrough`.
- [ ] Tests: equivalence with `--accompany`, strike latching, allocation, snapshot consistency, chain delay, CLI options and errors.
- [ ] README and `docs/manual-testing.md`.
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

## Open Questions
