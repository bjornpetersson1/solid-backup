# E8-1 · Crackle in input monitoring

- **Issue:** [#81](https://github.com/bjornpetersson1/solid-backup/issues/81)
- **Labels:** `E8 Playtest 2`
- **Phase:** 8. Playtest 2
- **Depends on:** none open (builds on E7-3 (#72))
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 1147 tests passed); hearing no crackle live with one and with two devices, and `avbrott 0` after a few minutes, are manual checks for the owner

## Summary
With monitoring on, the microphone crackles in the headphones. The cause is in the hand-over between the input's and the output's callbacks (`Live.InputMonitor`). This issue gives the hand-over a small jitter buffer and smooth drift correction, so the monitored signal stays continuous. It also adds an interruption counter to the dashboard, so the fix can be checked live.

## Cause (as found while writing the spec)
Input and output are two separate PortAudio streams with their own callbacks (also when it is the same device). At 64 samples and 48 kHz, `InputMonitor` lets at most one buffer (≈ 1.3 ms) wait beyond the one being played. It then handles timing in two ways:
- **Too little waiting:** whenever the output's callback comes before the input's has delivered, the rest of the output buffer is filled with hard silence.
- **Too much waiting:** whenever more than one buffer waits, the oldest samples are skipped without a crossfade.

Callbacks seldom alternate perfectly. They arrive in bursts, and with two devices the clocks also drift. So these hard edges happen often, and each one is a click: together they are the crackle. Confirming this with a test is the first task.

## Goals
- A monitored signal without hard edges: no silence cut into the signal and no uncrossfaded jumps, whether input and output share a device or not.
- An extra monitor delay no larger than about 5 ms.
- A dashboard count of the monitor's interruptions.

## Non-goals
- Echo cancellation, effects on the monitored signal, changing the level rules or the speaker safety (E7-3 stands).
- Crackle in the recorded synth (E8-2) or anywhere else than the monitored input.
- Adaptive resampling (see Decisions).
- One duplex stream for input and output.
- Changing the chords' latency or the latency target.

## Acceptance Criteria
1. **The cause is identified and described in the spec/PR.**
   - A test drives the E7-3 monitor with generated input and output callbacks in a realistic irregular order: bursts of up to 3 input buffers (the output's callbacks steady), and the input clock 0.1 % faster, then slower, than the output. It shows the hard edges in the current code, and it fails before the fix. The PR names the cause.
2. **A sine through monitoring gives a continuous output: no discontinuities, no dropped or repeated buffers left uncrossfaded, no clipping at the default level.**
   - The same irregular orders as in 1, with a 440 Hz sine at −6 dBFS and the chords silent. Across 10 s of output (after the start), the largest sample-to-sample step is at most 1.5 × the sine's own largest step.
   - With callback bursts within the jitter buffer (up to 3 input buffers) and no drift, there are 0 gaps, and at most one correction, within the first second, while the monitor settles; none after.
   - With ±0.1 % drift, the monitor delay stays within its bounds (criterion 3) through crossfaded corrections, and every correction meets the step bound above.
   - The input stops (no more input buffers): the monitored signal fades to silence within the step bound instead of being cut off, and it fades in again when input returns. Each such stop counts as one gap.
   - At the default level, a full-scale input mixed with silent chords stays within full scale (E7-3's soft limit, unchanged).
3. **The monitor delay is at most max(5 ms, 3 × `BufferSize`) on top of the devices' own latency.**
   - It is measured in the chain test (E7-3's orders) and in the monitor test (a ramp input, so each output sample tells which input sample it plays), from input sample to output sample, for both start orders (input first, output first) and through the drift cases in 2, outside the crossfades (where two input positions are mixed).
   - E7-3's "at most two buffers" is replaced by this bound.
4. **The dashboard shows the monitor's interruptions.**
   - The monitoring row reads e.g. `på 80 % · avbrott 0` (Swedish).
   - The count is gaps (input ran dry) plus overflows (the ring was full and input was overwritten). Crossfaded drift corrections are not counted.
   - It is read through the existing lock-free snapshot.
   - Dashboard test.
5. **Nothing else changes.**
   - 0 bytes are allocated per callback with monitoring on, for both callbacks (the live chain's allocation test).
   - With monitoring off, the output is identical sample for sample to before this change.
   - The recording still holds only the chords on its output channel.
   - E7-3's level, fade-out on stop and speaker rules still pass.
   - `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` passes.
6. **Manual check (owner):** with headphones and monitoring on, your voice is heard without crackle, both with the built-in microphone and headphones and with one device for both. The dashboard shows `avbrott 0` after a few minutes of play.

## Tasks
- [x] A test with irregular callback orders (bursts, ±0.1 % drift) and a step measure that shows the crackle in the current `InputMonitor`.
- [x] A jitter buffer in `InputMonitor` with a target fill. It starts (and restarts after a gap) only once the target fill is reached, with a fade-in.
- [x] Drift correction: when the fill leaves its window, drop or repeat a short stretch with a crossfade.
- [x] Running dry: fade out over the samples still waiting instead of cutting in silence, and count a gap.
- [x] A gap and overflow counter, published to the dashboard snapshot. The Swedish text goes on the monitoring row.
- [x] Update the E7-3 tests and spec text that assert "at most two buffers".
- [x] README: the monitoring paragraph mentions the interruption count.

## Decisions
- **Both setups** (owner: "both / don't know"): the fix must hold for one device and for two (two clocks), so the tests cover bursts and drift both ways.
- **Delay budget about 5 ms** (owner, recommended): the extra monitor delay is at most max(5 ms, 3 × `BufferSize`). At 64 samples and 48 kHz that is 240 samples, enough for input bursts of up to 3 buffers. The `3 × BufferSize` floor keeps larger buffer sizes working. The bound is derived from the sample rate and buffer size, not a new setting.
- **Crossfaded drop or repeat** (owner, recommended) rather than adaptive resampling: simple, deterministic, no allocation, easy to test. On a voice, a short crossfade a few times a minute (0.1 % drift is about one buffer a second) is not heard as a click. The crossfade length is an implementation choice within the step bound.
- **Interruption counter on the dashboard** (owner, recommended): `avbrott N` on the monitoring row makes the manual check measurable. Crossfaded corrections are smooth by design and are not counted as interruptions.
- **The step bound** (default): 1.5 × the sine's own largest step is loose enough for a crossfade between two parts of the same sine and tight enough to catch a cut to silence or an uncrossfaded jump at −6 dBFS.
- **Bursts of up to 3 input buffers, not 4** (owner, while implementing): when the output runs four buffers before the input delivers, the delay is at least 4 × 64 = 256 samples, beyond the 5 ms bound. The owner kept the 5 ms bound, and the tests use bursts of up to 3 input buffers (about 4 ms of callback jitter at 64 samples and 48 kHz). The output's callbacks come steadily in the tests, as the hardware clock drives them; with both sides bursting, the inherent delay alone is about 3.5 buffers.
- **One settling correction at the start** (owner, while implementing): the monitor cannot know the burst pattern in advance, so within its first window (0.25 s) it corrects the fill once to fit what it has seen. Without drift there are no corrections after that. The test allows at most one, within the first second.
- **How the monitor works** (implementation): `Live.InputMonitor` waits for a buffer and a margin (0.46 ms) of input, then plays from the oldest waiting sample, at most max(5 ms, 3 × `BufferSize`) − 0.75 ms behind, with a 2 ms fade-in. Every 0.25 s it looks at the least input that waited before a read. Outside the band of 0.29–0.75 ms beyond a buffer, it drops or repeats the difference to the 0.46 ms target with a 2 ms crossfade.
- **Shared with E8-2** (clean code): the ring with its crossfaded drop and repeat is `Audio.SplicingRing`, the trough over a window is `Audio.FillWindow`, and the crossfade (also the fades from and to silence) is `Audio.Crossfade`, so the recorder can use the same pieces instead of a copy. `InputMonitor` keeps only the level, the mix, its states (waiting, playing, fading out) and the counters. Durations use `Analysis.Duration.Samples`. `HandOverBuffer`'s own drift window (E2-1) is left as it is: it is outside this issue.
- **Repeats and fade-outs play what was played, backwards** (implementation): a repeat crossfades out of the last 2 ms played, read backwards, so it needs no input that has not arrived. When a read would starve, the monitor repeats a little (a correction, not a gap) instead of stopping. Only when nothing at all has arrived since that repeat has the input run dry: then it fades out the same way and counts a gap.
- **Interruptions** (implementation): `InputMonitor.Interruptions` (gaps + overflows) goes through `LiveChain.MonitorInterruptions` into `LiveSnapshot.MonitorInterruptions`, null while not monitoring. The dashboard row reads `på, 70 % · avbrott 0`, keeping the menu's text for the setting (`LiveDashboard.Monitor`).
- **The step test proves the cause** (implementation): `MonitorContinuityTests` was run against the E7-3 monitor first. Every bursty and drifting schedule failed with a step of 0.96, a cut from the sine to silence and back, against a bound of 0.043.
- **Code review fixes:**
  - An overflow is counted once per stall, not once per input buffer written while the ring is full.
  - The output restarts only once input has really been overwritten (more waiting than the ring holds), so every restart is a counted overflow.
  - A restart while playing fades out what was playing before fading in the latest input, instead of jumping to a fade-in from silence (`AStalledOutput_FadesOutAndInAgain_CountingOneOverflowPerStall`, which fails on the code before the fix with a step of 0.50).
  - `SplicingRing.Publish` hands the read position over through its own field, once per buffer.
  - Once monitoring has stopped, the input's callback no longer writes into the monitor.
  - The chain test checks the mix from the end of the fade-in, in two parts (before and after the settling correction), each with its own delay within the bound. It no longer skips the first 0.3 s.
- **Declined code review finding:** "the approved criteria were loosened" (bursts of 3 instead of 4, one settling correction). Both changes were asked of and approved by the owner during implementation (recorded above), not made to get to green.
- **Departs from E7-3:** E7-3's delay bound of two buffers and its "silence for the missing samples" and "skip the oldest samples" rules are replaced by the rules above.

## Open Questions
- None.
