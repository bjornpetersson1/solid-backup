# Rhythm: where the tempo comes from and how patterns play

- **Issue:** #17 (E5-4)
- **Status:** Approved by the owner (2026-10-31)

This document is the decision from the E5-4 discussion. It is the rule book for E5-5 (rhythm patterns) and gives E5-6 (Jazz) its time base. Code follows it; it does not invent rules of its own. Every example in [Examples](#examples) becomes a test in E5-5.

## Decisions in short

| Question | Decision |
|---|---|
| Tempo source | A fixed BPM chosen in the menu. No tap tempo, no tempo from onsets, no live following. |
| Onsets and the tempo | Onsets do not touch the tempo or the grid. They only matter for chord changes (E5-2). |
| Where the grid starts | At the start of live mode, and it runs for the whole session. |
| Time signature | 4/4 or 3/4, chosen in the menu. |
| When a pattern plays | While there is a chord; it stops after `PhraseResetMs` of silence. The gate and the level features (E5-2) may silence it earlier. |
| Chord change | Held changes at once; Arpeggio and Strum use the new chord from their next step, including the first chord after silence. |

## Settings

Added to `appsettings.json`, validated with Swedish errors, listed in the README settings table, and later chosen in the menu (E6-2):

| Setting | Values | Default | Menu (Swedish) |
|---|---|---|---|
| `RhythmPattern` | `Held`, `Arpeggio`, `Strum` | `Held` | Hållet, Arpeggio, Ackordslag |
| `Bpm` | whole number, 40–240 | 90 | Tempo (BPM) |
| `TimeSignature` | `4/4`, `3/4` | `4/4` | Taktart |

## The tempo grid

- **Origin.** Sample 0 of the output stream when live mode starts is beat 1 of bar 1. In measurement and tests, sample 0 of the rendered output.
- **Beat length** is `SampleRate × 60 / Bpm` samples, which need not be a whole number.
- **No drift.** The start of step *n* is the exact time *n* × step length, rounded to the nearest sample. It is never built by adding rounded step lengths, so the error stays under half a sample (≈ 0.01 ms at 48 kHz) however long the session runs.
- **Bars** have 4 beats in 4/4 and 3 in 3/4. Beat 1 of each bar is the *downbeat*.
- **Steps** depend on the pattern: Arpeggio steps on every eighth note (2 per beat), Strum on every beat, Held has no steps.
- The grid runs through silence. A pattern that stops and starts again stays on the same grid.
- The grid is a pure function from a sample position to bar, beat and step. It does not allocate and can be used in the audio callback.

## The patterns

All three play the chord's tones in the voicing the synthesizer gives them (E5-1), lowest to highest.

### Held (`Held`)

- The chord sounds as long as the pattern plays.
- A new chord takes over at once, with the synthesizer's click-free transition (E5-1).

### Arpeggio (`Arpeggio`)

- On every step (eighth note) one chord tone starts, and the previous step's tone is released (its release follows the sound's envelope).
- The tone is chosen by the step's place in the bar: step *k* of the bar (counting from 0) plays tone *k* mod *the number of tones*. Every bar therefore starts on the lowest tone, and a three-tone chord in 4/4 plays 1 2 3 1 2 3 1 2.
- A new chord is used from the next step. The tone ringing when the change comes keeps ringing until that step.

### Strum (`Strum`)

- On every beat the chord is struck downwards: the lowest tone exactly on the beat, each next tone 10 ms (480 samples at 48 kHz) after the one below it.
- A strike releases the previous strike's tones as it starts.
- A new chord is used from the next beat. The tones ringing when the change comes keep ringing until that beat.

### Accent

In Arpeggio and Strum, the step on the downbeat plays at full strength and every other step at 0.8 (about −2 dB). Dynamics from E5-2 scale the result; the accent is applied on top.

## When a pattern plays

- **Start.** The pattern starts when the harmony gives its first chord, at start or after a stop. Held sounds at once; Arpeggio and Strum start on the next step of the grid.
- **The first chord also waits.** With a tempo, waiting for the next step is the intended musical timing. It is at most one step: at 90 BPM, 333 ms for Arpeggio and 667 ms for Strum. A chord that arrives exactly on a step's sample plays on that step.
- **Short silences.** The harmony keeps its chord through silence (`docs/harmony.md`), so the pattern keeps playing it.
- **Stop.** When the silence has lasted `PhraseResetMs` (default 2000 ms), the pattern stops: it releases the tones that are sounding and starts no new ones. It starts again on the next chord.
- **The gate and level features.** Whether anything sounds, and how loud, is decided by E5-2 (gate, dynamics, chord character). The pattern decides only *when* tones start and stop. With the gate on, silence is silent at once.

## Latency

The latency target (< 50 ms, `Constitution/Requirements-and-goal.md`) is input latency + analysis + output latency. The wait for the next step in Arpeggio and Strum is deliberate musical timing and is not part of it. Held has no wait, so with Held the chord is heard within the latency target; E6-1 checks the target with Held.

## Jazz

Jazz (E5-6) uses this grid for its time (`docs/harmony.md`): its tonic loop changes chord every other beat, and a modulation's II – V – I one chord per beat. While Jazz's loop runs, the pattern does not stop after `PhraseResetMs` of silence; the loop never stands still. With the gate on, silence is still silent.

## Examples

Unless a row says otherwise: 48 kHz, 90 BPM (one beat = 32000 samples, one eighth = 16000), 4/4, the chord C major voiced C E G, and the chord is there from sample 0. Positions are output samples.

| # | Pattern and setup | Expected | Why |
|---|---|---|---|
| R1 | Strum | Strikes at 0, 32000, 64000, 96000, 128000 …; in each, C at the beat, E 480 and G 960 samples later. Full strength at 0 and 128000, 0.8 on the others. | One strike per beat, 10 ms apart, accent on the downbeat. |
| R2 | Arpeggio | C E G C E G C E starting at 0, 16000, … 112000; bar 2 starts again on C at 128000. | Tone = step in bar mod 3. |
| R3 | Arpeggio, 3/4 | C E G C E G; bar 2 starts on C at 96000. | Six eighths per bar in 3/4. |
| R4 | Arpeggio, a four-tone chord C E G Bb | C E G Bb C E G Bb in bar 1. | Tone = step in bar mod 4. |
| R5 | Arpeggio; the chord changes to F (F A C) at 20000 | E from 16000 rings until 32000; at 32000 C (tone 2 of F A C), at 48000 F. | The new chord from the next step; the tone index follows the bar. |
| R6 | Held; the chord changes to F at 20000 | F from 20000. | Held changes at once. |
| R7 | Strum; the first chord after silence comes at 40000 | Nothing before 64000; the first strike at 64000. | The first chord also waits for the next beat. |
| R8 | Strum; the first chord comes at exactly 64000 | The first strike at 64000. | A chord on a step's sample plays on that step. |
| R9 | Strum, 70 BPM | Beats at 0, 41143, 82286, 123429, 164571, 205714, 246857, 288000. | One beat = 41142.857 samples; each step is rounded from its exact time, so beat 7 lands exactly on 288000. |
| R10 | Strum, 120 BPM at 44.1 kHz | Beats at 0, 22050, 44100 …; E 441 and G 882 samples after each beat. | 10 ms = 441 samples at 44.1 kHz. |
| R11 | Arpeggio; the last note ends at 100000 (gate off, no further note) | Steps keep playing until 196000 (100000 + 2000 ms); none starts at or after 196000. | The pattern stops after `PhraseResetMs` of silence. |
| R12 | As R11, then a new chord at 300000 | Nothing between 196000 and 304000; at 304000 C. | The grid kept running through the stop: 304000 is the next eighth, step 3 of bar 3 (bar 3 starts at 256000), and 3 mod 3 = 0. |

Every position must be met within 1 ms (48 samples at 48 kHz), the acceptance criterion of E5-5; the rounding rule makes them exact.

## What changes in the plan

- **E5-5** (#18) builds the grid, the three patterns, the settings above and the examples as tests. Its tempo source is `Bpm`; nothing else.
- **E5-6** (#45) takes its time base from this grid.
- **E6-1** (#19) checks the latency target with Held.
- **E6-2** (#20) adds the pattern, tempo and time signature to the menu.
- **Requirements:** the tempo-source open point is closed, the latency row says the pattern's step wait is not latency, and step 3 of the demo says that with Arpeggio the chords follow on the next step.
