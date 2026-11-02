# E7-3 · Input monitoring

- **Issue:** [#72](https://github.com/bjornpetersson1/solid-backup/issues/72)
- **Labels:** `E7 Playtest`
- **Phase:** 7. Playtest
- **Depends on:** none open (builds on E2-2 (#6), E2-3 (#7), E5-3 (#16), E6-1 (#19), E6-2 (#20), E6-3 (#21))
- **Status:** Approved

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
- **Delay.** The input crosses from the input's thread to the output's through a lock-free buffer allocated up front. The chain adds at most two buffers (`2 × BufferSize` samples) of delay on top of the devices' own latency. If the input runs ahead (clock drift), the oldest samples are skipped so the delay never grows; if it runs behind, silence is played for the missing samples. Nothing in either callback allocates, locks or does I/O.
- **Built-in speakers.** If the output is the built-in speakers (E2-3's route check), live mode does not monitor, and says so in Swedish when it starts and on the dashboard; the saved choice is kept for the next start with headphones. The menu shows the same note next to the speaker warning.
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
- [ ] `MonitorOn` and `MonitorLevelPercent` in `AppSettings`, `appsettings.json`, validation and the README.
- [ ] A lock-free, preallocated input-to-output buffer in the live chain, with the drift rules, and the mix with the soft limit on the output side.
- [ ] Turn monitoring off with the built-in speakers, with the Swedish note in live mode, the dashboard and the menu.
- [ ] Menu item, settings table row, dashboard text.
- [ ] The recording's output channel stays chords only.
- [ ] `docs/manual-testing.md`.
- [ ] Tests: mix, delay, level, no clipping, off unchanged, speakers, recording, menu, dashboard, validation, allocation.

## Decisions
- **Level as 0–100 %, default 70 %** (owner's choice): simple in the menu; a linear gain on the input.
- **Off with the built-in speakers, with a Swedish note** (owner's choice): a microphone into the speakers would howl; the saved choice is not changed.
- **Recording keeps chords only on the output channel** (owner's choice): the input is already on the left channel, and the right stays clean for analysis.
- **`MonitorOn` default off** (owner's choice).
- **At most two buffers of added delay** (default): sharpens "as little delay as the pipeline allows" into a checkable bound; the input and output streams are separate (E6-1), so one buffer of hand-over is the floor and one more absorbs jitter.
- **Raw input, not gated** (default): monitoring is for hearing yourself; the gate is about the chords.
- **Soft limit on the sum** (default): the synthesizer already bends towards full scale instead of clipping (E5-1); the same curve keeps the mix within full scale.
- **Live mode only** (default): the issue speaks of hearing yourself while playing.

## Open Questions
- None.
