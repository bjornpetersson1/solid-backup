# Test corpus

The pitch, level and onset targets in Measurement (`Constitution/Requirements-and-goal.md`) are measured on this corpus. It has two halves:

- **Generated tones**, built in code by `GeneratedCorpus` (`src/SolidBackup/Measurement`), not stored in git.
- **Recordings** of the owner's voice, whistling, room silence and breathing, in `recordings/`, each with a reference file.

Detectors are measured with `CorpusScorer`, which runs a detector over every clip buffer by buffer through `IAudioInput`, without audio hardware, and scores its reports against the references.

## Generated tones

All built at the requested sample rate (48 kHz by default; 44.1 kHz works too) with fixed seeds, so they are identical on every run and machine:

| Clip | Content |
| --- | --- |
| `generated-sine-scale` | a sine on every semitone from E2 to C7, 300 ms each with 100 ms of silence between |
| `generated-sawtooth-scale` | the same with a band-limited sawtooth |
| `generated-sine-noise-scale` | the sine scale with white noise 20 dB below the sine's RMS over the whole clip (the gaps are `noise`) |
| `generated-sawtooth-noise-scale` | the sawtooth scale with the same noise |
| `generated-glides` | a logarithmic sine glide from E2 to C7 and back, 4 s each way |
| `generated-unvoiced` | 1 s digital silence, 1 s white noise at −40 dBFS, 1 s silence, 1 s noise at −60 dBFS |

Notes have 5 ms fades and an amplitude of 0.5. Every note start is an onset.

For key detection (E4-3) there are also melodies, built by `GeneratedCorpus.Melodies` from sines in the same way. They are not part of the pitch, level and onset measures; their reference files add a `key` line per stretch. Each phrase is played twice, at 450 ms a beat, with 50 ms of silence after every note, and the tonic lies between F#3 and F4:

| Clip | Key | Phrase |
| --- | --- | --- |
| `generated-melody-c-major` | C major | starts on the third |
| `generated-melody-g-major` | G major | starts on the fifth below the tonic |
| `generated-melody-eb-major` | Eb major | starts on the tonic, with a chromatic passing note (the raised fourth) |
| `generated-melody-f-sharp-major` | F# major | starts on the fifth |
| `generated-melody-a-minor` | A minor | starts on the tonic |
| `generated-melody-e-minor` | E minor | starts on the fifth |
| `generated-melody-c-minor` | C minor | starts on the third |
| `generated-melody-c-to-g-major` | C major, then G major | the C major phrase, then the G major one |
| `generated-melody-a-to-d-minor` | A minor, then D minor | the A minor phrase, then the E minor one moved to D |

Every phrase uses the scale's fourth and seventh degrees, and the minor ones the raised seventh, so a melody is not also a melody in a neighbouring key. The key estimator's margin (0.12 in correlation), its decay (0.7 × `KeyChangeSeconds`) and its hold time (0.15 × `KeyChangeSeconds`) were chosen as the values for which every melody gives its key, and both modulations the new key in time, at `KeyChangeSeconds` 4 and 8; the window of values that does so is narrow, so live singing may behave worse.

To listen to them, write them as WAV with their reference files:

```bash
dotnet run --project tools/SolidBackup.CorpusTool -- tones <folder> [44100]
```

## Recordings

| Clip | Kind | Notes | Length |
| --- | --- | --- | --- |
| `singing` | voice | 19 separated sung notes, F3–C5 (one around A#2) | 32 s |
| `whistle` | whistle | 14 separated whistled notes, A#5–C7 | 25.5 s |
| `silence` | silence | the room with nobody playing | 12.5 s |
| `breathing` | breath | breathing near the microphone | 14 s |

All are mono, 48 kHz, 16-bit PCM WAV. They are free recordings: the notes were not set in advance. `script.csv` lists, per clip, its kind and the notes in the order they were sung or whistled; `script.md` is the guide for recording more.

### The reference files

Each recording has `recordings/<clip>.ref.csv`. Lines starting with `#` are comments. The other lines are:

- `segment,<start ms>,<end ms>,<kind>[,<cents>,…]`: the segments are in time order and cover the whole clip without gaps or overlap. The kind is `note`, `silence`, `breath`, `noise`, `out-of-range` or `unclear`. A `note` or `out-of-range` segment has one or more pitch values in MIDI cents (A4 = 6900), spaced evenly from start to end and joined linearly: one value is a steady note, two a glide, and a recorded note has its measured pitch curve with one value every 10 ms.
- `onset,<ms>`: one per note in the range, where its level rise begins.
- `key,<start ms>,<tonic>,<mode>`: only in a generated melody's reference, the key from that time on, e.g. `key,0,F#,Ionian`.

The generated clips use the same model (`tones` writes the same format).

### Recording and labelling a new clip

1. Record following `script.md` and put the WAV file in `recordings/`.
2. Let the tool propose the note list, and check it by ear:
   ```bash
   dotnet run --project tools/SolidBackup.CorpusTool -- suggest <clip>
   ```
3. Add the clip to `script.csv`: `<clip>,<voice|whistle|silence|breath>,<notes>`.
4. Label it, which writes `recordings/<clip>.ref.csv` and prints how far each note's median lies from the listed note:
   ```bash
   dotnet run --project tools/SolidBackup.CorpusTool -- label <clip>
   ```
5. Run `dotnet test`. The corpus tests check that each recording is 48 kHz mono 16-bit, that its reference covers it, that its notes match the list with every median within ±50 cents of the listed note, that there is one onset per note in the range, and that the corpus stays at or below 10 MB.

**How the labelling works.** Notes are the loud stretches of the level envelope (RMS over 10 ms) that are strongly periodic; stretches closer than 75 ms are joined, and loud stretches without a pitch (a breath, a click) are left to the gaps. Each note's pitch is measured every 10 ms by normalised autocorrelation, searching only within one semitone of the listed note, so a label cannot be an octave off. The rest of the note's sound becomes `unclear`: the parts of the loud stretch before its first and after its last periodic frame (an attack, a decay), and the quiet lead-in and tail around the stretch for as long as they stay periodic within a semitone of the note. The gaps between notes are `silence`, or `breath` when their median level is more than 10 dB above the clip's floor. A note's onset is where its level first comes within 20 dB of its peak (and above the midpoint between the floor and the peak).

**Caveat.** The note lists for the current recordings were proposed by `suggest` and not set in advance, so the reference rests on that analysis plus the owner's listening check, not on an independent score. `suggest` searches the whole range and takes the shortest strongly periodic lag; a separate spectral check of the whistled notes agreed with it.

## Measuring the analysis

```bash
dotnet run --project tools/SolidBackup.CorpusTool -c Release -- report
```

runs the analysis components with the default settings from `appsettings.json` over the generated tones and the recordings: the pitch detector (E3-2), and the level meter, noise gate and onset detector (E3-3). It prints a markdown report with two tables, per clip and in total. The first table has pitch accuracy, octave errors and false notes. The second has the notes in silence and breath while the gate is open, onset accuracy, and extra onsets with how many of them fall in silence or breath. Each figure comes with its counts. The report then says whether each target is met and gives the average analysis time per buffer. Run it in Release; a Debug build analyses about four times slower. The clips run one at a time so the time per buffer is not inflated by other threads. The detector's constants are in `src/SolidBackup/Analysis`: YIN's dip threshold is 1 − `PitchConfidenceThreshold` (0.2 by default), the energy floor −80 dBFS, and a change of note is reported once it has held for 10 ms and two estimates (`NoteHold`). The level is the RMS over the last 10 ms; an onset is a rise of `OnsetThresholdDb` within 20 ms (`OnsetDetector.RiseMs`).

## Scoring rules

`CorpusScorer` and `MeasurementTargets` define them once for every detector:

- A **window** is one detector report (one per buffer), placed at the time the detector says its estimate refers to.
- **Voiced** windows lie in `note` segments, **unvoiced** windows in `silence`, `breath` or `noise`. `out-of-range` and `unclear` segments are left out of every measure, and out-of-range notes have no onset.
- A voiced window whose reference pitch at that moment lies nearer a note outside the range (a note drifting past C7, say) is left out too.
- Windows within **30 ms** of a segment boundary (including the clip's start and end) are left out of the pitch measures, because a buffer there mixes two states.
- **Pitch accuracy**: the share of voiced windows whose detected pitch is within **±50 cents** of the reference at that time; a window with no detected note counts as wrong. Target ≥ 95 %.
- **Octave errors**: the share of voiced windows off by **1150 cents or more** (an octave, within the tolerance). Target ≤ 1 %.
- **False notes**: the share of unvoiced windows with a detected note. Target ≤ 2 %. The windows in `silence` and `breath` alone are also counted separately (`SilenceOrBreathWindows`); see the next rule for the notes counted there.
- **Notes in silence and breath**: the windows in `silence` and `breath` where the detector gives a note while its noise gate is open (`DetectorReport.GateOpen`). Target: none (E3-3). The other pitch measures ignore the gate. As the other side of that trade-off, the report also counts the correct notes the gate cuts off (`NotesCutByGate`: voiced windows with the right note while the gate is closed), without a target.
- **Onset accuracy**: the share of reference onsets matched one to one by a detected onset within **±20 ms**. Target ≥ 90 %. Detected onsets left unmatched are reported as extra onsets, and those falling in silence or breath are counted apart, without a target.

The range is the default E2–C7 from `appsettings.json`.
