# solid-backup

An experimental .NET console app for macOS. It listens to a single melody line from a microphone, detects its pitch and level, and plays chords in response through its own synthesizer. The requirements are in `Constitution/Requirements-and-goal.md`.

The app has its command line and its settings (E1); the audio layer, with pass-through and device listing (E2); the analysis, measured on a test corpus (E3); the harmony (E4); the synthesizer, level features, recording and rhythm patterns (E5); live mode (E6): the live pipeline with `--live`, the start menu and the live dashboard; and the playtest changes (E7): the chord change rate and sensitivity, autoplay through silence, input monitoring, and Free with three-tone chords.

## Requirements

- macOS
- .NET 8 SDK 8.0.402 or a later 8.0.4xx patch (pinned in `global.json`)

## Build and test

From the repo root, the same checks as CI:

```bash
dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test
```

Tests need no audio hardware and no network.

## Run

```bash
dotnet run --project src/SolidBackup              # the menu: choose, then "Starta live"
dotnet run --project src/SolidBackup -- --help    # help
dotnet run --project src/SolidBackup -- --version # version
dotnet run --project src/SolidBackup -- --live        # live mode: chords to what you sing
dotnet run --project src/SolidBackup -- --live --style Pop --sound Organ --pattern Held --record
dotnet run --project src/SolidBackup -- --passthrough # play the microphone in the output
dotnet run --project src/SolidBackup -- --devices     # list the audio devices
dotnet run --project src/SolidBackup -- --passthrough --input "USB Audio" --output "Externa hörlurar"
dotnet run --project src/SolidBackup -- --passthrough --record  # and record it to recordings/
dotnet run --project src/SolidBackup -- --harmonize corpus/recordings/singing.wav --style Campfire --key G
dotnet run --project src/SolidBackup -- --render chords.wav --sound Organ --chords "C Am F G7" --chord-ms 800
dotnet run --project src/SolidBackup -- --play --pattern Arpeggio --bpm 100 --time-signature 3/4
dotnet run --project src/SolidBackup -- --play --sound Pluck --output "Externa hörlurar"
dotnet run --project src/SolidBackup -- --accompany corpus/recordings/singing.wav --render sung.wav --features gate,dynamics
```

`--passthrough` plays the chosen input in the chosen output until you press `q`, `Esc` or Ctrl+C, then prints the dropout counts. Use headphones: there is no echo cancellation, and the app warns when the output is the built-in speakers. If the terminal app has no microphone access, it stops with instructions instead of starting (System Settings → Privacy & Security → Microphone). On first use macOS asks for access; answer the dialog, and run again if the audio does not start.

`--live` is live mode: the input runs through pitch, level, gate and onsets, the level features that are on and the harmony on the input stream's thread, and the rhythm pattern and the synthesizer play the chords on the output stream's thread, until `q`, `Esc` or Ctrl+C. Only the latest decision crosses from one stream to the other, so input jitter and clock drift can delay a chord by a buffer but never cut the sound, and the pattern's grid counts the output's samples. It runs the same microphone and speaker checks as `--passthrough`. `--input`, `--output`, `--style`, `--key`, `--mode`, `--sound`, `--features`, `--pattern`, `--bpm`, `--time-signature` and `--record` work as with `--passthrough` and `--accompany`, for that run only. While it plays, the **dashboard** is redrawn in place about five times a second. It is one panel with these rows: the note and cents, the chord, the key (marked `(auto)` when estimated), a level meter from −60 to 0 dBFS with the gate threshold marked `│` and whether the gate is open, the style, sound and rhythm, the chord change rate and sensitivity (Byten), autoplay on or off (Autospel), monitoring with its level and, while it runs, how often the monitored input was interrupted (`avbrott`), off, or off because the output is not safe (Medhörning), each level feature on (✓) or off (✗), the latency (total, marked against `LatencyTargetMs`, and its parts) and the dropouts, with the recording's length and dropped buffers when recording. It also shows the speaker warning when the output is the built-in speakers, and, above the panel, a note when monitoring is on but the output is not safe for it; the output is checked again twice a second while live mode monitors. It fits 120 columns with the widest values and 80 columns with ordinary ones. It only reads snapshots, so it cannot hold up the audio. At the end live mode prints the dropouts and the latency parts.

The **latency** is the input stream's reported latency + the chain's delay + one buffer for the hand-over + the output stream's reported latency. The chain's delay is measured when live mode starts: an A3 tone at −20 dBFS after 500 ms of silence runs offline through a fresh chain with the chosen settings, with the Held pattern (the step wait of Arpeggio and Strum is musical timing) and, with the key `auto`, in C major (the key estimate's first wait happens once per session), and the delay is the time until the chord's first sample above −60 dBFS. With the defaults it is 34 ms with Pad and about 31 ms with Organ and Pluck, whose faster attacks are heard sooner. If the tone never gives a chord (e.g. a gate threshold above −20 dBFS), the delay is shown as `?`. **Dropouts** are the input overflows and output underflows PortAudio reports, as in Measurement.

`--record` (with `--passthrough` or `--live`) records the session to a stereo 16-bit WAV file in `RecordingFolder` (default `recordings/`, git-ignored), named after the start time, e.g. `recordings/2026-10-31_14-05-12.wav`: the input on the left channel and the output on the right, in sync. In live mode the input is recorded from the input's thread and the output from the output's, each through its own ring, and the output's recording starts with its first buffer after the input's first. A side whose buffers were dropped gets silence in their place, so the two stay in step, and since two devices run on two clocks, a side that stays more than 10 ms (or one buffer, if longer) ahead for half a second has its lead dropped from the recording, with a 2 ms crossfade so the drop does not click. The file is written from a background thread, so recording never holds up the audio; if the disk falls more than 2 s behind, recorded buffers are dropped and counted instead. The app prints the path when it starts and the path, length, dropped buffers and channel adjustments (crossfaded drift drops, `kanaljusteringar`) when it stops.

`--devices` lists the inputs and outputs and marks the system defaults, the chosen devices and the built-in speakers. `--input` and `--output` choose a device by its exact name (case does not matter) for that run only; they are not saved.

`--harmonize <wav>` (with `--bpm` and `--time-signature` for the grid that Jazz and `ChordChangesPerBar` use; with `ChordChangesPerBar` `Off`, changes come as soon as the hold times allow) runs the pitch detection and the harmony (see [`docs/harmony.md`](docs/harmony.md)) over a WAV file and prints the time, the note and the chord at every chord change. `--style`, `--key` and `--mode` override `HarmonyStyle`, `Key` and `Mode` for that run only. It also prints the key when `auto` first finds it and whenever the key changes, by `auto` or by Blues or Jazz moving it. The file's own sample rate is used (44100 or 48000 Hz). It cannot be combined with another way of running.

Started without arguments, the app shows the **menu**: a table of the current choices and one item each for the input, the output, the key (`auto`, or a tonic and then a mode), the style, the sound, the rhythm (pattern, tempo 40–240 BPM, time signature), the chord changes (Ackordbyten: the most per bar, then the sensitivity), the level features (any of them, or none), autoplay (Autospel, on or off), monitoring (Medhörning: on or off, then the level 0–100 %) and recording (on or off), plus `Starta live` (preselected), `Inspelningar`, `Importera klipp` and `Avsluta`. **Inspelningar** lists the WAV files in `RecordingFolder`, newest first, with their date and length (`oläsbar` for a file that cannot be read). A recording can be heard on the chosen output, at the file's own rate, as both channels mixed, the microphone only or the chords only: space pauses, ← and → seek 5 s, and q or Esc stop. It can also be renamed (you type the name without `.wav`; empty, invalid and taken names are refused; Esc cancels) or deleted, after a j/n question. **Importera klipp** takes an audio file's path, typed or dragged in from Finder: WAV (16-, 24- or 32-bit PCM or 32-bit float, mono or stereo, at its own rate), or M4A/AAC, MP3, AIFF or CAF, which macOS's own `/usr/bin/afconvert` converts to WAV at `SampleRate` first (locally; the only program the app starts). Clips of up to 20 minutes are taken. A run menu then shows the run's settings, starting from the saved ones; key, style, sound, rhythm, chord changes, level features and autoplay can be changed for this run only, and nothing is saved. `Kör` runs the clip through the same chain as `--accompany` (Esc cancels) and saves it in `RecordingFolder` as `<clip>_<time>.wav`, stereo with the clip on the left and the chords on the right, so it can be heard at once with the player above and is listed under Inspelningar. Every choice is saved at once to the user settings file below and is the next start's default; a choice that cannot be saved is still used for the session. A saved device that is no longer there is marked `(saknas)`, and the menu shows the speaker warning when the output is the built-in speakers, and a note when monitoring is on but the output is not safe for it. `Starta live` runs live mode as `--live` does, recording when recording is on; `q` or `Esc` lets the chord ring out (at most 1 s) and returns to the menu, and Ctrl+C quits the app once the audio and the recording are closed (with the exit code of live mode, 1 if the recording could not be saved). In the menu itself Ctrl+C quits at once, as everything is already saved. The menu needs an interactive terminal.

`--render <wav>` plays chords with the built-in synthesizer and writes them to a mono 16-bit WAV file at `SampleRate`; `--play` plays them on the output device (the setting or `--output`) until the end, or until `q`, `Esc` or Ctrl+C. `--sound` (`Pad`, `Organ` or `Pluck`) overrides `Sound` for that run, `--chords` takes chord symbols as in [`docs/harmony.md`](docs/harmony.md) separated by spaces (default `"C Am F G"`), and `--chord-ms` how long each lasts (100–10000 ms, default 1000); the last chord's release ends the sound. The first chord is in close voicing with its root in octave 3; each next one keeps the tones it shares with the one before and moves the others as little as it can, within C3–C5.

`--accompany <wav> --render <out.wav>` runs a recording through the whole chain, as live mode will: pitch, level, gate and onsets, the level features that are on, the harmony (with the chord change rate) and the synthesizer playing the chords with the rhythm pattern, through silence when `Autoplay` is on, which the closing message then says. It writes the voice mixed with the accompaniment to a 16-bit WAV at the recording's rate, or the accompaniment alone with `--no-voice`. `--style`, `--key`, `--mode` and `--sound` override the settings for that run, and `--features` the four level-feature switches: a comma-separated list of `gate`, `onsets`, `character` and `dynamics`, or `none`.

`--render`, `--play` and `--accompany` play the chords with the rhythm pattern in `RhythmPattern` (`Held`, `Arpeggio` or `Strum`) at `Bpm` in `TimeSignature`, as [`docs/rhythm.md`](docs/rhythm.md) describes; `--pattern`, `--bpm` and `--time-signature` override them for that run. The grid starts at the first sample, Arpeggio plays one chord tone per eighth note and Strum strikes the chord on every beat, 10 ms between tones, both with beat 1 a little stronger; with a tempo, a new chord is heard from the pattern's next step.

Each way of running (`--live`, `--passthrough`, `--devices`, `--harmonize`, `--accompany`, `--render`, `--play`) excludes the others, and an option that does not belong to the chosen one gives a Swedish error.

## Audio on macOS (E2-1 findings)

Measured with the spike in `spikes/PortAudioSpike` (not part of the solution):

```bash
dotnet run --project spikes/PortAudioSpike -- devices
dotnet run --project spikes/PortAudioSpike -- latency [--rate 44100]
dotnet run --project spikes/PortAudioSpike -- stability <frames> <seconds> [--separate] [--mute]
```

**Binding.** `PortAudioSharp2` 1.0.6 (PortAudio V19.7.0-devel). It brings the native `libportaudio.dylib` through the `org.k2fsa.portaudio.runtime.*` packages, and the `osx-x64` one loaded without Homebrew. It wraps devices and streams but not `Pa_GetStreamInfo` or `Pa_GetHostApiInfo`; the spike reaches those through its own P/Invoke.

**Machine.** MacBook Pro 15,4 (Intel Core i5-8257U), macOS 15.7.9, .NET 8.0.402. Default input: the built-in microphone (48 kHz). Default output: headphones in the jack ("Externa hörlurar", 44.1 kHz), a separate CoreAudio device from the built-in speakers.

**Reported latencies at 48 kHz** (input-only and output-only streams, suggested latency = the device's default low latency):

| Frames | Input (ms) | Output (ms) | Sum (ms) | Duplex in + out (ms) |
| --- | --- | --- | --- | --- |
| 32 | 44.5 | 5.3 | 49.8 | 221.8 |
| 64 | 44.5 | 6.0 | 50.5 | 223.8 |
| 128 | 45.9 | 6.0 | 51.9 | 225.2 |
| 256 | 48.5 | 8.6 | 57.1 | 233.2 |
| 512 | 53.9 | 14.0 | 67.9 | 249.2 |

- The built-in microphone's own CoreAudio stream latency is 1919 frames (40 ms); that, not the buffer, is most of the input latency.
- A duplex stream across two devices reports ~215 ms input latency, also with both devices set to 48 kHz, because PortAudio puts a large ring buffer between the two devices. Input and output therefore run as **two streams** with a lock-free hand-over.
- At 44.1 kHz the microphone is resampled and reports ~141 ms; 48 kHz is the right rate for it.

**Stability.** 60 s of pass-through per buffer size, output muted:

- One duplex stream: 0 input overflows and 0 output underflows at every size from 32 to 512.
- Two streams with a lock-free ring buffer between them (`--separate`): 0 overflows and underflows from PortAudio and 0 ring overruns at every size. CoreAudio delivers the microphone's input in bursts, and the two device clocks drift. The output therefore drops frames only when the backlog has stayed above one buffer for a whole 0.5 s window (1–2 times a minute at 32, 64 and 256 frames, 41 times at 128). The hand-over ran dry once (at start) at 32 and 64 frames, never at 256, and 41 times at 128, each right after a drift correction.

| Frames | Reported in + out (ms) | Average hand-over (ms) | Total (ms) |
| --- | --- | --- | --- |
| 32 | 44.5 + 5.3 | 2.6 | 52.4 |
| 64 | 44.5 + 6.0 | 2.9 | 53.4 |
| 128 | 45.9 + 6.0 | 5.3 | 57.2 |
| 256 | 48.5 + 8.6 | 7.1 | 64.2 |

**Recommended default `BufferSize`: 64.** It is about 4 ms better than 128 and stable, while 32 gains only ~1 ms for twice the callbacks. **With the built-in microphone the 50 ms target is not met even before any analysis:** the microphone's own 40 ms stream latency puts every buffer size above 50 ms. Meeting the target needs a microphone with a lower latency (an external USB microphone or audio interface, to be measured with `latency`). The target itself is unchanged and is checked in E6-1.

**Microphone permission.** macOS asks on behalf of the app that hosts the terminal; the runs above were in VS Code's terminal. Terminal and iTerm are expected to get their own entries, but that is not yet verified here.

- On first use, `Pa_OpenStream` blocked while the dialog was pending, then failed with `PaMacCore (AUHAL) Error on line 661: err='28479'`. The next run, after access was granted, worked.
- With access granted, the input carries sound (peak levels 0.06–0.87 in the runs above).
- Access is managed in System Settings → Privacy & Security → Microphone, per terminal app.
- What a denied permission looks like (silence or an error) was not observed on this machine.

**CoreAudio properties useful for E2-3.** The transport type of all three devices is built-in (`bltn`). The output data source is `hdpn` for the headphones and `ispk` for the built-in speakers.

**Not verified here:** Apple Silicon (`osx-arm64`), iTerm and Terminal permission behaviour, a denied permission, and the acoustic end-to-end latency. These are manual checks for the owner.

## Test corpus

The pitch, level and onset targets are measured on the corpus in `corpus/`: generated tones built in code and recordings with reference files. See [`corpus/README.md`](corpus/README.md) for its contents, formats, how to record and label a clip, the scoring rules, and how to measure the pitch, level, gate and onset analysis (`dotnet run --project tools/SolidBackup.CorpusTool -c Release -- report`).

## Settings

Defaults are in `src/SolidBackup/appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `LowestNote` | `E2` | Lowest note of the pitch range |
| `HighestNote` | `C7` | Highest note of the pitch range |
| `BufferSize` | `64` | Audio buffer size in frames (a power of two) |
| `SampleRate` | `48000` | Sample rate in Hz (44100 or 48000) |
| `InputDevice` | `""` | Input device name; empty means the system default |
| `OutputDevice` | `""` | Output device name; empty means the system default |
| `PitchConfidenceThreshold` | `0.8` | YIN confidence (0–1) a pitch needs to be reported as a note |
| `GateThresholdDbfs` | `-42` | Noise gate threshold in dBFS (at most 0); the gate opens when the level reaches it |
| `GateHysteresisDb` | `3` | The gate closes only below the threshold minus this many dB (above 0, at most 40) |
| `GateHoldMs` | `20` | …and only once the level has stayed there this long (above 0, at most 1000 ms) |
| `OnsetThresholdDb` | `15` | A rise in level of at least this many dB within 20 ms is an onset (above 0, at most 60) |
| `OnsetMinIntervalMs` | `100` | Shortest time between two onsets (above 0, at most 2000 ms) |
| `LatencyTargetMs` | `50` | Latency target in milliseconds |
| `HarmonyStyle` | `Church` | Harmony style: `Church`, `Campfire`, `Drone`, `Pop`, `Modal`, `Blues`, `Jazz` or `Free` (see [`docs/harmony.md`](docs/harmony.md)); Jazz keeps time on the `Bpm` and `TimeSignature` grid |
| `Key` | `auto` | The key's tonic (`C`, `F#`, `Bb`, …), or `auto` to estimate the tonic and major or minor from the notes |
| `Mode` | `Ionian` | The key's mode: `Ionian`, `Dorian`, `Phrygian`, `Lydian`, `Mixolydian`, `Aeolian` or `Locrian`; ignored with `auto` |
| `MinNoteForChangeMs` | `120` | While singing, a new note must last this long before it may change the chord (above 0) |
| `MinChordDurationMs` | `250` | While singing, the shortest time between two chord changes (above 0) |
| `ChordChangesPerBar` | `2` | While singing, the most chord changes per bar, each on an allowed beat of the `Bpm` and `TimeSignature` grid: `1` (beat 1), `2` (beats 1 and 3), `Beat` (every beat) or `Off` (as soon as the hold times allow); the first chord after silence still comes at once. The menu's Ackordbyten choice |
| `ChordChangeSensitivity` | `Medium` | How easily a new note changes the chord: `Low`, `Medium` or `High` multiply `MinNoteForChangeMs` and `MinChordDurationMs` by 2, 1 or 0.5. The menu's Ackordbyten choice |
| `PhraseResetMs` | `2000` | After a silence this long, the previous chord is forgotten (above 0) |
| `KeyChangeSeconds` | `4` | With `auto`, a modulation is followed within this many seconds (above 0) |
| `KeyFirstSeconds` | `2` | With `auto`, no key (and no chord) until at least this many seconds of notes have been heard (above 0) |
| `Sound` | `Pad` | The synthesizer's sound: `Pad`, `Organ` or `Pluck` |
| `GateOn` | `true` | Level feature: notes count, and the chord sounds, only while the noise gate is open; off, the chord keeps sounding through silence until `PhraseResetMs`. With `Autoplay` on, the chord keeps sounding through silence; the gate still decides which notes count |
| `DynamicsOn` | `true` | Level feature: the chord's strength follows the input level |
| `ChordCharacterOn` | `true` | Level feature: soft singing gives a sparse chord (root and fifth), loud singing a full one (with the root an octave down) |
| `OnsetsOn` | `true` | Level feature: an articulated note is ready without the hold times (it changes the chord at once with `ChordChangesPerBar` `Off`, otherwise on the next allowed beat) and strikes the chord anew |
| `DynamicsQuietDbfs` | `-42` | Dynamics: at this level and below the chord plays 24 dB below full strength (−90 to 0, below `DynamicsLoudDbfs`) |
| `DynamicsLoudDbfs` | `-12` | Dynamics: at this level and above the chord plays at full strength (−90 to 0) |
| `CharacterSparseBelowDbfs` | `-30` | Chord character: below this level the chord is sparse (−90 to 0, at least 6 dB below `CharacterFullAboveDbfs`) |
| `CharacterFullAboveDbfs` | `-18` | Chord character: above this level the chord is full (−90 to 0); a level must pass a boundary by 3 dB to change the character |
| `RhythmPattern` | `Held` | How the chord is played: `Held`, `Arpeggio` or `Strum` (see [`docs/rhythm.md`](docs/rhythm.md)) |
| `Bpm` | `90` | Tempo for Arpeggio and Strum, a whole number from 40 to 240 |
| `TimeSignature` | `4/4` | `4/4` or `3/4` |
| `MonitorOn` | `false` | Whether live mode mixes the microphone into the output with the chords, so you hear yourself in the headphones. Only to an output whose route is known not to be the built-in speakers (an unreadable route counts as unsafe), checked again twice a second during live mode, which stops the monitoring for the session if a headphone is pulled out. The menu's Medhörning choice |
| `MonitorLevelPercent` | `70` | The monitored microphone's level, 0–100 % of the input. The monitored input waits in a small jitter buffer (at most about 5 ms), kept in step with the output by short crossfades, so uneven callbacks and two devices' clocks do not make it crackle |
| `Autoplay` | `false` | Whether the pattern keeps playing the last chord on the grid through silence, however long: the gate does not silence it and it does not stop after `PhraseResetMs` (the harmony still resets). The menu's Autospel choice |
| `RecordingFolder` | `recordings` | Where `--record` puts its WAV files: a folder relative to the current directory, or an absolute path; created if missing |
| `RecordOn` | `false` | Whether live mode records the session; the menu's Inspelning choice. `--live --record` records for one run also when it is off |

The menu's choices are saved to `~/Library/Application Support/solid-backup/usersettings.json`, which only holds the settings that differ from the defaults. A missing or invalid user settings file is ignored with a message, and the defaults are used.
