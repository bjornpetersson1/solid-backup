# solid-backup

An experimental .NET console app for macOS. It listens to a single melody line from a microphone, detects its pitch and level, and plays chords in response through its own synthesizer. The requirements are in `Constitution/Requirements-and-goal.md`.

The audio, analysis, harmony and sound parts are still to come (epics E2–E6); so far the app has its command line, an empty menu and its settings.

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
```

`--passthrough` plays the chosen input in the chosen output until you press `q`, `Esc` or Ctrl+C, then prints the dropout counts. Use headphones: there is no echo cancellation, and the app warns when the output is the built-in speakers. If the terminal app has no microphone access, it stops with instructions instead of starting (System Settings → Privacy & Security → Microphone).

`--devices` lists the inputs and outputs and marks the system defaults, the chosen devices and the built-in speakers. `--input` and `--output` choose a device by its exact name (case does not matter) for that run only; they are not saved.

The menu needs an interactive terminal.

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

## Settings

Defaults are in `src/SolidBackup/appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `LowestNote` | `E2` | Lowest note of the pitch range |
| `HighestNote` | `C6` | Highest note of the pitch range |
| `BufferSize` | `64` | Audio buffer size in frames (a power of two) |
| `SampleRate` | `48000` | Sample rate in Hz (44100 or 48000) |
| `InputDevice` | `""` | Input device name; empty means the system default |
| `OutputDevice` | `""` | Output device name; empty means the system default |
| `GateThresholdDbfs` | `-50` | Noise gate threshold in dBFS (at most 0) |
| `LatencyTargetMs` | `50` | Latency target in milliseconds |

The menu's choices are saved to `~/Library/Application Support/solid-backup/usersettings.json`, which only holds the settings that differ from the defaults. A missing or invalid user settings file is ignored with a message, and the defaults are used.
