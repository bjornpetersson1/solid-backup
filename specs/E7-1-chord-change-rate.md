# E7-1 · Chord change rate

- **Issue:** [#70](https://github.com/bjornpetersson1/solid-backup/issues/70)
- **Labels:** `E7 Playtest`
- **Phase:** 7. Playtest
- **Depends on:** none open (builds on E4-2 (#12), E5-5 (#18), E6-2 (#20), E6-3 (#21))
- **Status:** Approved

## Summary
Two new settings make how often the chord changes a choice: a maximum number of chord changes per bar, with changes placed on beats of the tempo grid, and a sensitivity that scales the hold times. The owner found the chords too restless in the E6 playtest.

## Goals
- Chord changes while singing land on beats of the tempo grid (`docs/rhythm.md`), at most a chosen number per bar.
- A simple low / medium / high choice for how easily a new note changes the chord.
- Both chosen in the menu, remembered, and shown on the dashboard.

## Non-goals
- Autoplay through silence (E7-2).
- The chord choice itself (which chord a note gives); every style's rules are unchanged.
- Jazz's tonic loop and a modulation's II – V – I: they keep their own timing on the grid and are not limited by the maximum.
- Delaying the first chord after silence: it still comes at once.
- Changing the grid, the tempo source or the rhythm patterns.
- Changing settings during live mode.

## The rules (proposed text for `docs/harmony.md`, "When the chord may change")

Two new settings (validated, with Swedish errors, in the README and the menu):

| Setting | Values | Default | Menu (Swedish) |
|---|---|---|---|
| `ChordChangesPerBar` | `Off`, `1`, `2`, `Beat` | `2` | Ackordbyten per takt: Av, 1, 2, Varje slag |
| `ChordChangeSensitivity` | `Low`, `Medium`, `High` | `Medium` | Känslighet för ackordbyten: Låg, Medel, Hög |

- **Sensitivity** multiplies both hold times, `MinNoteForChangeMs` and `MinChordDurationMs`: Low ×2, Medium ×1, High ×0.5. With the defaults that is 240/500 ms, 120/250 ms and 60/125 ms. The two settings stay in `appsettings.json` as the Medium values.
- **Allowed beats** are counted on the tempo grid from the start of live mode (`Bpm`, `TimeSignature`), beat 1 of each bar being its downbeat:
  - `1`: beat 1 of each bar;
  - `2`: beats 1 and 3 of each bar, in 4/4 and in 3/4 alike;
  - `Beat`: every beat;
  - `Off`: no grid, today's behaviour.
- **Rule 2 becomes:** while singing, a new note becomes *ready* once it has lasted the scaled `MinNoteForChangeMs` and the scaled `MinChordDurationMs` has passed since the last change. With `Off`, a ready note is evaluated at once, as today. Otherwise it is evaluated on the first allowed beat at or after it became ready, if it is still the sounding note then; the change takes effect on that beat.
- **Silence on the beat:** a ready note that has ended before its beat changes nothing (rule 3, silence keeps the chord). A new note that starts before the beat must become ready on its own.
- **After silence, at once (rule 1)** is unchanged: the first note after silence chooses immediately, off the grid, and it is not counted against the maximum.
- **Onsets:** with the onsets level feature on, a new note right after an onset is ready at once, without the hold times, and is evaluated on the next allowed beat (with `Off`, at once, as today).
- **Jazz:** the tonic loop and a modulation's II – V – I change on their own beats and are neither limited by the maximum nor counted against it. Jazz changes caused by the melody (common rules, secondary dominants, the start of the loop or of a modulation) follow the rule above.
- **The examples** in `docs/harmony.md` so far (T, K, L, D, P, M, B, F, J) are run with `ChordChangesPerBar` `Off` and `ChordChangeSensitivity` `Medium`; their table intro says so.

### New examples (C, for "change rate")

Church, C Ionian, 120 BPM in 4/4 (one beat = 500 ms, one bar = 2000 ms), times in ms from the start of live mode, Medium sensitivity, onsets off, unless a row says otherwise.

| # | Setup | Notes (start–end, ms) | Chords | Why |
|---|---|---|---|---|
| C1 | `2` | C 0–500, B 500–1500 | C at 0, G at 1000 | B is ready at 620; the next allowed beat is beat 3 at 1000. |
| C2 | `1` | C 0–500, B 500–2500 | C at 0, G at 2000 | Only beat 1 is allowed; the next one is at 2000. |
| C3 | `2` | C 0–500, A 500–1200 | C at 0, F at 1000 | A is ready at 620 and still sounds at 1000; candidates F, Am → priority 4. |
| C4 | `2` | C 0–500, B 500–900, silence 900–1100, A 1100–1600 | C at 0, F at 1100 | B is ready at 620 but has ended before 1000; A is the first note after a short silence and chooses at once. |
| C5 | `Beat`, High | C 0–200, B 200–700 | C at 0, G at 500 | Ready at 260 (60 ms note, 125 ms chord); the next beat is 500. |
| C6 | `Off`, Low | C 0–500, B 500–1000 | C at 0, G at 740 | No grid; ready at 500 + 240 (compare T3, G at 620). |
| C7 | `2`, 3/4 | C 0–500, B 500–1300, A 1300–2000 | C at 0, G at 1000, Am at 1500 | 3/4: allowed beats 0, 1000, 1500, 2500 …; A is ready at 1420, beat 1 of bar 2 is at 1500. |
| C8 | `2`, onsets on | C 0–500, B with an onset 500–1500 | C at 0, G at 1000 | The onset makes B ready at once; it still waits for 1000. |
| C9 | `2`, Jazz, C major | C 0–4000 | Cmaj7 at 0, Am7 at 1000, Dm7 at 2000, G7(11) at 3000 | J1 unchanged: the loop is not limited. |

## Acceptance Criteria
1. **With a maximum of N changes per bar, a melody never gives more than N chord changes in any bar, and every change falls on a beat of the grid.**
   - For `1`, `2` and `Beat`, in 4/4 and 3/4, every chord change while singing takes effect at the first harmony update at or after an allowed beat (within one analysis step), and no bar has more changes than the maximum (1, 2, or the beats per bar). The exceptions are the first chord after silence and Jazz's loop and II – V – I, as in the rules.
   - Checked by the C examples as tests, and by a test that runs every corpus melody (E3-1) through each style with each maximum and both time signatures and asserts the two conditions on every change.
2. **Higher sensitivity gives more chord changes than lower on the same corpus melody; the defaults match a value the owner approved.**
   - On a named corpus melody, Church in its key, `Off`: High gives strictly more changes than Medium, and Medium strictly more than Low. A test proves it.
   - The defaults are `ChordChangesPerBar` `2` and `ChordChangeSensitivity` `Medium` (owner's choice); a test reads them from `appsettings.json`.
3. **Both settings are in the menu, remembered, and change the live behaviour.**
   - The menu has one item for chord changes (Swedish), which asks the maximum, then the sensitivity, each starting on the current value; the settings table shows both. Each choice is saved at once to the user settings file (E6-2's rule).
   - The menu test drives the new item and asserts the settings the live entry point receives; a test builds a new menu on the same files and sees the saved choices.
   - Live mode's harmonizer is built from both settings and the grid; a test shows that the same input gives different chord changes with different values.
   - The dashboard (E6-3) shows both values in Swedish; a dashboard test asserts the text.
   - Invalid values in `appsettings.json` or the user settings file give a Swedish validation error; tests cover both settings.
4. **`docs/harmony.md` describes the new rules, with examples that are tests and pass.**
   - The common rules and the settings table in `docs/harmony.md` say what "The rules" above says, the earlier examples state they run with `Off` and `Medium`, and examples C1–C9 are added; each is a test that passes.
   - All earlier harmony and rhythm examples still pass (`dotnet test`).
5. **0 bytes allocated per callback still holds.** The live chain's allocation test passes with both settings on non-default values.
6. **Manual check (owner):** the chords no longer feel restless when singing live. Added to `docs/manual-testing.md`.

## Tasks
- [ ] `ChordChangesPerBar` and `ChordChangeSensitivity` in `AppSettings`, `appsettings.json`, validation (Swedish errors) and the README settings table.
- [ ] The harmonizer applies the sensitivity factor and places melody-caused changes on the allowed beats of the grid; onsets and the first chord after silence as in the rules; Jazz's timed changes untouched.
- [ ] Live mode, `harmonize` and other harmonizer users build it from the settings and the grid.
- [ ] Menu item, settings table row and Swedish texts; the dashboard shows both values.
- [ ] Update `docs/harmony.md` (rules, settings, example intro, C1–C9) and `docs/manual-testing.md`.
- [ ] Tests: C1–C9, the corpus-wide grid test, the sensitivity test, defaults, validation, menu, dashboard, allocation.

## Decisions
- **First chord after silence at once** (owner's choice): keeps the latency target with Held; only changes while singing wait for the beat. It is not counted against the maximum, so the AC's "every change on a beat" applies to changes while singing.
- **Values `Off`, `1`, `2`, `Beat`, default `2`** (owner's choice): `Off` keeps today's behaviour; `2` is beats 1 and 3 in both 4/4 and 3/4; `Beat` is 4 or 3 per bar. The default `2` is the owner-approved value the issue allows instead of today's behaviour.
- **Sensitivity is a factor ×2 / ×1 / ×0.5 on both hold times** (owner's choice); `MinNoteForChangeMs` and `MinChordDurationMs` stay as the Medium values, default Medium.
- **Jazz's loop and II – V – I are exempt** (owner's choice): they are already on the grid and are the style's own form.
- **Onsets skip the hold times but still wait for the beat** (owner's choice), so the maximum always holds.
- **A ready note that has ended by its beat changes nothing** (owner's choice), consistent with "silence keeps the chord".
- **Earlier examples run with `Off`** (default): they describe the hold-time rules, which are unchanged; the new default would otherwise rewrite most of them.
- **Grid origin** (default): the start of live mode, the same grid as the patterns and Jazz; in tests, time 0.
- **One menu item asking two things** (default), like the rhythm item (pattern, tempo, time signature).
- **Setting names and values** (default): `ChordChangesPerBar` (`Off`, `1`, `2`, `Beat`) and `ChordChangeSensitivity` (`Low`, `Medium`, `High`), in the style of the existing settings.

## Open Questions
- None.
