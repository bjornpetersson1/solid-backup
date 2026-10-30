# E2-1 · PortAudio spike on macOS

- **Issue:** [#5](https://github.com/bjornpetersson1/solid-backup/issues/5)
- **Labels:** `E2 Audio`
- **Phase:** 2. Audio
- **Depends on:** E1-1 (#1)
- **Status:** Approved

## Summary
Verify that PortAudio works with low latency from .NET on macOS through `PortAudioSharp2`, with a small throwaway console project under `spikes/`, and record the findings in the README and the constitution so E2-2 can build on facts instead of assumptions.

## Goals
- Prove that `PortAudioSharp2` loads its bundled native library on this Mac (Intel, `osx-x64`) without Homebrew.
- Measure device listing, opening input and output streams, the smallest stable buffer size, and the latencies PortAudio reports.
- Describe how the microphone permission behaves in Terminal and iTerm.
- Update the constitution's Platform, Latency and Stack assumptions.

## Non-goals
- Production audio code, `IAudioInput` / `IAudioOutput`, WAV input or in-memory output (E2-2).
- Device choice by name, permission detection in the app, speaker warning (E2-3).
- Adding PortAudio to the `SolidBackup` app or test projects.
- Measuring the end-to-end acoustic latency with a loopback cable; only PortAudio's reported latencies are measured, as in Measurement.
- Running the spike on Apple Silicon (a manual check for the owner, see criterion 4).

## Acceptance Criteria
1. **A reproducible spike exists outside the solution.**
   - `spikes/PortAudioSpike/PortAudioSpike.csproj` is a .NET 8 console project that references `PortAudioSharp2` and is **not** part of `SolidBackup.sln`.
   - `dotnet run --project spikes/PortAudioSpike -- <mode>` supports at least: `devices` (list every device with host API, channel counts, default sample rate and default low/high latencies, marking the defaults), `latency` (open input, output and duplex streams at 48 kHz for each buffer size 32, 64, 128, 256, 512 and print the input and output latency PortAudio reports), and `stability` (run a duplex pass-through stream for a given buffer size and duration and count input overflows and output underflows from the callback status flags).
   - The callback in `stability` does not allocate, lock or do I/O; counts are kept in fields and printed after the stream stops.
   - The canonical check `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` still passes from the repo root, and CI is unchanged.
2. **The findings are documented in the README.**
   - A section "Audio on macOS (E2-1 findings)" states: the binding and version; the native library it loaded and for which runtime; the machine, macOS version and default devices it was measured on; a table of buffer size → reported input latency, output latency and their sum at 48 kHz; the smallest buffer size with 0 overflows and 0 underflows over 60 s of duplex pass-through; and the recommended default `BufferSize`.
   - It describes the microphone permission behaviour: what happens on first use, what the app receives when access is denied (silence or an error), and where the owner grants it (System Settings → Privacy & Security → Microphone), for Terminal and for iTerm.
   - It states plainly what was not verified on this machine (Apple Silicon).
3. **The assumptions in the constitution are updated.**
   - In `Constitution/Requirements-and-goal.md`, the Platform, Latency and Stack rows of "Open points and assumptions" reflect the findings: platform becomes macOS on Intel and Apple Silicon, with Apple Silicon marked as a manual check; the measured buffer size and reported latencies replace "Buffer sizes verified in E2-1"; the binding `PortAudioSharp2` replaces "Binding chosen in E2-1".
   - If the findings make `BufferSize` 256 a poor default, `appsettings.json` and the README settings table are changed to the recommended value; otherwise they stay as they are.
4. **Manual checks for the owner are listed.**
   - The PR lists, as unchecked manual checks: running `devices` and `stability` in Terminal and in iTerm and confirming the permission behaviour described in the README; and running `devices` and `latency` on an Apple Silicon Mac when one is available.

## Tasks
- [ ] Create `spikes/PortAudioSpike` with `PortAudioSharp2`, not added to the solution.
- [ ] Implement the `devices`, `latency` and `stability` modes.
- [ ] Run them on this Mac and collect the numbers.
- [ ] Observe the microphone permission in the terminal used for the run.
- [ ] Write the README section.
- [ ] Update the constitution's Platform, Latency and Stack rows (and `BufferSize` if needed).
- [ ] List the manual checks for the owner.

## Decisions
- **Platform: measure on this Intel Mac now, Apple Silicon as a manual check.** The available machine is an Intel Core i5 (`osx-x64`), while the issue says Apple Silicon. The constitution becomes "macOS (Intel and Apple Silicon)". (Owner's choice; departs from the issue.)
- **Binding: `PortAudioSharp2`.** It ships the native PortAudio library for `osx-x64`, `osx-arm64` and `linux-x64`, so no Homebrew step and the Ubuntu CI keeps building once E2-2 adds it to the app. If it does not work, the spike falls back to a thin own P/Invoke layer and the README says why. (Owner's choice.)
- **Spike code in `spikes/PortAudioSpike`, committed but outside the solution.** The numbers can be reproduced, and the app and CI are untouched until E2-2. (Owner's choice.)
- **Microphone permission: observed where possible, confirmed by the owner.** The macOS dialog needs a person; the Terminal and iTerm behaviour is a manual check. (Owner's choice.)
- **48 kHz, mono input, stereo output** (default): CoreAudio's usual native rate for built-in devices; mono matches the monophonic input.
- **Buffer sizes 32–512 in powers of two** (default): matches the `BufferSize` setting, which must be a power of two.
- **"Stable" means 0 overflows and 0 underflows over 60 s of duplex pass-through** (default): a short, repeatable stand-in for the one-hour target in Measurement, which is checked in E6-1.

## Open Questions
- None
