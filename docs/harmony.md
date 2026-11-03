# Harmony: how a note becomes a chord

- **Issue:** #11 (E4-1)
- **Status:** Approved by the owner (2026-10-31)

This document is the decision from the E4-1 discussion. It is the rule book for every harmony issue: E4-2 (engine, Church, Campfire, Drone), E4-3 (key detection), E4-4 (Pop, Modal), E4-5 (Blues), E4-6 (Free) and E5-6 (Jazz). Code follows it; it does not invent rules of its own. Every example in [Examples](#examples) becomes a test.

## Overview

The user picks a **harmony style** in the menu. Each style is a separate rule set with its own chord vocabulary and its own way of choosing:

| Style (menu, Swedish) | Setting value | In short | Built in |
|---|---|---|---|
| Kyrko | `Church` | Diatonic triads in the chosen mode, strict | E4-2 |
| Lägereld | `Campfire` | I, IV, V, vi | E4-2 |
| Bordun | `Drone` | Root and fifth of the tonic, always | E4-2 |
| Pop | `Pop` | I, V, vi, IV with add9/sus4 colours and borrowed chords | E4-4 |
| Modal | `Modal` | Tonic pedal with the mode's character chord | E4-4 |
| Blues | `Blues` | I7, IV7, V7, blue notes, moves key on foreign notes | E4-5 |
| Jazz | `Jazz` | Diatonic seventh chords, secondary dominants, II–V–I into a new key, a tonic loop that never stands still | E5-6 |
| Fri | `Free` | No key; seventh chords with tensions, chosen by voice leading from the previous chord | E4-6 |

The melody's direction (up or down) plays no part in any style.

## Inputs, state and output

The harmony is a pure, deterministic function, called whenever the analysis reports a change:

- **Input:** the current stable note from the pitch detector (E3-2, already smoothed by its note hold), or "no note", with the time in milliseconds.
- **Key:** a tonic (one of the 12 pitch classes) and a mode (see [Keys and modes](#keys-and-modes)).
- **State:** the current chord (or none), when it last changed, when the current note started, when the last note ended, and for Blues the current blues key.
- **Output:** the current chord, or none before the first note. A chord is a root pitch class, a quality, its pitch classes and a symbol (e.g. `Am7`), plus the *current key* that the dashboard shows. Voicing, register, sparse or full chords and whether anything sounds at all are decided in E5 (synth, level features, gate), not here.

Root names are always spelled `C Db D Eb E F F# G Ab A Bb B`. Symbols: `C` major, `Cm` minor, `Cdim` diminished, `C5` root and fifth, `C7`, `Cmaj7`, `Cm7`, `Cm7b5`, `Cdim7`, `Cadd9`, `Cmadd9`, `Csus4`, and tensions in brackets, e.g. `Cmaj7(#11)`, `C7(b9)`, `Cm7(11)`.

## Common rules (all styles)

### When the chord may change

Five settings in `appsettings.json` (validated, with Swedish errors, listed in the README); the last two are chosen in the menu (E7-1):

| Setting | Default | Meaning |
|---|---|---|
| `MinNoteForChangeMs` | 120 | While singing, a new note must have lasted this long before it may change the chord. |
| `MinChordDurationMs` | 250 | While singing, a chord change comes no earlier than this long after the previous change. |
| `PhraseResetMs` | 2000 | After a silence this long, the previous chord is forgotten. |
| `ChordChangesPerBar` | `2` | While singing, the most chord changes per bar, each on an allowed beat: `1`, `2`, `Beat` or `Off`. Menu: Ackordbyten, first question (Av, 1, 2, Varje slag). |
| `ChordChangeSensitivity` | `Medium` | How easily a new note changes the chord: `Low`, `Medium` or `High`. Menu: Ackordbyten, second question (Låg, Medel, Hög). |

- **Sensitivity** multiplies both hold times, `MinNoteForChangeMs` and `MinChordDurationMs`: Low ×2, Medium ×1, High ×0.5. With the defaults that is 240/500 ms, 120/250 ms and 60/125 ms. "The hold times" below are the scaled ones.
- **Allowed beats** are counted on the tempo grid of `docs/rhythm.md` from the start of live mode (`Bpm`, `TimeSignature`), beat 1 of each bar being its downbeat: `1` allows beat 1 of each bar; `2` beats 1 and 3, in 4/4 and in 3/4 alike; `Beat` every beat; `Off` uses no grid.

1. **After silence, at once.** The first note after "no note" (or at start) chooses a chord immediately, so the first chord is within the latency target. Neither hold time applies, it does not wait for a beat, and it is not counted against `ChordChangesPerBar`.
2. **While singing, after the hold times, on an allowed beat.** When the note changes without silence in between, the new note becomes *ready* once it has lasted `MinNoteForChangeMs` *and* `MinChordDurationMs` has passed since the last change, whichever is later. With `ChordChangesPerBar` `Off`, a ready note is evaluated at once. Otherwise it is evaluated on the first allowed beat at or after it became ready, if it is still the sounding note then; the change takes effect on that beat. A shorter passing note changes nothing, and a ready note that has ended before its beat changes nothing either (rule 3). A new note that starts before the beat must become ready on its own.
3. **Silence keeps the chord.** "No note" does not change the chord; the harmony keeps its state. Whether the chord keeps sounding is up to E5.
4. **Phrase reset.** If the silence before a note lasted at least `PhraseResetMs`, that note chooses as if there were no previous chord, and Blues returns to the menu's key. With `Autoplay` on (E7-2, `docs/rhythm.md`) the reset still happens; only the sound goes on with the last chord.

An onset (E3-3) is not a phrase start. With the onsets level feature on (E5-2), a new note right after an onset is ready at once, without the hold times, and is evaluated at once with `Off` or on the next allowed beat otherwise; its chord is struck anew.

Jazz's tonic loop and a modulation's II – V – I change on their own beats; they are neither limited by `ChordChangesPerBar` nor counted against it. Jazz changes caused by the melody follow rule 2.

### Choosing among candidates (key-based styles)

Church, Campfire, Pop and Blues choose the same way; Drone, Modal, Free and Jazz have their own rules below.

1. **Candidates** are the chords in the style's set that contain the note.
2. **Keep:** if the current chord is a candidate, it stays.
3. **Natural step:** otherwise, walk the previous chord's ordered successor list and take the first successor that is a candidate.
4. **Priority:** otherwise, take the candidate first in the style's priority order.
5. **No candidate:** keep the current chord. After silence, with no current chord to keep, take the style's tonic chord.

Degrees are counted from the mode's tonic (1–7), whatever the chord's quality. The successor lists are:

| Previous degree | Successors, in order |
|---|---|
| 1 | — |
| 2 | 5 |
| 3 | 6 |
| 4 | 5, 1 |
| 5 | 1, 6 |
| 6 | 4, 2 |
| 7 | 1 |

The default priority order is **1, 5, 4, 6, 2, 3, 7**; a style may give its own.

## Keys and modes

- **Settings:** `Key` is a tonic (`C`, `F#`, `Bb`, …) or `auto`; `Mode` is one of `Ionian`, `Dorian`, `Phrygian`, `Lydian`, `Mixolydian`, `Aeolian`, `Locrian` (default `Ionian`). The menu shows them in Swedish.
- **Modes are for Church and Modal.** The other key-based styles only know major and minor: Ionian, Lydian and Mixolydian count as major, the rest as minor. Free ignores the key entirely, and the dashboard shows `–`.
- **`auto` (E4-3)** estimates the tonic and major or minor from the recent notes. With `auto`, `Mode` is ignored: Church and Modal use Ionian for major and Aeolian for minor. A different mode needs a fixed key. How the estimate works, and how it follows a modulation without flickering, is E4-3's spec.
- **A style may move the current key.** The menu's key (fixed or `auto`) is the starting key. Blues (and later Jazz) may move the *current key* that the dashboard shows, as their rules say.

## The styles

### Church (Kyrko)

- **Chords:** the diatonic triads of the chosen mode, leaving out the diminished triad. The one exception is Locrian, where the tonic itself is diminished and is kept, so the mode has a home chord.

  | Mode (on C) | Chords |
  |---|---|
  | Ionian | C Dm Em F G Am |
  | Dorian | Cm Dm Eb F Gm Bb |
  | Phrygian | Cm Db Eb Fm Ab Bbm |
  | Lydian | C D Em G Am Bm |
  | Mixolydian | C Dm F Gm Am Bb |
  | Aeolian | Cm Eb Fm Gm Ab Bb |
  | Locrian | Cdim Db Ebm Fm F# Ab Bbm |

- **Strict:** Aeolian keeps its minor fifth degree (`v`); there is no harmonic-minor dominant.
- **Choice:** the common rules with the default priority. A note outside the mode is in no chord, so the chord stays.
- **Contains the note:** always, except a foreign note, which is ignored.

### Campfire (Lägereld)

- **Chords:** major: I, IV, V, vi (on C: C F G Am). Minor: i, iv, V, VI (on A: Am Dm E F); the major V is deliberate.
- **Choice:** the common rules with the default priority. A note in none of the four chords (a foreign note, or the minor seventh degree in minor) keeps the chord.

### Drone (Bordun)

- **Chord:** the tonic's root and fifth (`C5`), always, whatever the note or mode. It changes only when the key does.
- **Contains the note:** no.

### Pop

- **Chords:** major: I, V, vi, IV, with priority **1, 5, 6, 4**. Minor: i, bVII, bVI, bIII, with priority **1, 7, 6, 3**.
- **Colours**, applied after *keep* and before *natural step*, and only when the current chord is from this set (not a borrowed one):
  - the note is the ninth (a major second above the root) of the current chord → the current chord with `add9` (`Cadd9`, `Amadd9`);
  - the note is the fourth above the root of a major current chord → `sus4` (`Csus4`).

  A coloured chord counts as containing its colour note; a colour change is a chord change and follows the hold times.
- **Borrowed chords**, used when nothing in the set contains the note (instead of keeping the chord): major borrows bVII, iv, bVI (on C: Bb Fm Ab), minor borrows V, IV (on A: E D). The first in that order that contains the note is chosen. A borrowed chord's successor is the tonic.
- **Contains the note:** always, as a chord tone or a colour.

### Modal

- **Chords:** the mode's tonic triad, and one character chord triggered by the mode's character note:

  | Mode (on C) | Tonic | Character note | Character chord |
  |---|---|---|---|
  | Ionian | C | F (4) | F (IV) |
  | Dorian | Cm | A (6) | F (IV) |
  | Phrygian | Cm | Db (b2) | Db (bII) |
  | Lydian | C | F# (#4) | D (II) |
  | Mixolydian | C | Bb (b7) | Bb (bVII) |
  | Aeolian | Cm | Ab (b6) | Ab (bVI) |
  | Locrian | Cdim | F# (b5) | F# (bV) |

- **Choice:** the character note gives the character chord; a note of the tonic triad gives the tonic; any other note keeps the chord. After silence: the character chord if the note is the character note, otherwise the tonic.
- **Contains the note:** no; the chord works as a pedal.

### Blues

- **Chords:** I7, IV7, V7 of the current blues key, in major and minor alike, with priority **1, 4, 5**. Successors as in the common rules.
- **Blue notes:** the minor third, diminished fifth and minor seventh above the current blues key's tonic (on C: Eb, Gb, Bb) always keep the current chord, even when another chord contains them. After silence they give I7.
- **Moving the key:** a note that is neither in I7, IV7 or V7 nor a blue note moves the current blues key to the nearest key on the circle of fifths in which one of I7, IV7, V7 contains the note. When two keys are equally near, the flat side wins. The chord is then chosen in the new key with the common rules. The dashboard shows the new key.
- **The key goes back** to the menu's key (or the current `auto` estimate) on a phrase reset, or when that key changes.
- **Contains the note:** yes, except blue notes.

### Free (Fri)

- **No key.** The style only looks at the note and the previous chord.
- **Chords:** every root with the qualities below, each with the tensions it allows:

  | Quality | Tones (semitones above root) | Tensions |
  |---|---|---|
  | `maj7` | 0 4 7 11 | 9 (2), #11 (6) |
  | `m7` | 0 3 7 10 | 9 (2), 11 (5) |
  | `7` | 0 4 7 10 | b9 (1), 9 (2), #11 (6) |
  | `m7b5` | 0 3 6 10 | 11 (5) |
  | `dim7` | 0 3 6 9 | — |

- **Choice:**
  1. After silence with no previous chord: the note as the root of a `maj7`.
  2. **Keep:** if the note is a tone or an allowed tension of the current chord, it stays; when the note is a tension, the chord takes it as its tension (so it always contains the note).
  3. Otherwise, among all chords that have the note as a tone or a tension, take the one with, in order:
     1. the most pitch classes in common with the previous chord's four tones;
     2. the least movement: the sum, over the candidate's four tones, of each tone's distance in semitones (0–6) to the nearest tone of the previous chord;
     3. quality in the order `maj7`, `m7`, `7`, `m7b5`, `dim7`;
     4. the note as a chord tone rather than a tension;
     5. the smallest interval from the root up to the note.
  4. When the note is a tension, the chord includes it (`Cmaj7(#11)`).
- **Contains the note:** always, as a tone or a tension; the tensions give the dissonance.

### Jazz

Decided with the owner in E5-6. Jazz uses the tempo grid from `docs/rhythm.md` (beats counted from the start of live mode, `Bpm`, `TimeSignature`), whatever the rhythm pattern.

- **Chords:** the diatonic seventh chords of the current key, with priority **1, 5, 4, 6, 2, 3, 7** and the common successors.

  | Key | Chords (on C) |
  |---|---|
  | Major | Imaj7 IIm7 IIIm7 IVmaj7 V7 VIm7 VIIm7b5 (Cmaj7 Dm7 Em7 Fmaj7 G7 Am7 Bm7b5) |
  | Minor | Im7 IIm7b5 bIIImaj7 IVm7 V7 bVImaj7 bVII7 (Cm7 Dm7b5 Ebmaj7 Fm7 G7 Abmaj7 Bb7); V7 is the harmonic-minor dominant |

- **Choice when the note is in the key:** the common rules (keep, natural step, priority), except for the tonic note below.
- **The tonic loop:** when the stable note is the current key's tonic, the chord becomes the tonic chord at once (after the hold times while singing, like any change), and the loop starts: I – VI – II – V (major: Imaj7 – VIm7 – IIm7 – V7; minor: Im7 – bVImaj7 – IIm7b5 – V7), two beats per chord, around and around.
  - The loop changes chord on every other beat of the grid: the beats with an even index counted from the grid's origin (in 4/4 beats 1 and 3 of each bar; in 3/4 alternately across the bar lines). The second chord comes on the first such beat at least one beat after the tonic chord started.
  - While the tonic sounds, V7 takes it as the 11th (`G7(11)` on C), so every loop chord contains the note. In silence V7 is plain.
  - The loop goes on through silence and never stops by itself; only a new note that is not the tonic stops it and is chosen by the rules. Loop changes do not wait for `MinChordDurationMs`.
- **A foreign note** (not in the current key):
  1. **Secondary dominant:** the V7 of a diatonic chord other than the tonic and the m7b5 chord (on C major: A7 B7 C7 D7 E7, the dominants of II III IV V VI), that contains the note. When several do, the one whose target comes first in the order 5, 4, 6, 2, 3, 7. A secondary dominant's successor is its target.
  2. **Modulation:** the second foreign note in the same phrase (counted since the last phrase reset or key change) moves the current key to the nearest key on the circle of fifths, in the same major or minor, whose scale contains the note; the flat side wins a tie. The harmony then plays II – V – I of the new key, one chord per beat: II at once, V on the next beat of the grid, I on the beat after, without hold times and whatever the melody does meanwhile. After that the common rules apply in the new key. The dashboard shows the new key.
- **The key goes back** to the menu's key (or the current `auto` estimate) on a phrase reset, or when that key changes, as in Blues.
- **Contains the note:** yes, as a chord tone or as V7's 11th in the loop, except during a modulation's II – V – I, which is played through.
- **Rhythm:** in Jazz the pattern does not stop after `PhraseResetMs` of silence while the loop runs (`docs/rhythm.md`); with the gate on, silence is still silent.

## Examples

Unless a row says otherwise, each note lasts 500 ms, the notes follow without silence, and the settings are the defaults with `ChordChangesPerBar` `Off` (and `ChordChangeSensitivity` `Medium`), so every note passes the hold times; the change-rate examples (C) say their own. "Chord" is the chord in effect while that note sounds, after its hold time.

### Timing (Church, C Ionian)

| # | Notes (start–end, ms) | Chords | Why |
|---|---|---|---|
| T1 | C 0–500 | C at 0 | After silence: chosen at once. |
| T2 | C 0–500, D 500–580, C 580–1000 | C throughout | D lasts 80 ms < `MinNoteForChangeMs`. |
| T3 | C 0–500, B 500–1000 | C at 0, G at 620 | B evaluated at 500 + 120. |
| T4 | C 0–50, B 50–500 | C at 0, G at 250 | The hold time is met at 170, but `MinChordDurationMs` holds the change until 250. |
| T5 | B 0–500, A 500–1000, silence 1000–1800, C 1800–2300 | G, Am, Am | 800 ms silence: Am remembered and contains C. |
| T6 | B 0–500, A 500–1000, silence 1000–3500, C 3500–4000 | G, Am, C | 2500 ms silence ≥ `PhraseResetMs`: chosen afresh, priority gives I. |

### Change rate (Church, C Ionian)

120 BPM in 4/4 (one beat = 500 ms, one bar = 2000 ms), times in ms from the start of live mode, Medium sensitivity, onsets off, unless a row says otherwise.

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

### Church

| # | Key | Notes | Chords | Why |
|---|---|---|---|---|
| K1 | C Ionian | C E G C | C C C C | The current chord contains each note. |
| K2 | C Ionian | C F G C | C F G C | F: candidates F, Dm, priority → F. G: candidates C, Em, G; IV's first successor V → G. C: V → I. |
| K3 | C Ionian | C F# G | C C C | F# is outside the mode and is ignored. |
| K4 | C Ionian | F# (after silence) | C | No candidate and no current chord → tonic. |
| K5 | C Ionian | B D E | G G C | B: G, Em → priority 5. E: V's first successor I is a candidate. |
| K6 | D Dorian | D B A D | Dm G Am Dm | B: G, Em (Bdim left out) → priority 4. A: IV's successor v → Am. D: v → i. |
| K7 | A Aeolian | E B G# A | Am Em Em Am | Strict: the fifth degree is Em; G# is foreign. |

### Campfire

| # | Key | Notes | Chords | Why |
|---|---|---|---|---|
| L1 | C major | E D C | C G C | D is only in G (Dm is not in the set). |
| L2 | A minor | A G# A G | Am E Am Am | G# is in the major V; G is in none of the four chords. |
| L3 | C major | C Eb | C C | Eb is foreign. |

### Drone

| # | Key | Notes | Chords |
|---|---|---|---|
| D1 | D (any mode) | D F# C G# | D5 D5 D5 D5 |

### Pop

| # | Key | Notes | Chords | Why |
|---|---|---|---|---|
| P1 | C major | C D E B | C Cadd9 Cadd9 G | D is the ninth of C; E is in Cadd9; B is no colour of C → G. |
| P2 | C major | C F A | C Csus4 Am | F is the fourth of major C; A: Am, F → Pop priority 6 before 4. |
| P3 | C major | C Bb C | C Bb C | Bb is in no chord in the set → borrowed bVII; no colour on a borrowed chord; bVII → tonic. |
| P4 | C major | C Ab | C Fm | Borrowed order bVII, iv, bVI: Fm is the first that contains Ab. |
| P5 | A minor | A G# | Am E | Not a colour (sus4 needs a major chord) → borrowed V. |

### Modal

| # | Key | Notes | Chords | Why |
|---|---|---|---|---|
| M1 | D Dorian | D E B C F | Dm Dm G G Dm | E is neither the tonic nor the character note; B is the character note; F is in the tonic triad. |
| M2 | G Mixolydian | G F D B | G F G G | F (b7) → bVII; D is in the tonic triad. |
| M3 | C Ionian | C F E | C F C | F (4) → IV. |

### Blues

| # | Key | Notes | Chords | Current key | Why |
|---|---|---|---|---|---|
| B1 | C | C E F A | C7 C7 F7 F7 | C | F: F7, G7 → priority 4. |
| B2 | C | C Eb Gb Bb | C7 C7 C7 C7 | C | Blue notes keep the chord, even Eb, which F7 contains. |
| B3 | C | C B C | C7 G7 C7 | C | B: only G7. C: V's first successor I. |
| B4 | C | C Ab | C7 Bb7 | F | Ab is foreign; F (one step flat) has Bb7 containing Ab. |
| B5 | C | C Db | C7 Eb7 | Bb | Nothing one step away; Bb and D are two steps away (Eb7, A7) → the flat side. |
| B6 | C | C Ab, silence 2500 ms, E | C7 Bb7 C7 | F, then C | Phrase reset returns to the menu's key. |

### Free

| # | Notes | Chords | Why |
|---|---|---|---|
| F1 | C A | Cmaj7 Am7 | A: Am7 and Em7(11) both share 3 tones and move 2 semitones, are both `m7`; A is a chord tone in Am7. |
| F2 | C A F# | Cmaj7 Am7 Cmaj7(#11) | F#: Cmaj7(#11), C7(#11) and F#m7b5 share 3 tones and move 1 → `maj7` first. |
| F3 | C A E | Cmaj7 Am7 Am7 | E is in Am7. |

### Jazz

C major, 120 BPM in 4/4 (one beat = 500 ms; loop changes at 0, 1000, 2000 … ms), unless a row says otherwise. Times are ms from the start of live mode, which is the grid's origin.

| # | Notes (start–end, ms) | Chords | Current key | Why |
|---|---|---|---|---|
| J1 | C 0–4000 | Cmaj7 at 0, Am7 at 1000, Dm7 at 2000, G7(11) at 3000 | C | The tonic starts the loop; V7 takes the held C as its 11th. |
| J2 | C 1600–4500 | Cmaj7 at 1600, Am7 at 3000, Dm7 at 4000 | C | 2000 is less than one beat after 1600; the loop waits for 3000. |
| J3 | E 0–500, D 500–1000, C 1000–3500 | Cmaj7 at 0, G7 at 620, Cmaj7 at 1120, Am7 at 2000, Dm7 at 3000 | C | E: priority 1. D: no keep or successor, priority 5. C: the loop. |
| J4 | C 0–500, silence | Cmaj7 at 0, Am7 at 1000, Dm7 at 2000, G7 at 3000, Cmaj7 at 4000 … | C | The loop goes on through silence; V7 is plain without a note. |
| J5 | C 0–500, F# 500–1000, G 1000–1500 | Cmaj7 at 0, D7 at 620, G7 at 1120 | C | F#: D7 and B7 contain it; target 5 before 3. G: the successor of D7 is its target, G7. |
| J6 | As J5, then Bb 1500–2000 | … Gm7 at 1620, C7 at 2000, Fmaj7 at 2500 | F from 1620 | Bb is the second foreign note: F is the nearest key with Bb; II – V – I one per beat. |
| J7 | As J6, silence until 5000, E 5000–5500 | … Fmaj7 at 2500, Cmaj7 at 5000 | F, then C at 5000 | Phrase reset: the key goes back to C; E chooses by priority 1. |
| J8 | A minor: A 0–4000 | Am7 at 0, Fmaj7 at 1000, Bm7b5 at 2000, E7(11) at 3000 | Am | The minor loop. |
| J9 | 3/4: C 0–4000 | Cmaj7 at 0, Am7 at 1000, Dm7 at 2000, G7(11) at 3000 | C | Every other beat across the bar lines gives the same times as 4/4. |
| J10 | C 0–1100, E 1100–1600 | Cmaj7 at 0, Am7 at 1000, Am7 kept at 1250 | C | E is a new note and stops the loop, evaluated once `MinChordDurationMs` has passed since the loop change at 1000; Am7 contains E, so it is kept. |

## What changes in the plan

- **E4-2** (#12) builds the engine, the common rules, the settings (`HarmonyStyle` default `Church`, `Key`, `Mode`, `MinNoteForChangeMs`, `MinChordDurationMs`, `PhraseResetMs`), and Church, Campfire and Drone, with their examples (T, K, L, D) as tests.
- **E4-3** (#13) estimates the tonic and major or minor only.
- **New issues:** E4-4 Pop and Modal (#42), E4-5 Blues (#43), E4-6 Free (#44), each depending on E4-2, and E5-6 Jazz (#45), depending on E4-2 and E5-4. Each one's examples above become its tests.
- **Requirements:** the chord-choice line in `Constitution/Requirements-and-goal.md` says that the chord contains the note *in the styles whose rules say so*, and the epic lists gain the new issues.
