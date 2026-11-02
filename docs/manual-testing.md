# Manual testing per epic

What the owner can check by hand after each epic, on the Mac, beyond what `dotnet test` and CI already prove. Run everything from the repo root. Use headphones for every check that plays sound.

Before any epic, the automatic checks should pass:

```bash
dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test
```

## E1 Foundation

1. `dotnet run --project src/SolidBackup -- --help` prints the help in Swedish, and `-- --version` prints the version.
2. `dotnet run --project src/SolidBackup` shows the menu titled `solid-backup`; choosing `Avsluta` quits with no error.
3. In the menu, press `Ctrl+C`: the app ends without a stack trace.
4. Write an invalid value in `~/Library/Application Support/solid-backup/usersettings.json` (e.g. `{ "BufferSize": -1 }`) and start the app: it says in Swedish that the file is ignored and starts with the defaults. Delete the file afterwards.
5. On GitHub, the CI run on the latest PR is green.

## E2 Audio

1. `dotnet run --project src/SolidBackup -- --devices` lists your inputs and outputs, marks the system defaults and flags the built-in speakers.
2. `-- --passthrough` with headphones: you hear your own voice with no audible delay or crackle. Stop with `q`, `Esc` or `Ctrl+C`; the dropout counts printed at the end should be 0 (or very close) after a minute of talking.
3. `-- --passthrough --input "<exact name>" --output "<exact name>"` with an external microphone or headphones uses those devices; a wrong name gives a Swedish error and the device list.
4. With the built-in speakers as output, the app warns about feedback.
5. Remove the terminal's microphone access (System Settings → Privacy & Security → Microphone) and run `--passthrough`: the app stops with instructions instead of starting. Give the access back afterwards.
6. On Apple Silicon (if you have one): repeat 1–2; E2-1 only verified an Intel Mac.

## E3 Analysis

1. `dotnet run --project tools/SolidBackup.CorpusTool -c Release -- report` prints pitch accuracy ≥ 95 %, octave errors ≤ 1 %, false notes ≤ 2 % and onsets within 20 ms ≥ 90 %, and no notes in silence and breath with the gate on.
2. Record a new clip of your own (follow `corpus/script.md`), label it with `-- label <clip>`, and check that the medians it prints are within ±50 cents of the notes you sang. Run the report again: the targets still hold.
3. Try a whistled clip up to about C7 and a low sung note near E2: both are found.

## E4 Harmony

1. `dotnet run --project src/SolidBackup -- --harmonize corpus/recordings/singing.wav` prints the time, note and chord at every change, and the key when `auto` finds it.
2. Run the same file with each style: `--style Church`, `Campfire`, `Drone`, `Pop`, `Modal`, `Blues`, `Free` (and `--key G` or `--mode Dorian` where it makes sense). Read the chords against `docs/harmony.md`: do they make musical sense to you, and does each style feel different?
3. Record a melody in a known key and check that `auto` finds that key, and that it follows when you modulate.
4. Known open points from the E4 release: how long `auto` takes before the first chord (16 s on the singing recording), Free's keep with a new tension, Modal after a short silence, and Locrian's character note.

## E5 Sound

Live mode comes in E6; until then everything in E5 can be heard by rendering to a file, playing in the headphones, or running a recording through the whole chain. Open the files in QuickTime.

1. **Synthesizer (E5-1):** `dotnet run --project src/SolidBackup -- --play --sound Pad`, then `--sound Organ` and `--sound Pluck` (or `--render chords.wav --sound Organ` to a file). Each sounds like its name, the chords are in tune, and there are no clicks when the chord changes. With `--chord-ms 150` the fast changes should still not click.
2. **Level features (E5-2):** `-- --accompany corpus/recordings/singing.wav --render sung.wav`, then the same with `--features none`, `--features gate` and `--features dynamics,character`. With the gate the chords stop in the pauses; with dynamics they follow your loudness; with character soft notes give root and fifth and loud ones a bass note below; with onsets a clearly sung new note changes the chord at once. Record yourself (`--passthrough --record`) and try that file too.
3. **Recording (E5-3):** `-- --passthrough --record` for a few minutes in headphones: every count in the summary is 0, and the file in `recordings/` plays with your voice on the left and the output on the right, in sync, without gaps.
4. **Rhythm patterns (E5-5):** `-- --play --pattern Arpeggio --bpm 90` and `-- --play --pattern Strum --time-signature 3/4`. Arpeggio plays even eighths and Strum one strike per beat with the tones 10 ms apart, beat 1 a little stronger; 3/4 gives three beats per bar. Check against a metronome for a minute: no drift. A chord change comes on the pattern's next step.
5. **Jazz (E5-6):** `-- --harmonize corpus/recordings/singing.wav --style Jazz --key C` prints the changes; `-- --accompany corpus/recordings/singing.wav --render jazz.wav --style Jazz --pattern Arpeggio` lets you hear them. On the tonic the chords loop Imaj7–VIm7–IIm7–V7, two beats each, also in the pauses; a foreign note gives a secondary dominant, and a second one moves the key through IIm7–V7–Imaj7.

## E6 Live mode

This is where the whole experiment is judged; the demo in `Constitution/Requirements-and-goal.md` is the script.

1. **Menu (E6-2):** start without arguments, choose devices, key, style, sound, pattern, tempo, level features and recording, and start live mode. Leave with `Esc` or `q` and restart the app: your choices are the new defaults. Every choice changes what you hear.
2. **Latency (E6-1):** run `dotnet run --project src/SolidBackup -c Release -- --live --pattern Held` (until the menu exists). With Held and a low-latency microphone, the latency shown on the status line (later the dashboard) is under 50 ms, and the chords feel immediate when you sing. With the built-in MacBook microphone the target is expected to be missed (it reports ~45 ms alone).
3. **Dropouts (E6-1):** run `--live` for an hour, once without and once with `--record`; the dropout count on the status line and in the summary stays at 0, and the recording plays back with the voice left and the chords right.
4. **Dashboard (E6-3):** at 120 characters' width the note and cents, level meter, chord, key, features on, latency and dropouts are readable and update a few times per second.
5. **The demo:** headphones, microphone, key `auto`, sound pad, pattern arpeggio, dynamics, gate and onsets on, recording on. Sing a simple melody: the key and chords follow you, louder and softer change loudness and character, silence plays nothing, and the recording plays back afterwards.
6. **How it sounds:** the final judgement — do the chords sound right, and does it feel like playing an instrument?
