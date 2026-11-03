# Requirements spec: solid-backup (experiment)

Oct 30, 2026 · @Björn Petersson · Approved by the owner on Oct 30, 2026

solid-backup is a .NET console app for macOS that listens to the computer's built-in microphone or a connected one and plays chords in response to the pitch and volume of what comes in. It is part accompanist (you sing, whistle or play a melody, one note at a time, and the app backs you with chords that fit) and part instrument (pitch and volume are control signals that shape which chords are played and how they sound). The sound is made by the app's own simple synthesizer. The app also measures itself: pitch accuracy and latency have numeric targets that are tested.

## Goal and scope

The experiment should show whether a local console app can follow a single live melody line and back it with chords that sound musically right and feel immediate (under about 50 ms), and make that measurable with tests on recorded audio.

**In scope**

- Audio in from the default or a chosen input device (built-in or connected microphone), and audio out to a chosen output device, through PortAudio
- Monophonic pitch detection (one note at a time: voice, whistling, wind instruments, single guitar notes)
- Level (volume) analysis with features that can each be switched on or off in the settings:
  - chord loudness follows the input level (dynamics)
  - a noise gate: below a threshold nothing is played
  - onsets: a sudden rise in level triggers a new chord
  - chord character: a soft note gives a sparse chord, a loud note a fuller chord, a tension note or a different timbre
  - more level-driven features can be added later behind the same switch mechanism
- Chord choice from the detected note, within a key, in a harmony style chosen in the menu (Church, Campfire, Drone, Pop, Modal, Blues, Jazz, Free; decided in E4-1, see `docs/harmony.md`)
- Key: chosen in the menu, either a fixed key (a tonic and a mode) or `auto`, where the app estimates the key from what you sing, shows it, and follows a modulation
- Rhythm accompaniment: chords can be played in a pattern (held, arpeggio, strum) at a fixed tempo chosen in the menu (`docs/rhythm.md`)
- A built-in synthesizer (oscillators, envelope, a few sounds such as pad, organ and pluck), with no external instruments
- Recording a session to WAV (the microphone signal and the chords)
- An interactive menu at start, where you choose devices, key, sound, rhythm pattern and which level features are on. The choices are saved and become the defaults next time.
- A live dashboard while playing: the detected note (e.g. `A4 +12 cent`), a level meter, the current chord, the key, which features are on, and the measured latency
- Measurement against targets (see Measurement), tested on generated tones and recorded WAV files

**Out of scope**

- Polyphonic input (detecting chords or several notes at once)
- Windows and Linux
- MIDI in or out, SoundFonts and samples, plugins (VST/AU)
- Echo cancellation: headphones are assumed (see Open points)
- Changing settings with keys while in live mode: settings are changed in the menu, before starting
- GUI, web UI or hosted services; everything runs locally

## Open points and assumptions

| Point | Assumption in the spec | Needs confirmation |
| --- | --- | --- |
| Chord choice (music theory) | The detected note is mapped to a chord by the chosen harmony style. In the styles whose rules say so (Church, Campfire, Pop, Jazz) the chord contains the note and fits the key; Drone and Modal work as a pedal, Blues keeps the chord on blue notes, and Free has no key and chooses among all three-tone chords, moving the root at least every other change. The rules, including how the previous chord matters and how often the chord may change, are in `docs/harmony.md`. | Decided in E4-1 |
| Tempo source | A fixed BPM (40–240) and time signature (4/4 or 3/4) chosen in the menu. The grid starts with live mode and onsets do not move it. Arpeggio and Strum use a new chord from their next step; Held changes at once. The rules are in `docs/rhythm.md`. | Decided in E5-4 |
| Feedback | The chords are played in headphones. If the output device is the built-in speakers, the app shows a warning in the menu and on the dashboard. There is no echo cancellation. Monitoring (E7-3), when on, plays the microphone only to an output known not to be the built-in speakers, and live mode stops it for the session if the output changes to them. | None |
| Platform | macOS on Intel and Apple Silicon. PortAudio's CoreAudio backend through `PortAudioSharp2`, which ships the native library for `osx-x64` and `osx-arm64`, so no Homebrew step. macOS asks for microphone access on behalf of the terminal app that runs the process. Verified in E2-1 on an Intel Mac; the findings are in the README ("Audio on macOS"). | Apple Silicon is a manual check (E2-1) |
| Latency | Under 50 ms from the sound reaching the microphone until the chord is heard: input latency + analysis + output latency, where the device latencies are those PortAudio reports. Pitch detection needs about two periods of the lowest note, so the lowest supported note sets a floor: E2 (82 Hz) needs ~25 ms of audio. E2-1 found that a duplex stream across two devices adds a large hidden buffer, so input and output run as two streams with a lock-free hand-over, at 48 kHz with 64 frames by default (~53 ms reported in total). The built-in MacBook Pro microphone alone reports ~45 ms, so with it the target is missed before any analysis; meeting it needs a lower-latency microphone. The figures are in the README ("Audio on macOS"). | Measured in E2-1. In E6-1 the chain's own delay measured 34 ms (Pad; ~31 ms Organ and Pluck), and the built-in microphone with the speakers reported 89.9 ms in total; the target with a low-latency microphone and headphones is the owner's check |
| Pitch range | E2 to C7 (82–2093 Hz) by default, configurable. Covers singing voices, whistling and most wind instruments. | Confirmed by the owner (2026-10-30); the top raised from C6 to C7 by the owner in E3-1 (2026-10-31), since whistling reaches about C7 |
| Real time | The audio callback never allocates, locks or does I/O, so the GC and the console can never cause a dropout. Analysis and synthesis run on the audio thread or a dedicated thread with lock-free hand-over; the dashboard reads a snapshot. | None |
| Stack | .NET 8 (LTS), C#, PortAudio through `PortAudioSharp2`, `System.CommandLine` for flags, `Spectre.Console` for the menu and the dashboard, xUnit for tests. | Binding chosen in E2-1 |
| Settings | Defaults in `appsettings.json` (pitch range, buffer size, gate threshold, latency target). The menu's choices are saved to a git-ignored user settings file that overrides the defaults. No secrets are needed. | None |
| Language | Code, specs, commits and PRs in English. Text shown to the user in the console is in Swedish. | None |

## Flow and technical constraints

The system has three flows: setup, live play and measurement.

**Setup (menu)**

1. The app starts without arguments and shows the menu with the saved choices as defaults.
2. You choose input and output device, key (fixed or `auto`), sound, rhythm pattern and which level features are on.
3. The app checks the microphone permission and the output device (warns about speakers), saves the choices and starts live mode.

**Live play**

1. Each input buffer is analysed: level, onset, and the pitch with a confidence value.
2. The gate and the confidence decide whether there is a note. The note and the key (fixed, or estimated in `auto`) give a chord (E4).
3. The chord, the level features that are on, and the rhythm pattern decide what the synth plays and how loud.
4. The dashboard shows the state a few times per second. Recording, if on, writes the input and the output to WAV.
5. `Esc` or `q` fades the sound out, closes the recording and returns to the menu. `Ctrl+C` quits the app and also closes the audio and the recording cleanly.

**Measurement**

1. Generated tones and recorded WAV files are run through the same pipeline as live audio, buffer by buffer, without audio hardware.
2. The measures in Measurement are reported and checked against the targets.

**Technical constraints**

- Everything runs locally. There are no external services and no network calls.
- Audio goes through two interfaces, `IAudioInput` and `IAudioOutput`, with PortAudio as the real implementation and WAV-file input and in-memory output for tests.
- Pitch detection, level analysis, harmony and synthesis are pure, deterministic and buffer based, so a WAV file gives the same result every time.
- Every level feature is a separate, switchable component, so new ones can be added without changing the rest.
- No allocation, locks or I/O in the audio callback.
- Tests run without audio hardware and without network access.

## Parts and issues

Six parts (E1–E6). Each item is meant to be one issue that takes roughly half a day to a full day, and "Done when" is its acceptance criterion.

### E1 Project foundation

- [ ] **E1-1 Repo and skeleton**: git repo with `main` and `develop`, a private GitHub repo and project board, and a .NET 8 solution with an app project and a test project.
  - Done when: `dotnet build` and `dotnet test` run from the repo root, and a GitHub Actions workflow runs them on every PR.
- [ ] **E1-2 Claude workflow**: `CLAUDE.md`, the skills and `set-issue-status.sh` ported from BörsMönster, with the project-specific parts (repo name, board, release checks) changed for solid-backup.
  - Done when: `/spec-from-issue` can be run on an issue in this repo, and the canonical check `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` is documented in `CLAUDE.md`.
- [ ] **E1-3 Configuration and saved settings**: defaults from `appsettings.json`, overridden by a git-ignored user settings file that the menu writes.
  - Done when: a missing or invalid user settings file falls back to the defaults with a Swedish message, and saved choices are read back on the next start.
- [ ] **E1-4 CLI and menu skeleton**: `System.CommandLine` with `--help` and `--version`, and an empty `Spectre.Console` menu when no arguments are given.
  - Done when: `--help` works, and starting without arguments shows the menu.

### E2 Audio in and out

- [ ] **E2-1 PortAudio spike on macOS**: verify a .NET PortAudio binding on Apple Silicon: device listing, opening input and output, the smallest stable buffer size, the latencies PortAudio reports, and how the microphone permission behaves in Terminal and iTerm.
  - Done when: the findings are documented in the README, and the assumptions in this spec are updated.
- [ ] **E2-2 Audio interfaces**: `IAudioInput` and `IAudioOutput` with PortAudio implementations, a WAV-file input and an in-memory output for tests.
  - Done when: a WAV file can be run through the pipeline in a test without audio hardware, and the PortAudio implementation passes the microphone through to the output in a manual check.
- [ ] **E2-3 Devices and permission**: list and choose input and output devices, detect a missing microphone permission and a speaker output.
  - Done when: a missing permission gives a Swedish message with instructions, and the built-in speakers as output give a warning.

### E3 Analysis

- [ ] **E3-1 Test corpus**: generated tones (sine, sawtooth, with noise, glides) and recorded WAV files (voice, whistling), each with a reference file of the right note per time window.
  - Done when: the corpus is in the repo, small enough for git, and documented.
- [ ] **E3-2 Pitch detection**: a monophonic pitch detector (e.g. YIN) with note, cents and confidence per buffer, within the configured range.
  - Done when: the pitch measures in Measurement reach their targets on the corpus.
- [ ] **E3-3 Level, gate and onsets**: RMS level in dBFS, a noise gate with hysteresis, and onset detection.
  - Done when: silence and breath noise in the corpus trigger no note, and the onsets in the reference files are found within 20 ms.

### E4 Harmony

- [ ] **E4-1 Music theory discussion**: decide how a note becomes a chord (see Open points). This issue gives a written decision in `docs/harmony.md`, not code.
  - Done when: the owner has approved `docs/harmony.md`, and E4-2 is updated to match it.
- [ ] **E4-2 Chord choice**: the harmony engine and common rules from `docs/harmony.md`, as pure functions from note, key and state to a chord, with the settings and the styles Church, Campfire and Drone.
  - Done when: the timing, Church, Campfire and Drone examples in `docs/harmony.md` are tests that pass.
- [ ] **E4-3 Key detection**: `auto` estimates the tonic and major or minor from the recent notes, shows it, and changes it on a modulation without flickering.
  - Done when: a melody in a known key in the corpus gives that key, and a melody that modulates gives the new key within a configurable number of seconds.
- [ ] **E4-4 Pop and Modal styles**: the Pop and Modal rules from `docs/harmony.md`.
  - Done when: the Pop and Modal examples in `docs/harmony.md` are tests that pass.
- [ ] **E4-5 Blues style**: the Blues rules from `docs/harmony.md`, including blue notes and moving the key.
  - Done when: the Blues examples in `docs/harmony.md` are tests that pass.
- [ ] **E4-6 Free style**: the Free rules from `docs/harmony.md`, with no key and chosen by voice leading.
  - Done when: the Free examples in `docs/harmony.md` are tests that pass.

### E5 Sound

- [ ] **E5-1 Synthesizer**: oscillators, an ADSR envelope, polyphony for a chord, and a few sounds (pad, organ, pluck), with no clicks at chord changes.
  - Done when: a test renders a chord and finds the right frequencies, and the rendering in the audio callback allocates nothing (verified by a test).
- [ ] **E5-2 Level features**: dynamics, chord character, and onset-triggered chord change, each switchable, combined with the gate from E3-3.
  - Done when: every combination of switches gives the expected output on a corpus file, and a new feature can be added without changing the others.
- [ ] **E5-3 Recording**: record the session's input and output to WAV.
  - Done when: a recording can be played back in another program, and recording does not cause dropouts.
- [ ] **E5-4 Tempo discussion**: decide where the tempo comes from (see Open points). This issue gives a written decision in `docs/rhythm.md`, not code.
  - Done when: the owner has approved `docs/rhythm.md`, and E5-5 is updated to match it.
- [ ] **E5-5 Rhythm patterns**: held, arpeggio and strum on the tempo grid from `docs/rhythm.md` (`RhythmPattern`, `Bpm`, `TimeSignature`), with its examples as tests.
  - Done when: a rendered pattern has its notes on the expected samples within 1 ms.
- [ ] **E5-6 Jazz style**: the Jazz rules, whose details are decided in this issue from the direction in `docs/harmony.md` and the tempo in `docs/rhythm.md`.
  - Done when: `docs/harmony.md` has the detailed Jazz rules with examples, approved by the owner, and the examples are tests that pass.

### E6 Live mode

- [ ] **E6-1 Live pipeline**: input → analysis → harmony → synth → output, running in real time, with lock-free hand-over to the dashboard.
  - Done when: the latency target in Measurement is met on the owner's Mac, and an hour of play gives no dropouts.
- [ ] **E6-2 Menu**: devices, key, sound, rhythm pattern, level features and recording, saved as the next start's defaults; then live mode, and back to the menu with `Esc` or `q`.
  - Done when: every choice in the menu changes the live behaviour, and the choices are remembered.
- [ ] **E6-3 Live dashboard**: note and cents, level meter, chord, key, features on, measured latency and a dropout counter, updated a few times per second.
  - Done when: the dashboard is readable at 120 characters width, and updating it causes no dropouts.

## Order and dependencies

Verify the audio stack first, because everything else assumes that PortAudio works with low latency from .NET on macOS. The music theory discussion (E4-1) can be held in parallel with E2 and E3.

| Phase | Issues | Requires | Done when |
| --- | --- | --- | --- |
| 1. Foundation | E1-1 to E1-4 | – | Build, tests, CI and the Claude workflow run |
| 2. Audio | E2-1 to E2-3 | E1-1 | The microphone can be heard in the headphones through the app |
| 3. Analysis | E3-1 to E3-3 | E2-2 | Note and level are detected correctly on the corpus |
| 4. Harmony | E4-1 to E4-6 | E3-2 (E4-2, E4-3); E4-2 (E4-4 to E4-6) | A melody gives chords that follow the rules in `docs/harmony.md` |
| 5. Sound | E5-1 to E5-6 | E4-2; E5-4 (E5-6) | Chords are heard with the chosen sound, dynamics and pattern |
| 6. Live mode | E6-1 to E6-3 | Phase 5 | The demo can be run from the menu |

## Measurement

The experiment is successful if the app reaches the targets below and the owner finds that the chords sound right and feel immediate when playing live.

| Measure | How it is measured | Target |
| --- | --- | --- |
| Pitch accuracy | Share of voiced windows in the corpus where the detected note is within ±50 cents of the reference | ≥ 95 % |
| Octave errors | Share of voiced windows detected an octave or more off | ≤ 1 % |
| False notes | Share of unvoiced windows (silence, breath, noise) where a note is detected | ≤ 2 % |
| Onset accuracy | Share of reference onsets found within 20 ms | ≥ 90 % |
| Latency | Input latency + analysis + output latency (PortAudio's reported device latencies), measured live and shown on the dashboard, with the Held pattern. The deliberate wait for the next step in Arpeggio and Strum is not latency (`docs/rhythm.md`) | < 50 ms |
| Dropouts | Buffer underruns and overruns reported by PortAudio during an hour of play | 0 |
| Allocation in the audio callback | Bytes allocated per callback, measured in a test | 0 |

## Demo

The experiment is done when the following scenario can be run from your computer without anything being swapped by hand.

1. You plug in headphones, start the app without arguments and see the menu with last time's choices.
2. You choose the microphone, key `auto`, the sound "pad", the pattern "arpeggio", turn on dynamics, gate and onsets, and turn on recording.
3. You sing a simple melody. The dashboard shows the note, the level, the estimated key and the chord, and the chords follow you on the pattern's next step (with Held, within under 50 ms).
4. You sing louder and softer, and the chords follow in loudness and character. When you are silent, nothing is played.
5. You leave live mode, and the recording can be played back.

Send the spec to Claude Code together with this instruction: create one issue per item in Parts and issues, with "Done when" as acceptance criteria and dependencies as in Order and dependencies.
