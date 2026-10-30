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
```

The menu needs an interactive terminal.

## Settings

Defaults are in `src/SolidBackup/appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `LowestNote` | `E2` | Lowest note of the pitch range |
| `HighestNote` | `C6` | Highest note of the pitch range |
| `BufferSize` | `256` | Audio buffer size in frames (a power of two) |
| `GateThresholdDbfs` | `-50` | Noise gate threshold in dBFS (at most 0) |
| `LatencyTargetMs` | `50` | Latency target in milliseconds |

The menu's choices are saved to `~/Library/Application Support/solid-backup/usersettings.json`, which only holds the settings that differ from the defaults. A missing or invalid user settings file is ignored with a message, and the defaults are used.
