# E6-3 · Live dashboard

- **Issue:** [#21](https://github.com/bjornpetersson1/solid-backup/issues/21)
- **Labels:** `E6 Live mode`
- **Phase:** 6. Live mode
- **Depends on:** E6-1 (#19)
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 979 tests passed twice in a row); readability on the owner's terminal and an hour with the dashboard on are manual checks for the owner

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
- [x] The dashboard view, built from a snapshot (a pure function from snapshot and width to the rendered panel).
- [x] Redrawing in place about five times a second on the console thread, replacing E6-1's status line in live mode.
- [x] Add to the snapshot what the rows need and E6-1 did not already carry (style, sound, BPM and time signature, gate threshold, speaker warning, recording length), set once at start where they do not change.
- [x] Tests for the content, the widths and the redraw interval.
- [x] README and `docs/manual-testing.md` E6 step 4.

## Decisions
- **One panel of rows** (owner's choice): simple to read, and fits narrower windows than a grid of panels.
- **About five redraws a second** (default): "a few times per second" from the issue; fast enough to follow a melody, slow enough to cost little.
- **Builds on E6-1:** the dashboard only reads E6-1's snapshot, so it cannot reach the audio callbacks; the values that never change during a run are captured once at start rather than published per buffer.
- **The speaker warning on the dashboard** (default): the requirements' Feedback row asks for it in the menu and on the dashboard.
- **Widths are tested, not just looked at** (default): "readable" is made checkable as no wrapping and no cut-off values at 120 columns.

- **What the snapshot carries** (implementation): `LiveSnapshot` now holds the run's `AppSettings` (style, sound, rhythm, features, gate threshold and switch, latency target, key setting for `(auto)`) in place of E6-1's feature names and pattern, and a `RecordingStatus` (length, dropped buffers) read from the recorder. The speaker warning and where q/Esc lead are not live state; they are a `DashboardContext` handed to the view, made once at start: `AudioSession` hands the speaker warning it already computed to the activity in an `AudioStart` (devices, recorder, warning), and the menu builds its `LiveCommand` with `returnsToMenu`.
- **Spectre's live display** (implementation): `console.Live(...)` redraws the panel in place; the stop keys' 50 ms poll asks a `RedrawPacer` whether 200 ms have passed, so a redraw costs nothing on the audio threads. It replaces E6-1's status line. The display ends when `WaitForStop` returns, before the fade-out and the stop.
- **Fitting 80 columns** (implementation): the labels have a fixed width of 10 (`Funktioner`), the latency row is written compactly (`42.8 ms ✓ mål 50 · in 6.0 · kedja 30.5 · överl. 1.3 · ut 5.0`), and the meter takes the width that is left, 10 to 40 cells. At 120 columns the widest values (the longest chord symbol, key, style, five-digit latencies, seven-digit counts) still fit on one line each.
- **Marks** (implementation): ✓ and ✗ for the features and the latency, as in the layout the owner chose; the dropout count is red and bold above 0; the speaker warning is a yellow row.

- **Clean code:** the meter's width follows from the label column (the longest label), the panel's borders and the level text at its widest, not a hand-counted number; milliseconds and the latency's parts are written by one `LatencyTexts` helper for the dashboard and the summary; the summary takes only the snapshot, which carries the target; `--live` no longer prints the old "Live-läget pågår" line, as the dashboard's footer says how to stop; the tests share `TestSnapshots`.
- **Kept as they are** (clean-code findings declined): the run's settings stay in the snapshot rather than in `DashboardContext`, as this spec's task says the snapshot carries what the rows need; the dashboard is rebuilt from the snapshot on each redraw rather than caching the rows that never change, since it is a pure function of the snapshot (simple to test) and the cost is a few dozen small objects five times a second on the console thread, never on the audio threads; the test that the audio side allocates nothing while the dashboard draws on another thread stays, as criterion 3 asks for exactly that.

- **Code review fixes:** the panel shows the speaker warning as a short row (`ljudet går till de inbyggda högtalarna – använd hörlurar`), since the full sentence (128 characters) cannot fit; the latency row drops the target's unit so two-digit device latencies and a three-digit total still fit 80 columns (tested with 45 + 34 + 10.7 + 33.3 = 123.0 ms); a level below the meter's floor reads `<-60 dBFS`, so the level text never grows past the room kept for it; a gate threshold below the floor is not marked rather than drawn at −60; the dBFS figure has its own format rather than the latency helper's; the speaker check runs once, in `AudioSession`, and reaches live mode through `AudioStart`; the allocation test waits for the dashboard's first draw before it measures.
- **`LevelFeatures.NamesOn` stays** (code review finding declined): `AccompanyCommand.WrittenMessage` still uses it.

## Open Questions
