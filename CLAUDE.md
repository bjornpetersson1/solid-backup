# solid-backup

Experimental .NET console app for macOS: it listens to a microphone (built-in or connected), detects the pitch and level of a single melody line, and plays chords in response through its own simple synthesizer, with under about 50 ms latency. It is part accompanist and part instrument. The app measures itself against numeric targets for pitch accuracy, latency and dropouts. The requirements live in `Constitution/Requirements-and-goal.md`; every piece of work is a GitHub issue on the project board "solid-backup".

## Language

- Talk to the owner in **Swedish**.
- Everything committed or posted is in **English**: code, comments, specs, commit messages, PR text, review comments.
- Exception: text shown to the user in the console (menu, dashboard, error messages) is in **Swedish**.

## Workflow

Work is done issue by issue with `/ship-issue <issue number>`, which chains these skills:

1. `/spec-from-issue` – questions → spec in `specs/` → **owner approval** (the only planned stop)
2. `/implement-spec`
3. `/clean-code`
4. built-in `/code-review` (fix every finding)
5. `/run-test-and-check-spec`
6. `/open-pr` – commit, push, PR against `develop`
7. `/review-pr` – inline review comments on the PR
8. `/address-pr-review` – fix, clean code, verify, push, reply and resolve
9. Report to the owner, who reviews and merges.

Each skill can also be run on its own.

To ship a whole epic, run `/ship-epic <EPIC>`, e.g. `/ship-epic E2`. It:

1. writes the specs for all the epic's issues and gets them approved in one round (its only planned stop);
2. runs each issue through `/ship-issue` and merges its PR into `develop`;
3. opens the release PR into `main` and runs `/release-check`.

Discussion issues (E4-1 music theory, E5-4 tempo) produce an approved document in `docs/`, not code; they are worked through with the owner, not shipped automatically.

When an epic is done and `develop` is ready for `main`, run `/release-check <release PR number>`. It verifies the whole release, runs clean code over it, fixes what is wrong on a `feature/<EPIC>-release-check` branch, merges that fix PR into `develop` and posts a review on the release PR. The owner's only step is merging the release PR into `main`.

## Git and GitHub rules

- `develop` is the integration branch; `main` is only updated by the owner. PRs always target `develop`, except the release PR from `develop` into `main`.
- Branch per issue: `feature/<KEY>-<slug>`, e.g. `feature/E1-1-repo-and-skeleton`, created from `origin/develop`.
- Never push to `develop` or `main`, never merge, never force-push, never rewrite pushed history. The exceptions:
  - `/ship-epic` merges its spec PR and each issue's PR into `develop`, and opens the release PR from `develop` into `main`;
  - `/release-check` merges its own fix PR into `develop`.

  Only the owner merges into `main`.

- Commit messages: `<KEY>: <imperative summary>`, ending with the trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Move issues on the board with `.claude/scripts/set-issue-status.sh <issue> "<Todo|In Progress|In Review|Done>"`.

## Verification

The canonical checks are run from the repo root (the same steps as CI in `.github/workflows/ci.yml`, added in E1-1):

```bash
dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test
```

- Tests run without audio hardware and without network access: audio comes from generated tones and the WAV corpus through `IAudioInput`, and goes to an in-memory `IAudioOutput`.
- Outside `/implement-spec` and `/run-test-and-check-spec`, run the narrowest check that answers the question (`dotnet build`, `dotnet test --filter <test class>`), not the whole suite.
- Never weaken an acceptance criterion or a target in Measurement, skip or delete a test to get to green.
- Live checks (latency on the owner's Mac, an hour without dropouts, how it sounds) need a microphone and headphones: list them as manual checks for the owner instead of claiming them.

## Constraints from the requirements

- .NET 8, C#, macOS only. Audio through PortAudio (CoreAudio). Everything runs locally; no network calls, no hosted services.
- Audio goes through two interfaces, `IAudioInput` and `IAudioOutput`.
- The audio callback never allocates, locks or does I/O; the dashboard reads a snapshot through a lock-free hand-over.
- Pitch detection, level analysis, harmony and synthesis are pure, deterministic and buffer based.
- Monophonic input only, pitch range E2–C6 by default (configurable).
- Every level feature (dynamics, gate, onsets, chord character, …) is a separate component that can be switched on or off.
- Chord choice follows `docs/harmony.md` (E4-1) and tempo follows `docs/rhythm.md` (E5-4) once approved; do not invent these rules.
- Headphones are assumed; no echo cancellation.
- Defaults come from `appsettings.json`; the menu's choices are saved to a git-ignored user settings file.
- Targets: ≥ 95 % pitch accuracy (±50 cents), ≤ 1 % octave errors, ≤ 2 % false notes, ≥ 90 % onsets within 20 ms, < 50 ms latency, 0 dropouts, 0 bytes allocated per callback.
