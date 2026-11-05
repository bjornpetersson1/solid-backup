# E8-1 · Crackle in input monitoring

- **Issue:** [#81](https://github.com/bjornpetersson1/solid-backup/issues/81)
- **Labels:** `E8 Playtest 2`
- **Phase:** 8. Playtest 2
- **Depends on:** none open (builds on E7-3 (#72))
- **Status:** Approved

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
   - A test drives the E7-3 monitor with generated input and output callbacks in a realistic irregular order: bursts of up to 4 input buffers, and the input clock 0.1 % faster, then slower, than the output. It shows the hard edges in the current code, and it fails before the fix. The PR names the cause.
2. **A sine through monitoring gives a continuous output: no discontinuities, no dropped or repeated buffers left uncrossfaded, no clipping at the default level.**
   - The same irregular orders as in 1, with a 440 Hz sine at −6 dBFS and the chords silent. Across 10 s of output (after the start), the largest sample-to-sample step is at most 1.5 × the sine's own largest step.
   - With callback bursts within the jitter buffer (up to 4 buffers) and no drift, there are 0 gaps and 0 drift corrections.
   - With ±0.1 % drift, the monitor delay stays within its bounds (criterion 3) through crossfaded corrections, and every correction meets the step bound above.
   - The input stops (no more input buffers): the monitored signal fades to silence within the step bound instead of being cut off, and it fades in again when input returns. Each such stop counts as one gap.
   - At the default level, a full-scale input mixed with silent chords stays within full scale (E7-3's soft limit, unchanged).
3. **The monitor delay is at most max(5 ms, 3 × `BufferSize`) on top of the devices' own latency.**
   - It is measured in the chain test, from input sample to output sample, for both start orders (input first, output first) and through the drift cases in 2.
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
- [ ] A test with irregular callback orders (bursts, ±0.1 % drift) and a step measure that shows the crackle in the current `InputMonitor`.
- [ ] A jitter buffer in `InputMonitor` with a target fill. It starts (and restarts after a gap) only once the target fill is reached, with a fade-in.
- [ ] Drift correction: when the fill leaves its window, drop or repeat a short stretch with a crossfade.
- [ ] Running dry: fade out over the samples still waiting instead of cutting in silence, and count a gap.
- [ ] A gap and overflow counter, published to the dashboard snapshot. The Swedish text goes on the monitoring row.
- [ ] Update the E7-3 tests and spec text that assert "at most two buffers".
- [ ] README: the monitoring paragraph mentions the interruption count.

## Decisions
- **Both setups** (owner: "both / don't know"): the fix must hold for one device and for two (two clocks), so the tests cover bursts and drift both ways.
- **Delay budget about 5 ms** (owner, recommended): the extra monitor delay is at most max(5 ms, 3 × `BufferSize`). At 64 samples and 48 kHz that is 240 samples, enough for bursts of up to 4 buffers. The `3 × BufferSize` floor keeps larger buffer sizes working. The bound is derived from the sample rate and buffer size, not a new setting.
- **Crossfaded drop or repeat** (owner, recommended) rather than adaptive resampling: simple, deterministic, no allocation, easy to test. On a voice, a short crossfade a few times a minute (0.1 % drift is about one buffer a second) is not heard as a click. The crossfade length is an implementation choice within the step bound.
- **Interruption counter on the dashboard** (owner, recommended): `avbrott N` on the monitoring row makes the manual check measurable. Crossfaded corrections are smooth by design and are not counted as interruptions.
- **The step bound** (default): 1.5 × the sine's own largest step is loose enough for a crossfade between two parts of the same sine and tight enough to catch a cut to silence or an uncrossfaded jump at −6 dBFS.
- **Departs from E7-3:** E7-3's delay bound of two buffers and its "silence for the missing samples" and "skip the oldest samples" rules are replaced by the rules above.

## Open Questions
- None.
