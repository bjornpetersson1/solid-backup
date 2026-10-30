---
name: run-test-and-check-spec
description: Run restore, format check, build (warnings as errors) and tests, then prove every acceptance criterion of a spec and fix anything failing in a loop. Use for /run-test-and-check-spec <KEY> or as step 5 of /ship-issue and in /address-pr-review.
argument-hint: <spec key, e.g. E1-1 — optional>
---

# Run the checks, then prove the spec

Spec: $ARGUMENTS — if none was given, infer it from the branch name (`feature/<KEY>-…`) and say which one you picked.

## 1. Read the spec, and the issue

Read the spec and `gh issue view <N> --json body` — the issue's acceptance criteria must hold too, not only the spec's.

## 2. Run everything in one go

From the repo root, in the background, reading the output file (do not pipe through `tail`):

```bash
set -e
echo "== restore =="; dotnet restore
echo "== format =="; dotnet format --verify-no-changes --no-restore
echo "== build =="; dotnet build -warnaserror --no-restore
echo "== test =="; dotnet test --no-build
echo "ALL GREEN"
```

If tests skip themselves, say so and treat the dependent criteria as unverified, not passed. Tests need no audio hardware and the WAV corpus is in the repo, so a test that skips for a missing device or corpus file is a FAIL to fix, not a skip.

## 3. Walk the criteria

For each acceptance criterion in the spec, and each "Done when" item in the issue, record **PASS** or **FAIL** with the evidence: the command and its result, a named test, a line of output. Where a criterion can only be checked by running the app or reading code, do exactly that and say so. A criterion with no evidence is **not verified**.

Also confirm:
- `git status` shows no files that should not be committed (user settings file, recordings, build output, logs).
- No warnings from format, build or tests about anything the spec covers — a warning is a FAIL.

## 4. Fix, re-run, repeat

On a FAIL: fix the cause, re-run only the affected check, re-check the criterion. Run the full suite once more at the end, when everything is believed green. **Cap:** the same criterion failing three times → stop and report what blocks it. Never weaken a criterion or skip/delete a test to get to green.

## 5. Update the spec and report

- Tick genuinely done Tasks; move anything settled into Decisions.
- Set **Status** to the truth with numbers, e.g. `Implemented — all acceptance criteria verified (format, build with 0 warnings, 42 tests passed)`, or say what is still failing.
- Report each criterion with PASS/FAIL and evidence, and anything unverified or deliberately not fixed. Leave changes uncommitted.
