# E7-3 · Input monitoring

- **Issue:** [#72](https://github.com/bjornpetersson1/solid-backup/issues/72)
- **Labels:** `E7 Playtest`
- **Phase:** 7. Playtest
- **Depends on:** none open (builds on E2-2 (#6), E2-3 (#7), E5-3 (#16), E6-1 (#19), E6-2 (#20), E6-3 (#21))
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 1111 tests passed); hearing yourself without a disturbing delay and an hour without dropouts are manual checks for the owner

## Summary
Monitoring: in live mode, the microphone signal can be mixed into the headphones together with the chords, switched on or off, at a chosen level. The owner needs to hear themselves (E6 playtest).

## Goals
- Hear your own voice in the headphones with as little added delay as the live chain allows.
- On/off and a level, chosen in the menu, remembered, shown on the dashboard.
- Never a feedback loop through the built-in speakers.

## Non-goals
- Effects on the monitored signal (reverb, EQ, compression).
- Echo cancellation.
- Monitoring in the other commands (`passthrough`, `synth`, `harmonize`, measurement); only live mode.
- Changing the recording format: the recording keeps the raw input left and the chords right.
- Changing settings during live mode.

## The rules

| Setting | Values | Default | Menu (Swedish) |
|---|---|---|---|
| `MonitorOn` | `true`, `false` | `false` | Medhörning: På / Av |
| `MonitorLevelPercent` | whole number, 0–100 | 70 | Medhörningsnivå (%) |

- **The mix.** With monitoring on, each output sample is the chords plus the input × `MonitorLevelPercent` / 100 (100 % is the microphone signal unchanged, 0 % silent). The raw input is monitored: the gate and level features do not affect it.
- **No clipping.** The sum is kept within full scale (|sample| ≤ 1) with the synthesizer's soft limit, so a loud voice over a full chord bends smoothly instead of clipping; below the limit's knee the mix is unchanged.
- **Delay** (superseded by E8-1: a jitter buffer of at most max(5 ms, 3 × `BufferSize`) with crossfaded drift correction and fades instead of silence and skips; `specs/E8-1-crackle-in-input-monitoring.md`). The input crosses from the input's thread to the output's through a lock-free buffer allocated up front. The chain adds at most two buffers (`2 × BufferSize` samples) of delay on top of the devices' own latency. If the input runs ahead (clock drift), the oldest samples are skipped so the delay never grows; if it runs behind, silence is played for the missing samples. Nothing in either callback allocates, locks or does I/O.
- **Built-in speakers.** If the output is the built-in speakers (E2-3's route check), live mode does not monitor, and says so in Swedish when it starts and on the dashboard; the saved choice is kept for the next start with headphones. The menu shows the same note next to the speaker warning. (Tightened in the code review: an output whose route cannot be read counts as unsafe too, the route is read anew for each check, and live mode checks it again every 500 ms and stops the monitoring for the session when it turns unsafe.)
- **Recording.** The output channel of a recording (E5-3) holds only the chords, as before.
- **Leaving live mode.** The fade-out on `Esc`/`q` (E6-2) also stops the monitored input.

## Acceptance Criteria
1. **With monitoring on, the in-memory `IAudioOutput` receives the input mixed with the chords at the chosen level; with it off, only the chords.**
   - A live-chain test (generated tone through `IAudioInput`, in-memory `IAudioOutput`) runs the same input with monitoring off and on; the difference between the two outputs equals the input × level, shifted by the chain's monitor delay, to within 1e-6 where the sum is below the limit's knee. With monitoring off, the output is identical (sample for sample) to before this change.
   - The monitor delay in that test is at most `2 × BufferSize` samples, also with input and output buffers alternating unevenly (two inputs, then two outputs).
2. **The monitor level scales the input as set, and the mix does not clip beyond full scale.**
   - At 0 %, 50 % and 100 % the monitored part is 0, 0.5 and 1 × the input (test).
   - A full-scale input over a full chord at 100 % gives no output sample with |sample| > 1 (test).
3. **The setting is in the menu, remembered, and changes the live behaviour; 0 bytes allocated per callback still holds.**
   - The menu has a monitoring item (Swedish): on/off, then the level (0–100, re-asked in Swedish on a value out of range), starting on the current values; the settings table shows both. Each choice is saved at once (E6-2's rule). The menu test drives it and asserts the settings the live entry point receives; a new menu on the same files shows the saved values.
   - Live mode's chain reads `MonitorOn` and `MonitorLevelPercent` (covered by criterion 1's test through the live entry's builder).
   - The dashboard shows monitoring on with its level, off, or off because of the speakers, in Swedish (dashboard test).
   - With a fake output route that is the built-in speakers, live mode does not monitor and shows the Swedish note (test).
   - Invalid values give Swedish validation errors (tests for both settings).
   - The live chain's allocation test passes with monitoring on, for both callbacks.
4. **The recording holds only the chords on its output channel** (test with a recorder and monitoring on).
5. **Manual check (owner):** with headphones, your own voice is heard without a disturbing delay, and an hour of play gives no dropouts. Added to `docs/manual-testing.md`.

## Tasks
- [x] `MonitorOn` and `MonitorLevelPercent` in `AppSettings`, `appsettings.json`, validation and the README.
- [x] A lock-free, preallocated input-to-output buffer in the live chain, with the drift rules, and the mix with the soft limit on the output side.
- [x] Turn monitoring off with the built-in speakers, with the Swedish note in live mode, the dashboard and the menu.
- [x] Menu item, settings table row, dashboard text.
- [x] The recording's output channel stays chords only.
- [x] `docs/manual-testing.md`.
- [x] Tests: mix, delay, level, no clipping, off unchanged, speakers, recording, menu, dashboard, validation, allocation.

## Decisions
- **Level as 0–100 %, default 70 %** (owner's choice): simple in the menu; a linear gain on the input.
- **Off with the built-in speakers, with a Swedish note** (owner's choice): a microphone into the speakers would howl; the saved choice is not changed.
- **Recording keeps chords only on the output channel** (owner's choice): the input is already on the left channel, and the right stays clean for analysis.
- **`MonitorOn` default off** (owner's choice).
- **At most two buffers of added delay** (default): sharpens "as little delay as the pipeline allows" into a checkable bound; the input and output streams are separate (E6-1), so one buffer of hand-over is the floor and one more absorbs jitter.
- **Raw input, not gated** (default): monitoring is for hearing yourself; the gate is about the chords.
- **Soft limit on the sum** (default): the synthesizer already bends towards full scale instead of clipping (E5-1); the same curve keeps the mix within full scale.
- **Live mode only** (default): the issue speaks of hearing yourself while playing.

- **Where it lives** (implementation): `Live.InputMonitor` holds the ring (eight buffers, a power of two) with one counter per side, written with `Volatile`; `LiveChain` writes each input buffer into it after the analysis and the recording, and mixes it in on the output after the recording, so the recording keeps the chords only; the fade stops it. `LiveMode` builds it only with `MonitorOn` and not on the speakers (`InputMonitor.FromSettings`), and says whether it monitors (`LiveMode.Monitoring`). The soft limit is the synthesizer's `Synthesizer.Saturate`, now public.
- **The drift rules** (implementation): when the output reads a buffer and more than one further buffer is waiting, the oldest samples are skipped; if fewer samples than the buffer are waiting, the rest is silence; if the ring is full (the output stopped), new input is dropped. Measured in the tests, the monitor delay is 0 with the input first and at most two buffers with the output first (two outputs, then two inputs).
- **Texts** (implementation): the menu item Medhörning asks on/off, then the level (0–100, re-asked in Swedish); the table and the dashboard show "på, 70 %", "av", or on the dashboard "av (inbyggda högtalare)"; the note `SettingTexts.MonitorOffOnSpeakers` is shown under the menu's speaker warning and above the dashboard when live mode starts.
- **Speakers in tests** (implementation): the fake devices deliver silence, so the speaker rule is shown by `LiveMode.Monitoring` and the Swedish note in a `LiveCommand` test with the speaker route.

- **Clean code:** the rule "monitor with `MonitorOn`, not to the speakers" is `InputMonitor.Monitors`, used by the chain's builder, the texts and the menu, and live mode's note reads `LiveMode.Monitoring`; the menu's tempo and level prompts share `AskInRange`; `MonitorLevels` is in `Configuration`; past the waiting input the mix leaves the chords as they are; the tests share `LiveChainTests`' allocation harness and compute the chords-only reference once per order.
- **Declined clean-code findings:** `HandOverBuffer` is not reused, because its rules (whole-buffer drops, a buffer of silence on an underrun, drift dropped after a window) differ from the spec's sample-exact drift rules; the soft limit is not moved into one limiter on the final output, as that would change the synthesizer's own output path, outside this issue; "on the speakers" keeps coming from the speaker check's warning (`AudioStart.SpeakerWarning`), as before this change, rather than adding a route flag to `AudioStart`.

- **Code review fixes:**
  - **Fail closed** (safety): the speaker check was built for a warning and failed open; monitoring now needs a route known not to be the speakers (`AudioPreflight.IsSafeForMonitoring`), so an unreadable route or a device name CoreAudio does not know means no monitoring, with the same Swedish note.
  - **Fresh routes**: `CoreAudioRouteSource` read the routes once per instance, and the menu keeps one for the whole session, so a headphone pulled out between two live runs was not seen; the routes are now read on every call.
  - **During the session**: `MonitorGuard`, on the dashboard's thread, checks the output again every `LiveCommand.MonitorCheckInterval` (500 ms) while live mode monitors, and stops the monitoring for good when it is unsafe (`LiveMode.StopMonitoring`), before the microphone can howl in the speakers.
  - **One source of truth**: the dashboard shows what live mode does (`DashboardContext.Monitoring` from `LiveMode.Monitoring`), and the menu's row and note both follow the same safe-output check; the row says "av (osäker utgång)".
  - **Fade-out without a click**: on q/Esc the monitored input fades to silence over the next output buffer, then stops.
  - **A stalled output**: the input now overwrites the oldest samples when the ring is full, so after a stall the output resumes with the latest input, not with what was there before the stall.
  - **Level 0 %** (finding declined): `MonitorOn` with 0 % still builds the monitor; it is a valid choice the menu shows as "på, 0 %", and the per-sample cost is far below the callback's budget.

- **PR review fixes:** the README's live-mode and menu paragraphs list the E7 dashboard rows (Byten, Autospel, Medhörning) and menu items (Ackordbyten, Autospel, Medhörning) and the monitoring note; the Feedback row in `Constitution/Requirements-and-goal.md` says monitoring plays only to a safe output and stops if that changes.

## Open Questions
- None.
