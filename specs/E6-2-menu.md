# E6-2 · Menu

- **Issue:** [#20](https://github.com/bjornpetersson1/solid-backup/issues/20)
- **Labels:** `E6 Live mode`
- **Phase:** 6. Live mode
- **Depends on:** E1-3 (#3), E1-4 (#4), E2-3 (#7), E6-1 (#19)
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 960 tests passed); hearing every choice live and a real Ctrl+C in the menu and in live mode are manual checks for the owner

## Summary
The start menu: the app started without arguments shows the current choices and lets the owner change devices, key, harmony style, sound, rhythm (pattern, tempo, time signature), level features and recording, each saved at once as the next start's defaults, and start live mode (E6-1) from it. `Esc` or `q` fades the sound out and returns to the menu; `Ctrl+C` quits cleanly.

## Goals
- Every live choice can be made in the menu, without editing files or giving flags.
- The choices are remembered between starts.
- One way into live mode, shared with `--live`.

## Non-goals
- Changing settings during live mode.
- The dashboard (E6-3); until it exists, live mode started from the menu shows E6-1's status line.
- The technical settings in `appsettings.json` (pitch range, buffer size, sample rate, gate threshold and hysteresis, onset and harmony timing, dynamics and character levels, latency target, recording folder); they stay in the file.
- A measurement command in the menu.
- Removing or changing the existing flags; `--live` and its one-run options stay.

## Acceptance Criteria
1. **The menu shows the choices and starts live mode.**
   - The app started without arguments in an interactive terminal shows a Swedish heading, a table of the current choices (input, output, key, style, sound, rhythm, level features, recording) and a selection with `Starta live`, one item per choice, and `Avsluta`. `Starta live` is preselected.
   - If the output is the built-in speakers, the menu shows the existing speaker warning (E2-3) above the selection.
   - `Starta live` runs E6-1's live-mode entry point with the current settings (the same microphone and speaker checks as `--live`). When live mode ends with `Esc` or `q`, the menu is shown again with the same choices; a live-mode error (e.g. a device that cannot be opened) is shown in Swedish and the menu is shown again.
   - Without an interactive terminal the existing Swedish error and exit code 1 stay.
2. **Every choice in the menu changes the live behaviour.**
   - **Input and output:** a list of the devices for that direction from PortAudio, marked with the system default, plus `Systemets standard` (saved as empty). A saved device that is no longer there is shown as missing in the table, and starting gives E2-3's Swedish "no such device" error (listing the devices that exist) and returns to the menu, where it can be changed.
   - **Key:** `auto` or one of the twelve tonics; after a tonic, the mode (`Ionian` … `Locrian`, the names `--mode` accepts). The mode is not asked for `auto`.
   - **Style:** Church, Campfire, Drone, Pop, Modal, Blues, Jazz, Free.
   - **Sound:** Pad, Organ, Pluck.
   - **Rhythm:** the pattern (Held, Arpeggio, Strum), then the tempo (40–240 BPM, re-asked on a value out of range, in Swedish) and the time signature (4/4, 3/4).
   - **Level features:** a multi-select of gate, dynamics, chord character and onsets, with the ones that are on preselected; none is allowed.
   - **Recording:** on or off.
   - Each choice maps to the `AppSettings` field that E6-1's live mode already reads. A test drives the menu with a test console, changes every choice, starts live mode with a fake entry point, and asserts the settings it received have every changed value; together with E6-1's tests that each of these settings changes the chain, this proves the criterion. Hearing every choice live is a manual check for the owner (`docs/manual-testing.md` E6 step 1).
3. **The choices are remembered.**
   - Each choice is saved to the user settings file (E1-3's `SettingsStore.Save`, only what differs from the defaults) as soon as it is made, before returning to the menu.
   - A test changes choices, builds a new menu on the same settings files, and sees the changed choices in its table and as the preselected options.
   - A choice that cannot be saved (e.g. an unwritable file) shows a Swedish warning and still applies for this session.
   - One-run flags (`--live --style …`) are never saved; the menu does not read them.
4. **Leaving live mode.**
   - `Esc` or `q` in live mode releases the chord, lets it fade out over the sound's release (at most 1 s), then stops the audio, finishes a recording and returns to the menu (from `--live` it exits instead, as in E6-1).
   - `Ctrl+C` in live mode or in the menu quits the app with exit code 0 after closing the audio and finishing a recording; it never leaves a half-written WAV header or a stack trace. A test of the live entry point with a cancellation shows the recording finished and the devices disposed.
   - `Avsluta` quits with exit code 0.

## Tasks
- [x] Menu screen: choices table, speaker warning, selection.
- [x] One prompt per choice, mapped to `AppSettings`, saved at once.
- [x] Device listing for the menu without opening streams (E2-3's deferred split of choosing from opening).
- [x] Start live mode from the menu and come back after `Esc`/`q`; `Ctrl+C` quits cleanly.
- [x] Fade-out on `Esc`/`q` before stopping.
- [x] Recording on/off as a saved setting (`RecordOn`, default `false`) read by live mode; `--record` still turns it on for one run.
- [x] Tests with Spectre's test console; README and `docs/manual-testing.md`.

## Decisions
- **Main menu with a settings table** (owner's choice): the owner changes only what they want and starts with one key press, rather than a wizard through every choice on each start.
- **Saved at every change** (owner's choice): nothing is lost if the owner quits without playing.
- **Style, tempo and time signature are in the menu** (owner's choice): the requirements say style and tempo are chosen in the menu; they already change live behaviour.
- **Builds on E6-1:** the menu calls the same live-mode entry point as `--live` (checks, devices, recording, status line); nothing is duplicated.
- **Recording is a saved choice** (default): the issue lists it among the remembered choices; `RecordOn` in `appsettings.json` defaults to off.
- **Fade-out on Esc/q only** (default): the requirements' flow says `Esc`/`q` fades the sound out; `Ctrl+C` stops at once but still closes everything cleanly.
- **Missing saved device** (default): shown as missing in the table and refused at start (E2-3's rule), rather than silently replaced by the default.

- **How live mode tells q/Esc from Ctrl+C** (implementation): `StopKeys.WaitForStop` now returns a `StopReason` (finished, stop key, interrupted). Live mode lets the chord ring out only on a stop key (`LiveChain.FadeOut`, waited for by `LiveMode.FadeOut` up to `LiveMode.MaxFadeOut`, 1 s); Ctrl+C stops at once. The fade applies to `--live` too, which then exits.
- **Ctrl+C from the menu** (implementation): the menu listens for Ctrl+C only while its own prompts run; there nothing is open and every choice is already saved, so it shows the cursor again and ends the process with exit code 0 (Spectre's prompts block in a key read that a cancellation cannot reach without making the whole command line asynchronous; a manual check). While live mode runs, live mode handles Ctrl+C itself: it closes the audio and the recording and `LiveCommand.Play` returns a `LiveExit` with `Interrupted`, on which the menu returns 0 (tested).
- **Device listing without streams** (implementation, E2-3's deferred split): the menu lists devices through `IAudioDeviceCatalog` (`PortAudioSession.Open`), which opens no streams, each time it is shown, so a device plugged in meanwhile appears; the speaker warning uses the chosen output's name from that list.
- **Display names** (implementation): styles and modes use `HarmonyNames` (Swedish), the level features are shown as grind, ansättningar, ackordkaraktär and dynamik; values are saved by their setting names. A prompt starts on the current value (`DefaultValue`), so Enter keeps it.
- **`RecordOn`** (implementation): a new setting (default `false`) in `AppSettings`, `appsettings.json` and the README. Live mode records when it is on, from the menu and from `--live`; `--record` turns recording on for one `--live` run.

- **Clean code:** the Swedish feature names are a `DisplayName` in the `LevelFeatures` registry; the menu's display texts are `Cli.SettingTexts`, for the dashboard (E6-3) too; the device list and the speaker check are read when the menu starts, before a device choice and after live mode, not on every redraw; the fade-out polls every 10 ms instead of spinning; the test catalog is shared (`FakeCatalog`); `AppCommand` builds one preflight and one settings store.
- **Timing tests run alone** (finding while verifying): with the menu's and live mode's tests added, E3-2's `AnalysingTheCorpus_IsFasterThanRealTime` and E6-1's chain-delay budget failed now and then because other test classes ran on the CPU at the same time. Both are in one `Timing` collection that xUnit runs without parallelism; their targets are unchanged.
- **The `--help`/no-arguments test** chooses Avsluta: with `Starta live` preselected, Enter would start live mode on the real devices.

- **Code review fixes:** once a recording is open, `AudioSession` handles Ctrl+C itself: before the audio runs (the open can block on the permission dialog) it throws the file away and lets the process end, and from then on it asks the activity to stop, so the fade, the stop and the file's header are never cut off; Ctrl+C during the fade ends it at once. The menu keeps live mode's exit code after Ctrl+C (1 when the recording could not be saved). The speaker check is redone whenever the device list is read, so a default output that became the speakers during live mode is warned about. Saved device names are matched as `DeviceSelector` does (case and Unicode form), so the saved device's row is preselected. The fade's limit uses a `Stopwatch`. The level features are switched straight from the chosen entries. The no-arguments test gets its menu from the test (fake devices, a temporary settings file) instead of PortAudio and the owner's settings.
- **The whole `CorpusMeasurementTests` class stays in the `Timing` collection** (code review finding declined): its measurement is computed once (`Lazy`) and shared by every test in the class, so whichever test runs first times the analysis. Moving only the real-time test would let an accuracy test compute it under the parallel load again.

## Open Questions
