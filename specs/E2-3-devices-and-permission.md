# E2-3 · Devices and permission

- **Issue:** [#7](https://github.com/bjornpetersson1/solid-backup/issues/7)
- **Labels:** `E2 Audio`
- **Phase:** 2. Audio
- **Depends on:** E2-2 (#6)
- **Status:** Approved

## Summary
List the audio devices, choose input and output by name through settings and flags, stop with Swedish instructions when the microphone permission is denied, and warn when the output goes to the built-in speakers. Until the menu arrives in E6-2, this is reachable through `--devices`, `--input`, `--output` and the `--passthrough` flag from E2-2.

## Goals
- A device catalogue and selection by name that the menu in E6-2 can reuse.
- Precise detection of a denied microphone permission, from macOS itself.
- Precise detection of the built-in speakers, including telling headphones in the built-in jack apart from the speakers.

## Non-goals
- The menu, or saving device choices from it (E6-2). The flags do not write the user settings file.
- The speaker warning on the dashboard (E6-3); this issue provides the detection and shows it on the command line.
- Asking macOS for permission programmatically; macOS's own dialog appears when the stream opens.
- Echo cancellation, or refusing to play through speakers (it is a warning, not a stop).
- Hot-plugging: devices are resolved when a run starts, not followed during it.

## Acceptance Criteria
1. **Devices can be listed.**
   - `dotnet run --project src/SolidBackup -- --devices` lists every input and output device by name in Swedish, with channel counts, marks the system defaults and the devices chosen in the settings, and marks an output that is the built-in speakers.
   - Listing goes through an interface with a PortAudio implementation, so the listing and marking logic is tested with a fake catalogue.
2. **Devices can be chosen by name.**
   - New settings `InputDevice` and `OutputDevice` in `appsettings.json`, default `""` (the system default), in `AppSettings`, validated (any string, never null) and listed in the README settings table.
   - `--input <name>` and `--output <name>` override the settings for that run, and `--passthrough` from E2-2 opens the resolved devices instead of always the defaults.
   - A name matches a device of the right direction exactly, ignoring case. A name that matches nothing gives a Swedish error that names the missing device and lists the available ones of that direction, no stream is opened, and the exit code is 1. Tests cover a match, a case-insensitive match, no match, and the empty default.
3. **A missing permission gives a Swedish message with instructions.**
   - Before an input stream is opened, the app reads the microphone authorisation status from macOS (AVFoundation, through the Objective-C runtime with P/Invoke, no new package).
   - Denied or restricted: a Swedish message says that the terminal app lacks microphone access and how to grant it (System Settings → Integritet och säkerhet → Mikrofon, turn on the terminal app, then restart the terminal), no stream is opened, and the exit code is 1.
   - Not yet asked: the app continues, and macOS shows its own dialog when the stream opens.
   - Granted, or the status cannot be read: the app continues without a message.
   - The decision from status to message/continue is tested with a fake permission source; the macOS call itself is a manual check.
4. **The built-in speakers as output give a warning.**
   - The app asks CoreAudio (P/Invoke, no new package) whether the chosen output device is built in and whether its current data source is the internal speakers, so headphones in the built-in jack give no warning.
   - When the output is the built-in speakers, `--passthrough` shows a Swedish warning before starting (headphones are assumed, there is no echo cancellation) and still runs; `--devices` marks that device.
   - The decision from device properties to warning is tested with a fake route source; the CoreAudio call itself is a manual check.
5. **Manual checks for the owner are listed.** The PR lists, unchecked: the permission message after turning off the terminal's microphone access; no message after turning it back on; the speaker warning with no headphones; no warning with headphones in the jack and with USB/Bluetooth headphones; `--input`/`--output` with a real device name.
6. **The canonical check passes**, on macOS and in CI on Ubuntu (tests never call AVFoundation, CoreAudio or PortAudio).

## Tasks
- [ ] Device catalogue interface and PortAudio implementation.
- [ ] Settings `InputDevice`/`OutputDevice`, validation, `appsettings.json`, README.
- [ ] Name resolution with the Swedish error.
- [ ] `--devices`, `--input`, `--output`; wire the resolved devices into `--passthrough`.
- [ ] Microphone permission source (AVFoundation via the Objective-C runtime) and the decision logic.
- [ ] Output route source (CoreAudio transport type and data source) and the warning logic.
- [ ] Tests with fakes for catalogue, permission and route.
- [ ] README: `--devices`, `--input`, `--output`, the permission message and the speaker warning.

## Decisions
- **Settings plus flags until the menu exists.** `InputDevice`/`OutputDevice` are what E6-2 will save; `--input`/`--output` are one-run overrides and are not saved. (Owner's choice.)
- **Exact, case-insensitive name match; an unknown name is an error listing what exists.** Silently falling back to the speakers would defeat the headphones assumption. (Owner's choice.)
- **Permission from macOS (AVFoundation authorisation status).** It tells denied apart from a muted microphone; whether it reports the terminal's status correctly from Terminal and iTerm is taken from E2-1's findings, and if it does not, this spec is revised before implementing. (Owner's choice.)
- **Speakers from CoreAudio properties (transport type built-in + data source internal speakers).** Name matching misses headphones in the jack on an Intel Mac, where the device name stays the same. (Owner's choice.)
- **Builds on E2-2:** devices are opened through E2-2's PortAudio duplex stream, and `--passthrough` is the place the checks run before E6.
- **A warning, not a stop, for speakers** (from the issue and the constitution's Feedback row).
- **Not yet asked → continue** (default): macOS then asks the owner itself, which is the normal first-run experience.
- **Status unreadable → continue silently** (default): a failed check must never block a working microphone.

## Open Questions
- None
