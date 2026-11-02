# E6-2 · Menu

- **Issue:** [#20](https://github.com/bjornpetersson1/solid-backup/issues/20)
- **Labels:** `E6 Live mode`
- **Phase:** 6. Live mode
- **Depends on:** E1-3 (#3), E1-4 (#4), E2-3 (#7), E6-1 (#19)
- **Status:** Approved

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
- [ ] Menu screen: choices table, speaker warning, selection.
- [ ] One prompt per choice, mapped to `AppSettings`, saved at once.
- [ ] Device listing for the menu without opening streams (E2-3's deferred split of choosing from opening).
- [ ] Start live mode from the menu and come back after `Esc`/`q`; `Ctrl+C` quits cleanly.
- [ ] Fade-out on `Esc`/`q` before stopping.
- [ ] Recording on/off as a saved setting (`RecordOn`, default `false`) read by live mode; `--record` still turns it on for one run.
- [ ] Tests with Spectre's test console; README and `docs/manual-testing.md`.

## Decisions
- **Main menu with a settings table** (owner's choice): the owner changes only what they want and starts with one key press, rather than a wizard through every choice on each start.
- **Saved at every change** (owner's choice): nothing is lost if the owner quits without playing.
- **Style, tempo and time signature are in the menu** (owner's choice): the requirements say style and tempo are chosen in the menu; they already change live behaviour.
- **Builds on E6-1:** the menu calls the same live-mode entry point as `--live` (checks, devices, recording, status line); nothing is duplicated.
- **Recording is a saved choice** (default): the issue lists it among the remembered choices; `RecordOn` in `appsettings.json` defaults to off.
- **Fade-out on Esc/q only** (default): the requirements' flow says `Esc`/`q` fades the sound out; `Ctrl+C` stops at once but still closes everything cleanly.
- **Missing saved device** (default): shown as missing in the table and refused at start (E2-3's rule), rather than silently replaced by the default.

## Open Questions
