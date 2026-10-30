---
name: clean-code
description: Clean-code pass over the uncommitted changes (or the branch diff against develop, or against another base) without changing behaviour. Use for /clean-code, as step 3 of /ship-issue, after fixing PR review comments, and with `base origin/main` in /release-check.
argument-hint: <optional path, KEY, or "base <ref>">
---

# Clean-code pass

Scope: the changes on this branch — `git diff origin/develop...` plus uncommitted and untracked files (`git status --porcelain`). With the argument `base <ref>` (e.g. `base origin/main` for a release), the scope is `git diff <ref>...` instead. Do not touch code outside that diff. Behaviour must not change.

## 1. Built-in simplification

Stage everything so new files are part of the diff (`git add -A`), then invoke the built-in `simplify` skill and let it apply its fixes.

## 2. Project checklist

Then go through the changed files once more against this list and fix what you find:

- **Names** say what things are; no abbreviations other than domain terms (SMA, EMA, RSI, OHLC, OMXS30). Follow .NET naming: PascalCase for types and members, `I` prefix for interfaces, `Async` suffix for async methods.
- **Functions** do one thing; split anything that needs a comment to explain its sections. Prefer early returns over nesting.
- **No duplication** — reuse existing helpers and types in the repo before adding new ones.
- **Types**: no `dynamic` or `object` where a type exists, no null-forgiving `!` to silence the compiler, no unnecessary casts; prefer records for data; shared shapes defined once. Use `decimal` or `double` consistently for prices, not a mix.
- **Boundaries**: audio only through `IAudioInput` and `IAudioOutput`; no allocation, locks or I/O in the audio callback; each level feature a separate, switchable component; config only through the config loader; no magic numbers in code (thresholds, ranges, buffer sizes, targets come from config).
- **Async**: no `.Result` or `.Wait()`; pass `CancellationToken` through I/O calls.
- **Errors** are handled where they can be acted on, with a useful message; no swallowed errors.
- **Dead weight**: no unused exports, imports, files, debug logging or commented-out code.
- **Comments** explain *why*, not *what*; delete ones that restate the code.
- **Tests** are readable: clear names, arrange/act/assert, no logic copied from the implementation.

## 3. Check and report

Run `dotnet format` and `dotnet build -warnaserror` once at the end, plus the tests for the code you changed (`dotnet test --filter <class or namespace>`). Do not run the full suite. Summarise in a few lines what you changed and why. Leave changes uncommitted.
