# E6-3 · Live dashboard

- **Issue:** [#21](https://github.com/bjornpetersson1/solid-backup/issues/21)
- **Labels:** `E6 Live mode`
- **Phase:** 6. Live mode
- **Depends on:** E6-1 (#19)
- **Status:** Approved

## Summary
The live view while playing: one Swedish panel, redrawn about five times a second from E6-1's lock-free snapshot, showing the note and cents, a level meter, the chord, the key, the style, sound and rhythm, the level features that are on, the measured latency and a dropout counter. It replaces E6-1's status line in live mode, from `--live` and from the menu (E6-2).

## Goals
- The owner sees at a glance what the app hears and plays, and whether the latency and dropout targets hold.
- The dashboard can never disturb the audio.

## Non-goals
- Keys in live mode other than leaving it (`q`, `Esc`, `Ctrl+C`, as in E6-1 and E6-2).
- History, graphs, a chord timeline or a tuner needle.
- Colour themes or a layout choice; a narrower terminal than 120 characters is supported only on a best-effort basis.
- Anything the snapshot does not already carry, except what this spec adds to it below.

## Acceptance Criteria
1. **The dashboard shows the live state.**
   - One bordered panel titled `solid-backup · live`, with one labelled row each, in Swedish:
     - **Ton:** the note and cents in E3-2's format (`A4 +12 cent`), or `–` with no note.
     - **Ackord** and **Tonart:** the current chord symbol (or `–`) and the current key as the harmony reports it, e.g. a moved Blues key, marked `(auto)` when the key is estimated, `–` for Free or before the first key.
     - **Nivå:** a meter from −60 to 0 dBFS with the gate threshold marked, the level in dBFS, and whether the gate is open (when the gate feature is on).
     - **Stil:** the style, the sound, the pattern, and BPM and time signature.
     - **Funktioner:** each level feature with on or off.
     - **Latens:** the total in ms and its parts (input, chain, hand-over, output), with a mark for within or over `LatencyTargetMs`; `?` for the chain part when E6-1 could not measure it.
     - **Avbrott:** the dropout count (E6-1's definition), highlighted when above 0, and, with recording on, the recording's length and overrun count.
   - The footer says `q/Esc: tillbaka till menyn` (from the menu) or `q/Esc: avsluta` (from `--live`).
   - With the output on the built-in speakers, the speaker warning (E2-3) is shown in the panel.
   - Tests render the dashboard from fixed snapshots with Spectre's test console and assert each row's text, including the no-note, no-key, over-target, unknown-chain-delay, dropouts-above-0 and speaker cases.
2. **The dashboard is readable at 120 characters width.**
   - Rendered at a console width of 120, no line is wider than 120 columns, no row wraps, and no value is cut off, for the widest values: the longest chord symbol, key, style, sound and rhythm, five-digit latencies and a seven-digit dropout count (a test).
   - Rendered at 80 columns it still shows every row without wrapping (the meter shrinks; a test).
   - Readability on the owner's terminal is a manual check (`docs/manual-testing.md` E6 step 4).
3. **Updating it causes no dropouts.**
   - The dashboard runs on its own thread (the console thread), reads E6-1's snapshot without locking and never writes to anything the audio callbacks read. The audio side's code is unchanged in what it does per buffer, and E6-1's test of 0 bytes allocated per callback still passes with the dashboard running on another thread.
   - It redraws about five times a second (every 200 ms) in place, without scrolling; a test with a fake clock sees one redraw per interval.
   - The dashboard stops before the audio does, so stopping never waits on the console while the audio runs.
   - An hour of play with the dashboard on and the dropout count at 0 is part of E6-1's manual check.

## Tasks
- [ ] The dashboard view, built from a snapshot (a pure function from snapshot and width to the rendered panel).
- [ ] Redrawing in place about five times a second on the console thread, replacing E6-1's status line in live mode.
- [ ] Add to the snapshot what the rows need and E6-1 did not already carry (style, sound, BPM and time signature, gate threshold, speaker warning, recording length), set once at start where they do not change.
- [ ] Tests for the content, the widths and the redraw interval.
- [ ] README and `docs/manual-testing.md` E6 step 4.

## Decisions
- **One panel of rows** (owner's choice): simple to read, and fits narrower windows than a grid of panels.
- **About five redraws a second** (default): "a few times per second" from the issue; fast enough to follow a melody, slow enough to cost little.
- **Builds on E6-1:** the dashboard only reads E6-1's snapshot, so it cannot reach the audio callbacks; the values that never change during a run are captured once at start rather than published per buffer.
- **The speaker warning on the dashboard** (default): the requirements' Feedback row asks for it in the menu and on the dashboard.
- **Widths are tested, not just looked at** (default): "readable" is made checkable as no wrapping and no cut-off values at 120 columns.

## Open Questions
