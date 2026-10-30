# E1-1 · Repo and skeleton

- **Issue:** [#1](https://github.com/bjornpetersson1/solid-backup/issues/1)
- **Labels:** `E1 Foundation`
- **Phase:** 1. Foundation
- **Depends on:** none
- **Status:** Approved

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
   - `.github/workflows/ci.yml` triggers on `pull_request` and `push` for `develop` and `main`.
   - Steps: `dotnet restore`, `dotnet format --verify-no-changes --no-restore`, `dotnet build -warnaserror --no-restore`, `dotnet test --no-build`, on `ubuntu-latest` with .NET 8.
   - The workflow runs and passes on this issue's PR (visible in the PR's checks).

## Tasks
- [ ] Create `SolidBackup.sln`, `src/SolidBackup` (console) and `tests/SolidBackup.Tests` (xUnit) and add them to the solution.
- [ ] Add `Directory.Build.props` with the shared settings; strip the duplicates from the project files.
- [ ] Change `.editorconfig` to `lf` line endings and a final newline for C#.
- [ ] Add one smoke test so `dotnet test` exercises the app assembly.
- [ ] Add `.github/workflows/ci.yml`.
- [ ] Run the canonical checks locally.

## Decisions
- **CI runner: `ubuntu-latest`.** Tests need no audio hardware, and macOS minutes cost ten times as much in a private repo. Revisit in E2 if the PortAudio binding needs macOS in CI. (Owner's choice.)
- **Line endings: `lf` with a final newline.** The copied `.editorconfig` said `crlf` and no final newline, which is unnatural on macOS and makes `dotnet format` fight the editor. (Owner's choice; departs from the copied file.)
- **Analyzers as in BörsMönster:** `Nullable`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and the .NET 8 SDK's default analyzers; no `AnalysisLevel` change and no extra analyzer packages. (Owner's choice.)
- **No `global.json`** (default): BörsMönster has none, and CI pins `8.0.x` through `actions/setup-dotnet`.
- **Smoke test** (default): a trivial test that touches the app assembly, so `dotnet test` proves the reference works; it is replaced by real tests as features arrive.
- **Repo, branches, GitHub repo and board** already exist (per the issue's background), so they are not tasks here.

## Open Questions
- None
