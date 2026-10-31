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
| Jazz | `Jazz` | Diatonic seventh chords, secondary dominants, ii–V–I, never stands still | E5-6 (needs tempo from E5-4) |
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

Three settings in `appsettings.json` (validated, with Swedish errors, listed in the README):

| Setting | Default | Meaning |
|---|---|---|
| `MinNoteForChangeMs` | 120 | While singing, a new note must have lasted this long before it may change the chord. |
| `MinChordDurationMs` | 250 | While singing, a chord change comes no earlier than this long after the previous change. |
| `PhraseResetMs` | 2000 | After a silence this long, the previous chord is forgotten. |

1. **After silence, at once.** The first note after "no note" (or at start) chooses a chord immediately, so the first chord is within the latency target. Neither hold time applies.
2. **While singing, after the hold times.** When the note changes without silence in between, the new note is evaluated once it has lasted `MinNoteForChangeMs` *and* `MinChordDurationMs` has passed since the last change, whichever is later, provided the note is still sounding. A shorter passing note changes nothing.
3. **Silence keeps the chord.** "No note" does not change the chord; the harmony keeps its state. Whether the chord keeps sounding is up to E5.
4. **Phrase reset.** If the silence before a note lasted at least `PhraseResetMs`, that note chooses as if there were no previous chord, and Blues returns to the menu's key.

Whether an onset (E3-3) may count as a new phrase start is decided in E5-2, which owns onset-triggered chord change.

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

### Jazz (direction; details in E5-6)

Jazz needs a time base, so its detailed rules are decided in E5-6, once `docs/rhythm.md` (E5-4) gives the tempo. The direction agreed here:

- **Chords:** diatonic seventh chords (Imaj7, IIm7, IIIm7, IVmaj7, V7, VIm7, VIIm7b5), secondary dominants, and IIm7–V7–Imaj7 into a new key when a note is foreign to the current key. Like Blues, the style may move the current key.
- **Never stands still:** instead of resting on I while the melody rests or holds, it loops I–VIm7–IIm7–V7 at the tempo.
- **Contains the note:** yes.

## Examples

Unless a row says otherwise, each note lasts 500 ms, the notes follow without silence, and the settings are the defaults, so every note passes the hold times. "Chord" is the chord in effect while that note sounds, after its hold time.

### Timing (Church, C Ionian)

| # | Notes (start–end, ms) | Chords | Why |
|---|---|---|---|
| T1 | C 0–500 | C at 0 | After silence: chosen at once. |
| T2 | C 0–500, D 500–580, C 580–1000 | C throughout | D lasts 80 ms < `MinNoteForChangeMs`. |
| T3 | C 0–500, B 500–1000 | C at 0, G at 620 | B evaluated at 500 + 120. |
| T4 | C 0–50, B 50–500 | C at 0, G at 250 | The hold time is met at 170, but `MinChordDurationMs` holds the change until 250. |
| T5 | B 0–500, A 500–1000, silence 1000–1800, C 1800–2300 | G, Am, Am | 800 ms silence: Am remembered and contains C. |
| T6 | B 0–500, A 500–1000, silence 1000–3500, C 3500–4000 | G, Am, C | 2500 ms silence ≥ `PhraseResetMs`: chosen afresh, priority gives I. |

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

## What changes in the plan

- **E4-2** (#12) builds the engine, the common rules, the settings (`HarmonyStyle` default `Church`, `Key`, `Mode`, `MinNoteForChangeMs`, `MinChordDurationMs`, `PhraseResetMs`), and Church, Campfire and Drone, with their examples (T, K, L, D) as tests.
- **E4-3** (#13) estimates the tonic and major or minor only.
- **New issues:** E4-4 Pop and Modal (#42), E4-5 Blues (#43), E4-6 Free (#44), each depending on E4-2, and E5-6 Jazz (#45), depending on E4-2 and E5-4. Each one's examples above become its tests.
- **Requirements:** the chord-choice line in `Constitution/Requirements-and-goal.md` says that the chord contains the note *in the styles whose rules say so*, and the epic lists gain the new issues.
