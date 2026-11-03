# E7-4 · Free style with three-tone chords

- **Issue:** [#73](https://github.com/bjornpetersson1/solid-backup/issues/73)
- **Labels:** `E7 Playtest`
- **Phase:** 7. Playtest
- **Depends on:** none open (builds on E4-6 (#44); written after E7-1 (#70))
- **Status:** Implemented — all automated acceptance criteria verified (format, build with 0 warnings, 1123 tests passed); how Free sounds live is a manual check for the owner

## Summary
The Free style chooses among every chord of three pitch classes that contains the note, and moves to a new root at least every other chord change. In the E6 playtest Free stayed on one root and only changed colour (Cmaj7 → Cmaj7(#11)).

## Goals
- Free sounds varied: three-tone chords of every shape, and root movement.
- The rules are written down in `docs/harmony.md` and its examples are tests.
- The dashboard names every new chord.

## Non-goals
- The other styles, including Jazz's `G7(11)` and Pop's colours, which keep their chords and symbols.
- Tensions in Free: the seventh chords with tensions are gone from Free.
- When the chord may change (E7-1's rules apply to Free unchanged).
- Voicing, the synthesizer and the rhythm patterns: they already play chords of any number of tones.

## The rules (proposed text for `docs/harmony.md`, "Free (Fri)")

- **No key**, as before.
- **Chords:** every set of three pitch classes. Each set has exactly one name and one root, from the first shape in this list it matches (the shape's first tone is the root):

  | # | Shape (semitones above the root) | On C | Symbol |
  |---|---|---|---|
  | 1 | 0 4 7 | C E G | `C` |
  | 2 | 0 3 7 | C Eb G | `Cm` |
  | 3 | 0 5 7 | C F G | `Csus4` |
  | 4 | 0 4 10 | C E Bb | `C7(no5)` |
  | 5 | 0 3 10 | C Eb Bb | `Cm7(no5)` |
  | 6 | 0 4 11 | C E B | `Cmaj7(no5)` |
  | 7 | 0 2 4 | C D E | `Cadd9(no5)` |
  | 8 | 0 2 3 | C D Eb | `Cmadd9(no5)` |
  | 9 | 0 3 6 | C Eb Gb | `Cdim` |
  | 10 | 0 4 8 | C E G# | `Caug` |
  | 11 | 0 7 10 | C G Bb | `C7(no3)` |
  | 12 | 0 7 11 | C G B | `Cmaj7(no3)` |
  | 13 | 0 4 6 | C E F# | `C(#11,no5)` |
  | 14 | 0 3 4 | C Eb E | `C(#9,no5)` |
  | 15 | 0 3 11 | C Eb B | `Cm(maj7,no5)` |
  | 16 | 0 1 3 | C Db Eb | `Cm(b9,no5)` |
  | 17 | 0 1 7 | C Db G | `C5(b9)` |
  | 18 | 0 6 7 | C F# G | `C5(#11)` |
  | 19 | 0 1 2 | C Db D | `C(b9,9)` |

  These are the 19 shapes of three pitch classes up to transposition, so every set has one. Sus2 and quartal chords are the same sets as sus4 and are named sus4 (C D G is `Gsus4`, C F Bb is `Fsus4`). The augmented triad is the same set on three roots; as a candidate its root is the note (rule 3.5 below).
- **Choice:**
  1. After silence with no previous chord (at start or after a phrase reset): the note as the root of a major triad.
  2. **Keep:** if the note is in the current chord, it stays. A keep is not a chord change.
  3. Otherwise the candidates are all the chords that contain the note. **Root rule:** if the last chord change kept the root (a change to a chord with the same root as the one before it), candidates with the current chord's root are left out. Among the candidates, take the one with, in order:
     1. the most pitch classes in common with the current chord;
     2. the least movement: the sum, over the candidate's three tones, of each tone's distance in semitones (0–6) to the nearest tone of the current chord;
     3. the earliest shape in the list;
     4. the root by the smallest interval from the root up to the note.
  - The root rule's memory starts afresh with the first chord after silence with no previous chord; the first note after a short silence (with a previous chord) is a chord change like any other.
- **Contains the note:** always.

### New examples (replacing F1–F3)

| # | Notes | Chords | Why |
|---|---|---|---|
| F1 | C A | C Am | A: Am, Am7(no5) and A7(no3) share 2 tones and move 2 semitones; minor is the earliest shape. |
| F2 | C F | C Csus4 | F: Csus4, Fmaj7(no3) and Em(b9,no5) share 2 tones and move 1; sus4 is earliest. |
| F3 | C F B | C Csus4 G7(no5) | C → Csus4 kept the root, so root C is left out; G7(no5) and F5(#11) share 2 and move 1; shape 4 before 18. |
| F4 | C F B C | C Csus4 G7(no5) Csus4 | C: Csus4, Cmaj7(no3) and F5(#11) share 2 and move 1; sus4 is earliest. G → C is a new root. |
| F5 | C Eb F | C Cm Ebadd9(no5) | Eb: Cm (same root). F: Csus4 would win (shape 3) but C → Cm kept the root; Ebadd9(no5) and F7(no3) share 2 and move 2; shape 7 before 11. |
| F6 | C E G | C C C | Keep: every note is in C; no change. |
| F7 | D, silence 2500 ms, F# | D F# | The phrase reset forgets the chord, so there is none to keep; F# is the root of a major triad. |

## Acceptance Criteria
1. **Every Free chord has exactly three pitch classes and contains the stable note.**
   - A test runs every note after every possible current chord (all 220 sets with their names, and no chord) and asserts the result has three pitch classes, contains the note, and has the name and root the table gives its set.
   - A test runs every corpus melody (E3-1) through Free and asserts the same for every chord.
2. **In any sequence of chord changes in Free, no two changes in a row keep the same root.**
   - A property test over random note sequences (fixed seed, at least 10 000 notes, with silences short and long) asserts that among consecutive chord changes within a phrase no two in a row keep the root; the corpus melodies are checked the same way.
3. **The new Free examples in `docs/harmony.md` are tests that pass.**
   - The Free section of `docs/harmony.md` says what "The rules" above says (with the overview table's Free row updated), F1–F3 are replaced by the examples above, and each is a test that passes.
4. **The dashboard names the new chords.** Every symbol in the table is produced for its set on every root (test), and the dashboard shows it (dashboard test with a Free chord).
5. **Manual check (owner):** Free sounds varied and moves between roots when singing live. Added to `docs/manual-testing.md`.

## Tasks
- [x] The 19 shapes and their symbols in the chord model.
- [x] Rewrite Free's rules: three-tone candidates, keep, the root rule, the new order; no tensions.
- [x] Rewrite the Free section, the overview row and the examples in `docs/harmony.md`; `docs/manual-testing.md`.
- [x] Tests: the examples, the exhaustive set test, the root-rule property test, the corpus test, symbols, dashboard.

## Decisions
- **All 19 three-tone shapes** (owner's choice): any three pitch classes that contain the note, as the issue's scope says, in the order above with consonant shapes first.
- **One name and one root per set, named in the table's order** (owner's choice, "named root per shape"): the root is the shape's first tone and the one in the symbol, whatever the voicing. Sus2 and quartal are named sus4.
- **At least every other change moves the root** (owner's choice), as the issue says; otherwise the order favours common tones and least movement, as before.
- **A keep is not a chord change** (owner's choice).
- **The order of criteria** (default): the old order without the tension criteria; the root rule filters before it, so the issue's rule always holds.
- **The augmented triad's root is the note** (default): it is the same set on three roots, and the last tie-break picks the note.
- **After silence: the note as root of a major triad** (default): Free's old rule took a `maj7`; a major triad is its three-tone counterpart.
- **E7-1's timing applies** (default): the change rate and sensitivity work on Free's changes like any style's.

- **The chord model** (implementation): the 15 shapes the existing qualities did not cover are new `ChordQuality` values (`Dominant7No5` … `Cluster`) with their tones and symbols in `Chord`; the major, minor, sus4 and diminished triads reuse the existing qualities. `FreeRules.Shapes` lists the 19 in the table's order. The seventh chords and tensions stay for Jazz (`G7(11)`).
- **The root rule's state** (implementation): `FreeRules` remembers whether the last change kept the root; it starts over when there is no current chord and on `Reset` (a phrase reset). Each harmonizer has its own instance, as with Blues and Jazz.
- **The property tests** (implementation): the root rule is checked twice: through the harmonizer on 10 000 random notes and the corpus melodies, and on the rules alone over 100 000 random notes, which fails at once without the rule (the harmonizer's hold times make a second same-root change rare, so the first test alone would not show it).
- **The allocation test's expected count** for Free is 7 changes on its melody (D, Bm, D, E, Cmaj7(no5), E, E7(no5)), worked out by hand from the rules.
- **Docs** (implementation): the Free section, its overview row, the symbol list and examples F1–F7 in `docs/harmony.md`; the chord-choice row in `Constitution/Requirements-and-goal.md`; E7 step 4 in `docs/manual-testing.md`.

- **Clean code:** `FreeRules` uses `Chord.PitchClassMask` and `Contains` instead of its own bit table, and tries only the roots that put one of a shape's tones on the note (an indexed loop, so it allocates nothing); the new rows of the interval table are labelled; the tests share one root-rule check (`RootRule`) and check the corpus in one pass.
- **Declined clean-code findings:** b9, 9 and #11 stay as tensions of the chord model, as chord symbols with them are still read (`ChordSymbol`, the synth command's chords); trimming them would change that surface, outside this issue. One table holding each quality's intervals, suffix and allowed tensions would be a restructuring of the shared chord model; the qualities, intervals and suffixes stay in their existing places, kept together by the symbol tests on every root.

- **Code review fixes:**
  - **Symbols:** "C5(b9)" and "C5(#11)" were also a power chord with a tension, and the symbol table kept whichever came last; Free's shapes and the power chord now take no tensions in `ChordSymbol`, so these symbols read as Free's shapes, and symbols such as "C7(no5)(9)" or "Caug(#11)" are not chords (tests).
  - **Sparse clusters:** the sparse shape keeps the root and the fifth or a third; Free's cluster has neither and played its root alone. It now keeps the root and the lowest other tone; every Free shape is two tones sparse (test).
  - **The root rule's state** follows the chords passed in: a current chord the rules did not choose starts the rule afresh (test), so it does not rely on the caller applying every chosen chord.
  - **Dashboard:** a test shows Free's new symbols on the Ackord row.
  - **Docs:** the E4 manual check no longer lists Free's tensions as an open point.
  - **Smaller:** the search runs over a private array of the shapes, the root is written as `(note − interval) mod 12`, and a stray blank line went.

- **Verification:** `--harmonize`'s Free test now expects `C` then `Em` for C4, B4 (the first chord is a major triad; B is not in C and Em shares two tones and moves one semitone).

## Open Questions
- None.
