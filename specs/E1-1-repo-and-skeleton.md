# E1-1 · Repo and skeleton

- **Issue:** [#1](https://github.com/bjornpetersson1/solid-backup/issues/1)
- **Labels:** `E1 Foundation`
- **Phase:** 1. Foundation
- **Depends on:** none
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 1 test passed; CI passed on #23)

## Summary
A .NET 8 solution with a console app project and an xUnit test project, shared build settings, and a GitHub Actions workflow that runs the canonical checks on every PR and on pushes to `develop` and `main`.

## Goals
- `SolidBackup.sln` at the repo root with `src/SolidBackup` and `tests/SolidBackup.Tests`.
- Shared build settings in `Directory.Build.props`, as in BörsMönster.
- CI that runs restore, format check, build with warnings as errors, and tests.

## Non-goals
- Any app logic: CLI flags, menu, configuration, audio (E1-3, E1-4, E2-x).
- NuGet packages beyond what the templates need (`System.CommandLine`, `Spectre.Console`, PortAudio come with their issues).
- Central package management, code coverage, release builds or publishing.
- A macOS CI runner.

## Acceptance Criteria
1. **`dotnet build` and `dotnet test` run from the repo root.**
   - `SolidBackup.sln` contains `src/SolidBackup/SolidBackup.csproj` (console app, `net8.0`) and `tests/SolidBackup.Tests/SolidBackup.Tests.csproj` (xUnit, references the app project).
   - `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test` exits 0 from the repo root.
   - `dotnet test` runs at least one test, and it passes.
2. **Shared build settings.**
   - `Directory.Build.props` sets `net8.0`, `ImplicitUsings`, `Nullable` enabled, `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`; the project files do not repeat these.
   - `.editorconfig` uses `end_of_line = lf` and `insert_final_newline = true` for C#; every committed `.cs` file follows it (checked by `dotnet format --verify-no-changes`).
3. **A GitHub Actions workflow runs them on every PR.**
   - `.github/workflows/ci.yml` triggers on every `pull_request` and on `push` to `develop` and `main`, with a read-only token.
   - Steps: `dotnet restore`, `dotnet format --verify-no-changes --no-restore`, `dotnet build -warnaserror --no-restore`, `dotnet test --no-build`, on `ubuntu-latest` with the .NET 8 SDK pinned in `global.json`.
   - The workflow runs and passes on this issue's PR (visible in the PR's checks).

## Tasks
- [x] Create `SolidBackup.sln`, `src/SolidBackup` (console) and `tests/SolidBackup.Tests` (xUnit) and add them to the solution.
- [x] Add `Directory.Build.props` with the shared settings; strip the duplicates from the project files.
- [x] Change `.editorconfig` to `lf` line endings and a final newline for C#.
- [x] Add one smoke test so `dotnet test` exercises the app assembly.
- [x] Add `.github/workflows/ci.yml`.
- [x] Run the canonical checks locally.

## Decisions
- **CI runner: `ubuntu-latest`.** Tests need no audio hardware, and macOS minutes cost ten times as much in a private repo. Revisit in E2 if the PortAudio binding needs macOS in CI. (Owner's choice.)
- **Line endings: `lf` with a final newline.** The copied `.editorconfig` said `crlf` and no final newline, which is unnatural on macOS and makes `dotnet format` fight the editor. (Owner's choice; departs from the copied file.)
- **Analyzers as in BörsMönster:** `Nullable`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and the .NET 8 SDK's default analyzers; no `AnalysisLevel` change and no extra analyzer packages. (Owner's choice.)
- **`global.json` pins SDK `8.0.402` (`latestPatch`)**, and CI installs it with `global-json-file`, so `dotnet format` and the style analyzers are the same locally and in CI. (Code review; replaces the earlier default of no `global.json`.)
- **Smoke test** (default): a trivial test that touches the app assembly, so `dotnet test` proves the reference works; it is replaced by real tests as features arrive.
- *Superseded by E1-4 and the E1 release check:* `Program.Main(string[] args)` now runs the command line, and the smoke test was removed once real tests covered the app assembly.
- **Explicit `Program` class** (implementation): `public static class Program` with a parameterless `Main` returning `0`; E1-4 replaces the body. The smoke test only checks the assembly reference through `typeof(Program)` and never runs `Main`, so it will not start the menu once E1-4 lands. (Code review.)
- **No `coverlet.collector`** (implementation): removed from the xUnit template, since coverage is a non-goal.
- **`.editorconfig` keeps its own CRLF line endings** (implementation): only the two C# settings changed, to keep the diff minimal.
- **Current test packages** (code review): `Microsoft.NET.Test.Sdk` 18.11.0, `xunit` 2.9.3, `xunit.runner.visualstudio` 4.0.1 instead of the template's old versions, so NuGet audit warnings (errors under `TreatWarningsAsErrors`) do not turn CI red without a code change.
- **CI hardening** (code review): `permissions: contents: read`, and `pull_request` without a base-branch filter so every PR is checked.
- **`.gitattributes` with `*.cs text eol=lf`** (code review): checkout never converts C# files to CRLF, whatever `core.autocrlf` says.
- **Repo, branches, GitHub repo and board** already exist (per the issue's background), so they are not tasks here.

## Open Questions
- None
