# E8-2 · Crackle in the synth in recordings

- **Issue:** [#82](https://github.com/bjornpetersson1/solid-backup/issues/82)
- **Labels:** `E8 Playtest 2`
- **Phase:** 8. Playtest 2
- **Depends on:** E8-1 (#81) for the crossfaded drift correction; builds on E5-1 (#14), E5-3 (#16), E6-1 (#19)
- **Status:** Approved

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
- [ ] A recorder test with irregular callback orders that compares the recorded chord channel with what the output received, and shows the hard skips.
- [ ] A synth test of steps at chord changes, strikes, starts and stops, for every sound and pattern.
- [ ] Replace the recorder's hard skip with E8-1's crossfaded drop or repeat, shared rather than copied. Make the drift measure robust to bursts, so ordinary irregular callbacks cause no correction.
- [ ] An overrun no longer cuts in hard silence: it fades at the edges, or the remedy is chosen from what the test shows.
- [ ] Fix any synth discontinuity the test in 3 finds.

## Decisions
- **Only in the file** (owner): live sounds clean, so the recording path is the first suspect. The synth test in 3 still runs, to rule out clicks masked live.
- **At chord changes and strikes** (owner): this fits drift corrections triggered by input callbacks bunched after onsets. The tests reproduce that order.
- **Crossfaded, as in E8-1** (owner, recommended): one shared crossfaded drop or repeat for the monitor and the recorder, in both channels. No hard edges anywhere.
- **The step bounds** (default): 1.5 × a reference step, the same rule as in E8-1. For the synth, the reference is the chord's own attack from silence, so a change is never sharper than a fresh strike.
- **Builds on E8-1:** the crossfade component comes from E8-1. This issue makes the recorder use it.

## Open Questions
- None.
