# E8-2 · Crackle in the synth in recordings

- **Issue:** [#82](https://github.com/bjornpetersson1/solid-backup/issues/82)
- **Labels:** `E8 Playtest 2`
- **Phase:** 8. Playtest 2
- **Depends on:** E8-1 (#81) for the crossfaded drift correction; builds on E5-1 (#14), E5-3 (#16), E6-1 (#19)
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 1165 tests passed); a recording of a few minutes of play sounding clean in another program is a manual check for the owner

## Summary
The chords in a recorded WAV file crackle at chord changes and strikes, but live they sound clean (owner). So the fault is in how the recording keeps its two channels in step, or in something only the file shows. This issue finds the cause and fixes it, so that the chord channel of a recording sounds as it did live.

## Likely cause (to be confirmed by the first test)
`SessionRecorder` keeps the input and output channels in step by dropping the lead of the side that runs ahead. It drops it with a hard skip (`Ring.Skip`), with no crossfade, so every skip in the output channel is a click.

The lead is measured over 0.5 s windows against a threshold of one buffer (64 samples). Irregular callbacks can push the lead past that threshold. An onset makes the analysis on the input's thread heavier, which delays and bunches the input's callbacks, and an onset is also when a chord is struck. That fits clicks "at chord changes and strikes" heard only in the file.

The other suspects are:
- a dropped buffer replaced by hard silence (an overrun);
- clicks in the synthesizer's voices that are masked live.

The tests below tell these apart.

## Goals
- The recording's chord channel is continuous: it is what the output played, apart from smooth drift corrections.
- Both channels stay in step without hard edges, using E8-1's crossfaded correction.

## Non-goals
- The monitored input (E8-1), new sounds, a different file format or bit depth.
- Changing how the channels are laid out (input left, chords right) or the recordings folder.
- Playback in the app (E8-3).

## Acceptance Criteria
1. **The cause is identified and described in the spec/PR, including whether it is also heard live.**
   - The PR names the cause, backed by the tests in 2 and 3. A test that reproduces it fails before the fix.
   - Whether the synth is affected live comes from criterion 3. The owner reports it is not.
2. **A recording of a session driven by generated tones gives an output channel identical (within 16-bit rounding) to what the in-memory `IAudioOutput` received: no dropped, repeated or zeroed buffers.**
   - Live-chain test with a recorder: a melody of generated tones with onsets, and callbacks in irregular orders (bursts of up to 4 input buffers, input callbacks delayed and bunched after onsets). Without clock drift, the recorded chord channel equals the output sample for sample, within 16-bit rounding.
   - With ±0.1 % clock drift between the two sides, the channels stay in step (the drift stays within E6-1's bound). Every correction is crossfaded: in the recorded chord channel no sample-to-sample step exceeds the largest step in what the output played over the same stretch, × 1.5.
3. **Chord changes, note starts and stops give no discontinuities beyond what the envelope allows.**
   - For every sound and every rhythm pattern, a test renders a melody with chord changes, strikes, and starts and stops of the chord.
   - At each event, the largest sample-to-sample step is at most the largest step of the same chord struck alone from silence, × 1.5.
   - If this already passes before the fix, the synth is clean, as the owner hears it live, and the PR says so.
4. **Recording still causes no dropouts, and 0 bytes are allocated per callback.**
   - The live chain's allocation test passes with a recorder.
   - An overrun still keeps the channels in step.
   - E5-3 and E6-1's recording tests pass. A file still plays in another program (header test).
   - `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` passes.
5. **Manual check (owner):** a recording of a few minutes of play, with chord changes, sounds clean in another program.

## Tasks
- [x] A recorder test with irregular callback orders that compares the recorded chord channel with what the output received, and shows the hard skips.
- [x] A synth test of steps at chord changes, strikes, starts and stops, for every sound and pattern.
- [x] Replace the recorder's hard skip with E8-1's crossfaded drop or repeat, shared rather than copied. Make the drift measure robust to bursts, so ordinary irregular callbacks cause no correction.
- [x] An overrun no longer cuts in hard silence: it fades at the edges, or the remedy is chosen from what the test shows (kept as silence; see Decisions).
- [x] Fix any synth discontinuity the test in 3 finds.

## Decisions
- **Only in the file** (owner): live sounds clean, so the recording path is the first suspect. The synth test in 3 still runs, to rule out clicks masked live.
- **At chord changes and strikes** (owner): this fits drift corrections triggered by input callbacks bunched after onsets. The tests reproduce that order.
- **Crossfaded, as in E8-1** (owner, recommended): one shared crossfaded drop or repeat for the monitor and the recorder, in both channels. No hard edges anywhere.
- **The step bounds** (default): 1.5 × a reference step, the same rule as in E8-1. For the synth, the reference is the chord's own attack from silence, so a change is never sharper than a fresh strike.
- **Builds on E8-1:** the crossfade component comes from E8-1. This issue makes the recorder use it.

- **The cause, confirmed** (implementation): `RecordedChordsTests` drives the live chain and a recorder whose writer runs every 50 ms, as on its thread. Input callbacks arrive in bursts of up to 4 and on a clock 0.1 % slower than the output's. The old recorder then dropped the output channel's lead with a hard skip: a step of 0.18 in the recorded chords, against a bound of 0.09. Each such skip is a click in the chords, and live they are never heard, which is why the crackle was only in the file. Without drift the chords were already recorded exactly, and the bursts alone caused no skip.
- **Not heard live** (implementation): `ChordEventStepTests` renders, for every sound and pattern, a melody with chord changes, strikes, and stops and starts of the chord. Its largest step stays within 1.5 × that of a chord struck alone from silence, and it passed before any change. The synthesizer does not click, as the owner hears live.
- **The fix** (implementation): each side of `SessionRecorder` is now an `Audio.SplicingRing` (E8-1's ring) behind the recorder's own drop-and-owe-silence rule for a full ring. The writer reads it sample by sample and drops a lead with `SplicingRing.Splice`, a 2 ms crossfade. Each side's least lead over the 0.5 s window is an `Audio.FillWindow`.
- **A tolerance of 10 ms before a drop** (implementation): a lead is dropped only once it has stayed beyond max(one buffer, 10 ms) for a whole window, not one buffer. Callbacks that come in bursts of several buffers therefore never cause a correction, and only a faster clock does. The drop still takes the lead down to one buffer, so the channels stay within about 10 ms of each other. That is within E6-1's bound: its test of a 1 % faster input still passes. With 0.1 % drift there is a drop roughly every 10 s.
- **Overruns keep their silence** (implementation): a buffer dropped because the disk fell 2 s behind is still replaced by silence without a fade. It only happens after a long disk stall, which the app counts and reports when it stops, and the tests show it is not the crackle. Fading its edges would mean marking where silence starts in the ring, which this issue does not need.
- **Clean code:** owed silence is written with `SplicingRing.WriteSilence` (at most two `Clear` calls), and the ring's room has one formula (`SplicingRing.Room`). The recorded channel in the tests is read with `TestWav.Pcm16Channel`. The drift tests run 24 s of audio, since 0.1 % drift needs about 10 s to pass the tolerance; a larger drift would be faster but not what the criterion names.
- **Code review fixes:**
  - A new drop waits until the last one's crossfade has played, so two never overlap into a jump.
  - `Drain()` throws when the recorder has its own writer thread or has stopped.
  - A drop still crossfading keeps the samples it skips from the producer (`SplicingRing.Publish`).
  - The stop message counts the drift corrections (`kanaljusteringar`, so as not to be confused with pass-through's own `driftkorrigeringar`), so the manual check can tell how many drops a recording had.
  - The README states the tolerance as max(10 ms, one buffer).
  - The tests now check the criteria as written:
    - the input callbacks come bunched after every onset (`CallbackSchedule`'s `burstsFrom`);
    - with drift, the recorded input and chords stay within E6-1's 12 buffers of each other (each half second, found again in the noisy voice and in what was played);
    - each 10 ms of recorded chords steps no further than 1.5 × what the output played over the same stretch;
    - the synth test checks each chord event against that chord struck alone.
  - Made the recorder's drops hard again for a moment: the chord test then fails (a step of 0.061 against 0.039).
- **Test seam** (implementation): `SessionRecorder` takes `background: false` and has `Drain()`, so a test decides when the writer runs. `DriftCorrections` counts the crossfaded drops.

## Open Questions
- None.
