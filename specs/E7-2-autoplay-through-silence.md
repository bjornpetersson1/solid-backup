# E7-2 · Autoplay through silence

- **Issue:** [#71](https://github.com/bjornpetersson1/solid-backup/issues/71)
- **Labels:** `E7 Playtest`
- **Phase:** 7. Playtest
- **Depends on:** none open (builds on E5-2 (#15), E5-5 (#18), E6-1 (#19), E6-2 (#20), E6-3 (#21); written after E7-1 (#70))
- **Status:** Approved

## Summary
A switch `Autoplay`: when it is on, the rhythm pattern keeps playing the last chord on the tempo grid through silence, so the groove goes on between phrases. The owner asked for it in the E6 playtest.

## Goals
- With autoplay on, silence never stops or silences the accompaniment once a chord has been chosen; only a new note (or leaving live mode) changes what plays.
- With autoplay off, nothing changes.
- The switch is chosen in the menu, remembered and shown on the dashboard.

## Non-goals
- Playing a progression of its own during silence (the last chord repeats; Jazz's loop already moves on its own and is unchanged).
- How often the chord changes (E7-1).
- Changing the rhythm patterns, the grid or the chord choice.
- Fading the chords out after a long silence.
- Changing settings during live mode.

## The rules (proposed text for `docs/rhythm.md` and `docs/harmony.md`)

New setting (validated, with a Swedish error, in the README settings table and the menu):

| Setting | Values | Default | Menu (Swedish) |
|---|---|---|---|
| `Autoplay` | `true`, `false` | `false` | Autospel: På / Av |

With `Autoplay` on:

- **The pattern does not stop.** After the first chord, the pattern keeps playing the current chord on the grid through any silence, however long, until a new note changes the chord or live mode is left (`docs/rhythm.md`, "Stop" does not apply).
  - **Arpeggio and Strum** go on stepping on the grid with the last chord, with the usual accent.
  - **Held** keeps the chord sounding; it is not struck again.
- **The gate does not silence the chords.** With the gate on, silence is not silent: the chord keeps playing. The gate still decides which notes reach the harmony, so noise below the gate does not change the chord.
- **Level during silence.** With dynamics on, the chord keeps the strength it had on the last buffer in which a note reached the harmony; with chord character on, it keeps that buffer's character (sparse, normal or full). Both follow the level again from the next note.
- **Phrase reset still happens.** After `PhraseResetMs` of silence the harmony forgets the previous chord and Blues and Jazz return to the menu's key, as in `docs/harmony.md` rule 4; the chord that is playing keeps sounding, and the next note chooses as after a phrase reset. The dashboard shows the key the harmony is in.
- **Before the first note** nothing plays; autoplay continues a chord, it does not start one.
- **Leaving live mode** (`Esc`/`q`, `Ctrl+C`) fades out or stops as in E6-2.

With `Autoplay` off, everything is as before, including the examples R1–R12 and Jazz's own rule that its loop keeps the pattern playing.

### New examples (A, for "autoplay")

48 kHz, 90 BPM (one beat = 32000 samples, one eighth = 16000), 4/4, `Autoplay` on, the chord C major voiced C E G from sample 0, unless a row says otherwise.

| # | Setup | Expected | Why |
|---|---|---|---|
| A1 | Arpeggio, gate off; the last note ends at 100000, then 10 s of silence | Steps go on every 16000 samples through 580000 and beyond, C E G C E G C E in each bar. | The pattern does not stop after `PhraseResetMs`. |
| A2 | Strum, gate on; as A1 | A strike on every beat through the silence, full strength on downbeats, 0.8 on the others. | The gate does not silence the chords. |
| A3 | Held, gate on; as A1 | The chord sounds without a new strike through the silence. | Held is not struck again. |
| A4 | Arpeggio, gate on, dynamics on; the last note's strength is −6 dB | Every step during the silence plays at −6 dB (−8 dB on the non-downbeats). | The last strength is kept. |
| A5 | Strum, gate on; during the silence, noise below the gate with a detectable pitch (e.g. a quiet B) | The chord stays C. | Noise below the gate does not reach the harmony. |
| A6 | Church C Ionian, Strum; B (G), silence 2500 ms, then A | G keeps playing through the silence; A chooses F. | The phrase reset still happens: A is chosen afresh (candidates F, Am → priority 4), not Am as V's successor in a remembered phrase. |

## Acceptance Criteria
1. **With autoplay on and a melody followed by 10 s of silence, the pattern keeps playing the last chord on the grid for the whole silence.**
   - For Held, Arpeggio and Strum, with the gate on and off, a chain test (generated tones through `IAudioInput`, the in-memory `IAudioOutput`) renders a melody and 10 s of silence and asserts that the pattern's tones start on every step of the grid (Arpeggio, Strum) or the chord keeps sounding (Held) until the end, with the last chord's tones, and that the output is not silent in any 100 ms window of the silence.
   - Examples A1–A3 are tests that pass.
2. **With autoplay off, the behaviour is unchanged.**
   - The rhythm examples R1–R12 in `docs/rhythm.md` and every harmony example still pass, and a chain test shows the pattern stops after `PhraseResetMs` and the gate silences silence at once, as before.
3. **With autoplay and the gate on, silence does not silence the chords, and noise below the gate still does not change the chord.**
   - Examples A2 and A5 are tests that pass; A4 shows the kept strength and A6 the phrase reset.
4. **The switch is in the menu, remembered, and changes the live behaviour; 0 bytes allocated per callback still holds.**
   - The menu has an item for autoplay (Swedish, On/Off, starting on the current value); the settings table shows it; the choice is saved at once to the user settings file (E6-2's rule). The menu test drives it and asserts the settings the live entry point receives; a new menu on the same files shows the saved value.
   - Live mode's chain is built from `Autoplay`; a test shows the same input gives sound during a long silence with it on and none with it off.
   - The dashboard shows the value in Swedish; a dashboard test asserts the text.
   - An invalid value gives a Swedish validation error (test).
   - The live chain's allocation test passes with autoplay on, during the silence.
5. **The documents describe it.** `docs/rhythm.md` ("When a pattern plays", the settings table) and `docs/harmony.md` (rule 4, phrase reset) say what "The rules" above says, with examples A1–A6 in `docs/rhythm.md`.
6. **Manual check (owner):** pauses between phrases keep the groove going. Added to `docs/manual-testing.md`.

## Tasks
- [ ] `Autoplay` in `AppSettings`, `appsettings.json`, validation and the README.
- [ ] The chain keeps the pattern playing through silence with autoplay on: the phrase no longer ends the pattern, the gate no longer silences, dynamics and chord character hold their last values.
- [ ] Menu item, settings table row, dashboard text (Swedish).
- [ ] Update `docs/rhythm.md`, `docs/harmony.md` and `docs/manual-testing.md`.
- [ ] Tests: A1–A6, the 10 s chain test for each pattern with the gate on and off, unchanged behaviour with it off, menu, dashboard, validation, allocation.

## Decisions
- **Level during silence is the last level** (owner's choice): strength and chord character are kept from the last buffer with a note, so the groove goes on as it was.
- **Phrase reset still happens** (owner's choice): the harmony forgets and keys return as today; only the sound goes on with the last chord.
- **Held keeps the chord sounding, no re-strike** (owner's choice).
- **Default off** (owner's choice): today's behaviour unless the owner switches it on in the menu.
- **Its own menu item** (default): autoplay is not a level feature (the level features are separate switchable components of the analysis-to-sound path; autoplay is about the pattern), so it is not in the level-feature multi-select.
- **The recording** (default): the recorded output is what is played, so it includes the autoplayed chords.
- **Before the first note nothing plays** (default): the issue speaks of keeping the last chord going.
- **Builds on E7-1** (default): chord changes after silence follow E7-1's rules unchanged — the first note after silence chooses at once.

## Open Questions
- None.
