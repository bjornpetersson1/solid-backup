# E5-2 · Level features

- **Issue:** [#15](https://github.com/bjornpetersson1/solid-backup/issues/15)
- **Labels:** `E5 Sound`
- **Phase:** 5. Sound
- **Depends on:** E3-3 (#10), E4-2 (#12), E5-1 (#14)
- **Status:** Approved

## Summary
Four level features, each a separate switchable component behind one mechanism: the gate (silence is silent), dynamics (chord loudness follows the input level), chord character (soft gives a sparse chord, loud a fuller one) and onsets (an articulated note changes the chord without waiting and strikes it anew). They sit between the analysis and harmony (E3, E4) and the synthesizer (E5-1). `--accompany` runs a recording through the whole chain into a WAV file.

## Goals
- One mechanism that every level feature plugs into, so a new one is added without changing the others.
- The four features, each on or off by a setting, all on by default.
- An offline chain (WAV → analysis → harmony → features → synth) that the corpus test, `--accompany` and later E5-5 and E6-1 use.

## Non-goals
- The menu (E6-2); the switches are settings and a command-line override here.
- Rhythm patterns (E5-5); this chain plays the chord held. E5-5 decides *when* tones start; the features decide *which* tones, how strong, and whether anything sounds.
- The live pipeline and the dashboard (E6).
- Changing E3-3's gate, level meter or onset detector, or the harmony rules beyond the onset rule below.
- Features beyond these four.

## Acceptance Criteria
1. **One component per feature behind a common mechanism.**
   - Each feature is its own class behind one interface. Per buffer, each feature reads the analysis (level, gate state, onset, note) and adjusts the accompaniment for that buffer: which chord tones sound, their strength, whether the chord is struck anew, and whether anything sounds.
   - The features run in a fixed order (gate, onsets, chord character, dynamics). A feature that is off is not run, and its aspect stays at the neutral value: all chord tones, full strength, no extra strike, sound on.
   - The switches are the settings `GateOn`, `DynamicsOn`, `ChordCharacterOn` and `OnsetsOn` in `AppSettings` and `appsettings.json`, all `true` by default, listed in the README.
   - The chain allocates nothing per buffer after warm-up, with all features on (a test, as in E2-2).
2. **Gate.**
   - On: a note reaches the harmony only while E3-3's gate is open, and when the gate closes every sounding tone is released at once. When it opens again with a note, the current chord is struck anew.
   - Off: the pitch detector alone decides the note, and the chord keeps sounding through silence until `PhraseResetMs` has passed (the rule in `docs/rhythm.md`).
3. **Dynamics.**
   - On: the strength follows the input level, mapped linearly in dB from `DynamicsQuietDbfs` (default −42, the gate threshold) → −24 dB to `DynamicsLoudDbfs` (default −12) → 0 dB, clamped outside, and smoothed with a fixed 50 ms time constant.
   - Off: full strength.
   - Tests on generated tones: a louder input gives a louder output in the expected ratio; a step in level gives no step in the output (smoothed).
4. **Chord character.**
   - On, from the smoothed level: below `CharacterSparseBelowDbfs` (default −30) *sparse* (root and perfect fifth, or root and third when the chord has no perfect fifth); above `CharacterFullAboveDbfs` (default −18) *full* (the whole chord plus the root an octave below the E5-1 voicing); between them the whole chord. A level must cross a boundary by 3 dB before the character changes (hysteresis).
   - The sparse and full chords use E5-1's voicing, so a change of character only adds or releases tones, with E5-1's click-free rule.
   - Off: the whole chord.
   - Tests: C major soft → C3 G3; mid → C3 E3 G3; loud → C2 C3 E3 G3; `Cdim` soft → C3 Eb3; a level hovering at a boundary does not flip the character.
5. **Onsets.**
   - On: when E3-3 reports an onset, the next stable note within 60 ms (fixed) is evaluated at once, without `MinNoteForChangeMs` and `MinChordDurationMs`. The resulting chord, new or the same, is struck anew: all its tones start with a new attack and the old tones are released. If no new note arrives within the 60 ms, the current chord is struck anew at that point.
   - Off: onsets do nothing, and chords change only by the rules in `docs/harmony.md` with E5-1's common-tone rule.
   - An onset is not a phrase start. `docs/harmony.md` is updated to say so where it leaves the question to E5-2.
   - Tests on generated notes: a repeated note with an onset is struck anew; a note change with an onset changes the chord before the hold times would allow; without onsets neither happens.
6. **Every combination of switches gives the expected output on a corpus file.**
   - The chain runs `corpus/recordings/singing.wav` with each of the 16 combinations of the four switches, at default settings, and the test checks each combination against the rules above, using the reference segments:
     - gate on: no tone sounds in a `silence` or `breath` segment, from the segment's start plus `GateHoldMs` plus the sound's release; gate off: the chord keeps sounding through silences shorter than `PhraseResetMs`;
     - dynamics on: across the note segments, the output strength ranks with the input level (Spearman correlation ≥ 0.8); off: every tone at full strength;
     - chord character on: the number of sounding tones in each note segment is what the segment's smoothed level gives; off: always the whole chord;
     - onsets on: every onset is followed by a new strike within 60 ms; off: no strike without a chord change.
   - Switching one feature changes only its own aspect: for every pair of combinations that differ in one switch, the other aspects are identical. The gate is the exception, since it changes which notes reach the harmony; for it, the test checks the aspects only over the note segments where both runs have the same note.
7. **A new feature can be added without changing the others.**
   - The features are listed in one place (a registry that pairs each feature with its switch); adding one means a new class, a setting and one line there.
   - A test adds a test-only feature to the registry and shows that it runs, and that the four existing features' output is unchanged with it switched off.
8. **The owner can hear the features.**
   - `--accompany <wav> --render <out.wav>` runs the recording through the chain (analysis, harmony, features, E5-1's synth) and writes the voice mixed with the accompaniment to a 16-bit WAV at the recording's rate; `--no-voice` writes the accompaniment alone.
   - It takes `--style`, `--key`, `--mode` and `--sound` as `--harmonize` and `--render` do, and `--features <list>` (e.g. `gate,dynamics`, or `none`) to override the four switches for that run.
   - Errors (unknown feature, missing `--render`, combination with `--passthrough`, `--devices`, `--harmonize` or `--play`) are Swedish with exit code 1. `--help` lists the new options; tests cover a run and the errors.
   - The README "Run" section has an example. Listening to the result is a manual check for the owner.
9. **Settings are validated.** `DynamicsQuietDbfs` < `DynamicsLoudDbfs`, both in −90…0; `CharacterSparseBelowDbfs` < `CharacterFullAboveDbfs`, both in −90…0, at least 6 dB apart (twice the hysteresis); Swedish errors and tests.
10. **The canonical check passes.** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root, on macOS and in CI on Ubuntu.

## Tasks
- [ ] The feature interface, the per-buffer accompaniment it adjusts, and the registry with switches.
- [ ] The gate, dynamics, chord character and onset features.
- [ ] The offline chain from a WAV input to the synth, reusable by E5-5 and E6-1.
- [ ] Settings, validation and README for the switches and the four levels.
- [ ] `--accompany`, `--render` with a recording, `--no-voice`, `--features`.
- [ ] The onset sentence in `docs/harmony.md`.
- [ ] Tests: each feature, the 16 combinations on the singing recording, the extension test, allocation, settings, command line.

## Decisions
- **Onset = change without hold times + a new strike** (owner's choice): an articulated note is heard as one, whether the chord changes or not. Not a phrase start, so Blues keeps its key.
- **Waiting up to 60 ms for the note after an onset** (default): the onset arrives before the pitch detector has a stable new note; striking at once would give the old chord and then the new one. 60 ms is a starting value; if the corpus shows the note usually arrives later, the implementation records the measured value here.
- **Chord character in three levels with hysteresis** (owner's choice): sparse root and fifth, the whole chord, and the whole chord with a bass root. Boundaries are settings, as microphones differ.
- **All four features on by default** (owner's choice), as in the demo.
- **`--accompany` to WAV** (owner's choice): the owner hears the features on a recording, and the corpus test uses the same chain.
- **Dynamics mapped over 24 dB** (default): from the gate threshold to a loud voice; quiet singing is still audible. The smoothing removes level ripple without blurring onsets noticeably.
- **Gate off keeps sounding until `PhraseResetMs`** (from `docs/rhythm.md`): the harmony keeps its chord through silence, and only the gate makes silence silent at once.
- **Fixed feature order** (default): the gate decides whether anything sounds, onsets when to strike, character which tones, dynamics how loud; each aspect has one owner, which is what makes the "changes only its own aspect" test possible.
- **Flat boolean settings** (default): `AppSettings` is flat today; the registry maps each feature to its setting.
- **Builds on E5-1:** the voicing, the click-free common-tone rule and the tone-level strength. Builds on E3-3: the level meter, gate and onset detector, unchanged.

## Open Questions
