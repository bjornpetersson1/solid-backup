# E3-1 · Test corpus

- **Issue:** [#8](https://github.com/bjornpetersson1/solid-backup/issues/8)
- **Labels:** `E3 Analysis`
- **Phase:** 3. Analysis
- **Depends on:** E2-2 (#6)
- **Status:** Approved

## Summary
The corpus that every pitch, level and onset target in Measurement is measured on: deterministic generated tones built in code, short recordings of the owner's voice and whistling plus silence and breath noise, a reference file per recording, and a scorer that turns a detector's output into the Measurement figures. E3-2 and E3-3 plug their detectors into it instead of each building their own measurement.

## Goals
- A corpus that is small enough for git, fully documented and reproducible.
- One shared definition of the measures (pitch accuracy, octave errors, false notes, onset accuracy) so E3-2 and E3-3 are judged by the same rules.
- Reference files whose note sequence is checked automatically against the recording script, so labelling mistakes are caught by a test.

## Non-goals
- The pitch detector (E3-2) and the level, gate and onset detectors (E3-3). Here the scorer is only tested with a fake "oracle" detector and with constructed errors.
- Meeting the targets; that is E3-2's and E3-3's job.
- Recording from inside the app (E5-3). The owner records with any recorder (e.g. QuickTime Player or Voice Memos) and exports WAV.
- A measurement command in the app's menu or a dashboard. The scorer is a library used by tests (and later by E6).
- Corpus material for key detection (E4-3) or rhythm (E5); later issues add their own clips.
- Resampling: recordings must already be 48 kHz.
- Git LFS or any external storage.

## Acceptance Criteria
1. **Generated tones are built in code, deterministically.**
   - Generators produce, at 48 kHz: sine and sawtooth notes on every semitone from E2 to C6; the same notes with added white noise at a fixed signal-to-noise ratio; logarithmic glides across E2–C6; and unvoiced material (digital silence and low-level noise).
   - Each generated clip comes with its reference (segments, notes in cents, onsets) built from the same parameters, in the same model the recordings use.
   - Noise uses a fixed seed: generating a clip twice gives identical samples (a test checks this).
   - No generated audio is committed to git. A tool command writes the generated clips to WAV in a chosen directory so the owner can listen to them.
2. **Recordings of voice and whistling, plus silence and breath noise, are in the repo.**
   - `corpus/recordings/` holds the owner's recordings as 16-bit PCM WAV, 48 kHz, mono, readable by `WavReader` from E2-2: sung notes (separated notes and a short melody), whistled notes and a whistled melody kept within about G4–C6, room silence, and breath noise.
   - The clips follow a recording script, `corpus/script.md` (instructions) and a machine-readable `corpus/script.csv` (clip name, kind, expected note sequence).
   - The implementation stops once, after everything else in this spec is built, and asks the owner to record the clips into `corpus/recordings/`; it then continues on its own.
3. **Every recording has a reference file.**
   - `corpus/recordings/<clip>.ref.csv` holds, in time order and covering the whole clip without overlap: segments with start and end in milliseconds, a kind (`note`, `silence`, `breath`, `noise`, `out-of-range`), and for `note` the pitch in MIDI cents (the measured pitch, not only the nominal note); and onset rows with their time in milliseconds.
   - The reference files are drafted by a labelling tool command that measures each segment's pitch offline, searching only within a semitone of the note the script expects (so octave mistakes in the label are impossible), and places segment boundaries and onsets from the level envelope.
   - A test checks every recording against its reference and script: the file exists, the format is 48 kHz mono 16-bit, segments are ordered, contiguous and cover the clip, the note sequence equals the script's, every `note` pitch is within ±50 cents of the scripted note, and there is one onset per scripted articulated note.
   - The owner listening through the recordings against their labels is listed in the PR as an unchecked manual check.
4. **A scorer computes the Measurement figures on the corpus.**
   - It loads generated and recorded clips with their references and runs a detector over them buffer by buffer, through `IAudioInput` (`WavFileInput` for recordings), without audio hardware.
   - A detector reports, per buffer, either no note or a pitch in MIDI cents, and optionally an onset, each with the time it refers to.
   - It reports per clip and in total: pitch accuracy (share of voiced windows with a detected pitch within ±50 cents of the reference; a window with no detected note counts as wrong), octave errors (share of voiced windows off by 1150 cents or more), false notes (share of unvoiced windows — `silence`, `breath`, `noise` — with a detected note), and onset accuracy (share of reference onsets matched one-to-one by a detected onset within ±20 ms), with the window and onset counts behind each figure.
   - Windows within 30 ms of a segment boundary and `out-of-range` segments are left out of the pitch measures; the targets from Measurement are defined once in code and the report says whether each is met.
   - Tests prove it: an oracle detector that echoes the reference scores 100 % accuracy, 0 % octave errors, 0 % false notes and 100 % onsets; constructed errors (some windows an octave off, notes in silence, onsets 25 ms late) give exactly the expected figures.
5. **The corpus is small enough for git and documented.**
   - Everything under `corpus/` is at most 10 MB (about 100 s of recordings at 48 kHz mono 16-bit); a test enforces the limit.
   - `corpus/README.md` documents: what is in the corpus and why, the WAV format, the reference file format, how to record (pointing to the script), how to label and check a new clip, how to write the generated tones to WAV, and the scoring rules (windows, boundary exclusion, octave and onset tolerance, out-of-range).
   - The repo README links to `corpus/README.md`.
6. **The canonical check passes.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root, on macOS and in CI on Ubuntu, with no network access and no audio hardware.

## Tasks
- [ ] Define the reference model (segments, kinds, pitch in MIDI cents, onsets) and the `.ref.csv` reader and writer.
- [ ] Write the tone generators and their references.
- [ ] Write the recording script (`corpus/script.md`, `corpus/script.csv`).
- [ ] Write the tool commands: write the generated tones to WAV; draft a reference file for a recording.
- [ ] Write the scorer, its report and the targets, with oracle and constructed-error tests.
- [ ] Stop and ask the owner to record the clips; label them with the tool; commit them.
- [ ] Tests for every recording against its reference and the script, and for the corpus size limit.
- [ ] Write `corpus/README.md` and link it from the repo README.

## Decisions
- **The owner records, the tool labels** (owner's choice): the owner records short clips following a script; a labelling tool drafts the reference files. Answers the issue's open question.
- **Labels are checked against the script, plus a manual listen** (owner's choice): the script fixes the notes, so a test can verify the label's note sequence and that no label is off by more than ±50 cents from the scripted note. The tool's offline pitch search is restricted to ±1 semitone around the expected note, so it cannot make octave errors and is not the E3-2 detector, which keeps the reference independent of what it measures.
- **The implementation pauses once for recording** (owner's choice): after everything else is built, so the owner records against the finished script. This is the one extra stop in the epic.
- **Whistling stays within E2–C6** (owner's choice): the script keeps whistling at about G4–C6. Anything sung or whistled outside the range is labelled `out-of-range` and left out of the measures rather than counted.
- **Generated tones in code, not in git** (owner's choice): deterministic generators with a fixed seed cost 0 bytes and can cover every semitone; a tool command writes them to WAV for listening.
- **At most 10 MB, about 100 s of recordings** (owner's choice; the 2 minutes in the question do not fit 10 MB at 48 kHz mono 16-bit, so the size limit wins).
- **Reference files as CSV with note segments** (owner's choice): easy to read and diff; windows are derived when scoring, so the reference does not depend on the buffer size.
- **The scorer is part of E3-1** (owner's choice): E3-2 and E3-3 are measured by the same rules.
- **Reference pitch is the measured pitch of the segment** (default): a singer slightly off the nominal note should not count against the detector. The script asks for steady notes without vibrato.
- **Recordings at 48 kHz, 16-bit, mono** (default): the default `SampleRate`, and `WavReader` does not resample.
- **Corpus at `corpus/` in the repo root** (default): it is data shared by tests and, later, the app's measurement flow, not test code. The scorer and the generators live in the app project so E6 can reuse them; the tool commands live in a small separate console project under `tools/` so the app's command line is unchanged.
- **Scoring rules** (defaults, documented in `corpus/README.md`): a window is one detector report; voiced and unvoiced windows are judged by the reference segment at the time the detector says its report refers to; windows within 30 ms of a segment boundary are excluded from the pitch measures because a buffer there mixes two states; "an octave or more off" is 1150 cents or more (an octave within the ±50 cents tolerance); onsets match one-to-one within ±20 ms. Extra detected onsets are reported but have no target.
- **Generated noise SNR, glide length and note length** (implementation): chosen in the implementation and documented in `corpus/README.md`.

## Open Questions
- None
