# Recordings and their note lists

The recordings in `recordings/` are free recordings: sing or whistle whatever you like, then write down which notes you sang in `script.csv`. That list is not something the app knows or follows; the app detects any pitch that comes in. The list only lets the labelling tool measure the exact pitch of each note without being able to mistake its octave, so the reference the detectors are measured against can be trusted.

## Setup

- Record in a quiet room with the microphone you play with (the built-in microphone is fine), at a normal playing distance of about 30–50 cm.
- Record **mono, 48 kHz, 16-bit PCM WAV**. QuickTime Player (File → New Audio Recording) and Voice Memos record AAC/M4A; convert with `afconvert -f WAVE -d LEI16@48000 -c 1 in.m4a recordings/<clip>.wav`.
- Name each file after its clip (lower case, no spaces, e.g. `voice-melody.wav`) and put it in `recordings/`.
- Start and end each clip with about half a second of silence.
- Keep the whole set at about 75 s; the corpus may hold at most 10 MB (about 100 s).

## What to record

At least one clip of each kind:

- **voice**: sung notes, e.g. a scale, a repeated note and a short melody, across your range;
- **whistle**: whistled notes and a melody, within the detection range (up to C7 by default);
- **silence**: the room with nobody playing (about 8 s);
- **breath**: normal breathing near the microphone with a few deeper breaths (about 10 s).

For sung and whistled clips:

- One steady note at a time, **without vibrato**.
- **Separate every note** with a short pause (about 0.2 s) and start each one cleanly, e.g. on "da" or "ta". Every note counts as one onset, so a repeated note must be clearly re-started.
- About 0.5–1 s per note.

## Listing the notes

Add one line per clip to `script.csv`: `<clip>,<kind>,<notes>`, with the notes in the order you sang them as note names (`C3`, `F#4`, `Bb3`), e.g. `voice-melody,voice,G3 G3 D4 D4 E4 E4 D4`. Silence and breath clips have no notes: `silence,silence,`.

The name of the nearest note is enough; the tool measures how many cents off it was. A note more than 50 cents from the note you listed fails the corpus test: check the list, or record that clip again.

Then label the clips and run the tests (see `README.md`).
