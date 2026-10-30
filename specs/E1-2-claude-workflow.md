# E1-2 · Claude workflow

- **Issue:** [#2](https://github.com/bjornpetersson1/solid-backup/issues/2)
- **Labels:** `E1 Foundation`
- **Phase:** 1. Foundation
- **Depends on:** E1-1 (#1)
- **Status:** Approved

## Summary
The Claude workflow ported from BörsMönster (`CLAUDE.md`, the skills, `set-issue-status.sh`) was copied and adapted when the repo was set up. This issue verifies it against the solution from E1-1 and removes what is still BörsMönster-specific.

## Goals
- Every file under `.claude/` and `CLAUDE.md` refers to solid-backup: its repo, project board 6, its labels and its architecture.
- `set-issue-status.sh` moves issues on board 6.
- The canonical check is documented in `CLAUDE.md` and matches the CI workflow from E1-1.

## Non-goals
- Changing the workflow itself (steps, stops, merge rules).
- New skills or scripts.
- Changing the constitution.

## Acceptance Criteria
1. **`/spec-from-issue` can be run on an issue in this repo.**
   - The specs in `specs/` for E1-1 to E1-4 were produced with `/spec-from-issue` (batch mode, through `/ship-epic E1`).
   - `.claude/scripts/set-issue-status.sh` points at `OWNER=bjornpetersson1` and `PROJECT=6`, and moving an issue with it succeeds (e.g. this issue to In Progress during `/ship-issue`).
2. **The canonical check `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` is documented in `CLAUDE.md`.**
   - The command appears verbatim under Verification in `CLAUDE.md`.
   - The steps in `.github/workflows/ci.yml` (E1-1) are the same checks, so `CLAUDE.md`'s statement "the same steps as CI" is true.
3. **No BörsMönster leftovers.**
   - `grep -rniE "börs|bors|market data|stock|alpha vantage|sqlite|price|omxs" .claude CLAUDE.md` and `grep -rnwE "SMA|EMA|RSI|OHLC" .claude CLAUDE.md` find nothing that describes BörsMönster (examples use solid-backup's labels and keys, e.g. `E2 Audio`; the `clean-code` checklist names solid-backup's domain terms).
   - The architecture checks in `review-pr` and `release-check` name solid-backup's constraints (`IAudioInput`/`IAudioOutput`, no allocation, locks or I/O in the audio callback, switchable level features, local only).

## Tasks
- [ ] Bring the existing commit that points `set-issue-status.sh` at board 6 into this issue's PR.
- [ ] Replace the BörsMönster example label (`E2 Market data`) in `ship-epic` with a solid-backup one.
- [ ] Replace the BörsMönster domain terms (SMA, EMA, RSI, OHLC, OMXS30, prices) in the `clean-code` checklist with solid-backup's.
- [ ] Check the rest of `.claude/` and `CLAUDE.md` with the grep above; fix any leftovers.
- [ ] Confirm `CLAUDE.md`'s canonical check matches `ci.yml` from E1-1.

## Decisions
- **Branch** (default): the branch `feature/E1-2-claude-workflow` already exists, is pushed, and is `origin/develop` plus one commit (board 6). The issue continues on it instead of creating a new branch, so that commit is kept. `origin/develop` is merged into it (not rebased), since the branch is already pushed and history is never rewritten.
- **Proof for criterion 1** (default): running this epic is the proof; no test is written for a skill.
- **No questions asked:** the issue has no open questions, and its scope is a verification of files that already exist.
- **Wider leftover check** (spec update before implementing): verifying E1-1 showed that the `clean-code` checklist still names BörsMönster's domain terms and prices, which the original grep missed. The grep in criterion 3 now covers them; the criterion's intent is unchanged.

## Open Questions
- None
