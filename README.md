# solid-backup

An experimental .NET console app for macOS. It listens to a single melody line from a microphone, detects its pitch and level, and plays chords in response through its own synthesizer. The requirements are in `Constitution/Requirements-and-goal.md`.

So far the app has its command line, an empty menu and its settings (E1); the audio layer, with pass-through and device listing (E2); and the analysis, measured on a test corpus (E3): pitch detection, level, a noise gate and onsets, which are not yet wired into live play. Harmony, sound and live mode (E4–E6) are still to come.

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
dotnet run --project src/SolidBackup              # the menu (choose "Avsluta" to quit)
dotnet run --project src/SolidBackup -- --help    # help
dotnet run --project src/SolidBackup -- --version # version
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

`--record` (with `--passthrough`) records the session to a stereo 16-bit WAV file in `RecordingFolder` (default `recordings/`, git-ignored), named after the start time, e.g. `recordings/2026-10-31_14-05-12.wav`: the input on the left channel and the output on the right, in sync. The file is written from a background thread, so recording never holds up the audio; if the disk falls more than 2 s behind, recorded buffers are dropped and counted instead. The app prints the path when it starts and the path, length and dropped buffers when it stops.

`--devices` lists the inputs and outputs and marks the system defaults, the chosen devices and the built-in speakers. `--input` and `--output` choose a device by its exact name (case does not matter) for that run only; they are not saved.

`--harmonize <wav>` runs the pitch detection and the harmony (see [`docs/harmony.md`](docs/harmony.md)) over a WAV file and prints the time, the note and the chord at every chord change. `--style`, `--key` and `--mode` override `HarmonyStyle`, `Key` and `Mode` for that run only. It also prints the key when `auto` first finds it and whenever the key changes, by `auto` or by Blues moving it. The file's own sample rate is used (44100 or 48000 Hz). `--harmonize` cannot be combined with `--passthrough` or `--devices`.

The menu needs an interactive terminal.

`--render <wav>` plays chords with the built-in synthesizer and writes them to a mono 16-bit WAV file at `SampleRate`; `--play` plays them on the output device (the setting or `--output`) until the end, or until `q`, `Esc` or Ctrl+C. `--sound` (`Pad`, `Organ` or `Pluck`) overrides `Sound` for that run, `--chords` takes chord symbols as in [`docs/harmony.md`](docs/harmony.md) separated by spaces (default `"C Am F G"`), and `--chord-ms` how long each lasts (100–10000 ms, default 1000); the last chord's release ends the sound. The first chord is in close voicing with its root in octave 3; each next one keeps the tones it shares with the one before and moves the others as little as it can, within C3–C5.

`--accompany <wav> --render <out.wav>` runs a recording through the whole chain, as live mode will: pitch, level, gate and onsets, the level features that are on, the harmony and the synthesizer playing the chord held. It writes the voice mixed with the accompaniment to a 16-bit WAV at the recording's rate, or the accompaniment alone with `--no-voice`. `--style`, `--key`, `--mode` and `--sound` override the settings for that run, and `--features` the four level-feature switches: a comma-separated list of `gate`, `onsets`, `character` and `dynamics`, or `none`.

`--render`, `--play` and `--accompany` play the chords with the rhythm pattern in `RhythmPattern` (`Held`, `Arpeggio` or `Strum`) at `Bpm` in `TimeSignature`, as [`docs/rhythm.md`](docs/rhythm.md) describes; `--pattern`, `--bpm` and `--time-signature` override them for that run. The grid starts at the first sample, Arpeggio plays one chord tone per eighth note and Strum strikes the chord on every beat, 10 ms between tones, both with beat 1 a little stronger; with a tempo, a new chord is heard from the pattern's next step.

Each way of running (`--passthrough`, `--devices`, `--harmonize`, `--accompany`, `--render`, `--play`) excludes the others, and an option that does not belong to the chosen one gives a Swedish error.

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
| `HarmonyStyle` | `Church` | Harmony style: `Church`, `Campfire`, `Drone`, `Pop`, `Modal`, `Blues` or `Free` (see [`docs/harmony.md`](docs/harmony.md)); `Jazz` is accepted but not built yet (E5-6) |
| `Key` | `auto` | The key's tonic (`C`, `F#`, `Bb`, …), or `auto` to estimate the tonic and major or minor from the notes |
| `Mode` | `Ionian` | The key's mode: `Ionian`, `Dorian`, `Phrygian`, `Lydian`, `Mixolydian`, `Aeolian` or `Locrian`; ignored with `auto` |
| `MinNoteForChangeMs` | `120` | While singing, a new note must last this long before it may change the chord (above 0) |
| `MinChordDurationMs` | `250` | While singing, the shortest time between two chord changes (above 0) |
| `PhraseResetMs` | `2000` | After a silence this long, the previous chord is forgotten (above 0) |
| `KeyChangeSeconds` | `4` | With `auto`, a modulation is followed within this many seconds (above 0) |
| `KeyFirstSeconds` | `2` | With `auto`, no key (and no chord) until at least this many seconds of notes have been heard (above 0) |
| `Sound` | `Pad` | The synthesizer's sound: `Pad`, `Organ` or `Pluck` |
| `GateOn` | `true` | Level feature: notes count, and the chord sounds, only while the noise gate is open; off, the chord keeps sounding through silence until `PhraseResetMs` |
| `DynamicsOn` | `true` | Level feature: the chord's strength follows the input level |
| `ChordCharacterOn` | `true` | Level feature: soft singing gives a sparse chord (root and fifth), loud singing a full one (with the root an octave down) |
| `OnsetsOn` | `true` | Level feature: an articulated note changes the chord at once, without the hold times, and strikes it anew |
| `DynamicsQuietDbfs` | `-42` | Dynamics: at this level and below the chord plays 24 dB below full strength (−90 to 0, below `DynamicsLoudDbfs`) |
| `DynamicsLoudDbfs` | `-12` | Dynamics: at this level and above the chord plays at full strength (−90 to 0) |
| `CharacterSparseBelowDbfs` | `-30` | Chord character: below this level the chord is sparse (−90 to 0, at least 6 dB below `CharacterFullAboveDbfs`) |
| `CharacterFullAboveDbfs` | `-18` | Chord character: above this level the chord is full (−90 to 0); a level must pass a boundary by 3 dB to change the character |
| `RhythmPattern` | `Held` | How the chord is played: `Held`, `Arpeggio` or `Strum` (see [`docs/rhythm.md`](docs/rhythm.md)) |
| `Bpm` | `90` | Tempo for Arpeggio and Strum, a whole number from 40 to 240 |
| `TimeSignature` | `4/4` | `4/4` or `3/4` |
| `RecordingFolder` | `recordings` | Where `--record` puts its WAV files: a folder relative to the current directory, or an absolute path; created if missing |

The menu's choices are saved to `~/Library/Application Support/solid-backup/usersettings.json`, which only holds the settings that differ from the defaults. A missing or invalid user settings file is ignored with a message, and the defaults are used.
