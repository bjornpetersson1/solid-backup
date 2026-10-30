# E1-4 · CLI and menu skeleton

- **Issue:** [#4](https://github.com/bjornpetersson1/solid-backup/issues/4)
- **Labels:** `E1 Foundation`
- **Phase:** 1. Foundation
- **Depends on:** E1-1 (#1)
- **Status:** Approved

## Summary
The app's entry point: `System.CommandLine` handles `--help` and `--version`, and starting without arguments shows an empty `Spectre.Console` menu with a single quit choice, ready for E6-2 to fill.

## Goals
- `--help` and `--version` through `System.CommandLine`.
- An empty menu with a title and an `Avsluta` choice when started without arguments.
- Menu code that can be tested without a real terminal.

## Non-goals
- Menu content: devices, key, sound, rhythm, level features, recording (E6-2).
- Loading or saving settings at startup (E1-3 provides the loader; E6-2 wires it in).
- Live mode, the dashboard, `Esc`/`q` handling (E6-x).
- Other flags or subcommands.

## Acceptance Criteria
1. **`--help` works.**
   - `dotnet run --project src/SolidBackup -- --help` exits 0 and prints the app's description and the `--help` and `--version` options, with the app's own descriptions in Swedish.
   - A test invokes the command line with `--help` and checks the exit code and that the Swedish description is in the output.
2. **`--version` works.**
   - `--version` exits 0 and prints the app's version (`0.1.0` from the project), checked by a test.
3. **Starting without arguments shows the menu.**
   - The menu shows the title `solid-backup` and a selection prompt whose only choice is `Avsluta`.
   - Choosing `Avsluta` exits with code 0.
   - A test drives the menu with `Spectre.Console.Testing`'s `TestConsole`: it sees the title and the choice, selects `Avsluta`, and gets exit code 0.
   - `Ctrl+C` at the menu ends the app without a stack trace (manual check for the owner).
4. **Unknown arguments fail cleanly.**
   - An unknown option exits with a non-zero code and an error message, with no stack trace (a test).
5. **Checks pass:** `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test`.

## Tasks
- [ ] Add `System.CommandLine` and `Spectre.Console` to the app, and `Spectre.Console.Testing` to the tests.
- [ ] Root command with a Swedish description; built-in `--help` and `--version`.
- [ ] The root command's action with no arguments runs the menu.
- [ ] Menu with the title and the `Avsluta` choice, taking an `IAnsiConsole` so tests can drive it.
- [ ] Version `0.1.0` in the project.
- [ ] Tests for the criteria above.

## Decisions
- **`--help` text:** the app's and options' descriptions are Swedish; the library's own headings (`Usage:`, `Options:`) stay English. No custom help layout to maintain. (Owner's choice; a limited exception to "console text is Swedish".)
- **Menu:** a `solid-backup` title and a `SelectionPrompt` with only `Avsluta`; E6-2 adds the real choices. (Owner's choice.)
- **`System.CommandLine` version** (default): the latest stable 2.0.x release that supports `net8.0`, not the old 2.0 betas.
- **Testable console** (default): the menu takes an `IAnsiConsole`, so tests use `TestConsole` instead of a real terminal.
- **Version** (default): `0.1.0`, set once in the project and shown by `--version`.

## Open Questions
- None
