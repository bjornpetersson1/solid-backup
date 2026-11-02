# E1-4 · CLI and menu skeleton

- **Issue:** [#4](https://github.com/bjornpetersson1/solid-backup/issues/4)
- **Labels:** `E1 Foundation`
- **Phase:** 1. Foundation
- **Depends on:** E1-1 (#1)
- **Status:** Implemented — all acceptance criteria verified (format, build with 0 warnings, 49 tests passed); Ctrl+C at the menu is a manual check for the owner

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
- [x] Add `System.CommandLine` and `Spectre.Console` to the app, and `Spectre.Console.Testing` to the tests.
- [x] Root command with a Swedish description; built-in `--help` and `--version`.
- [x] The root command's action with no arguments runs the menu.
- [x] Menu with the title and the `Avsluta` choice, taking an `IAnsiConsole` so tests can drive it.
- [x] Version `0.1.0` in the project.
- [x] Tests for the criteria above.

## Decisions
- **`--help` text:** the app's and options' descriptions are Swedish; the library's own headings (`Usage:`, `Options:`) stay English. No custom help layout to maintain. (Owner's choice; a limited exception to "console text is Swedish".)
- **Menu:** a `solid-backup` title and a `SelectionPrompt` with only `Avsluta`; E6-2 adds the real choices. (Owner's choice.)
- **`System.CommandLine` version** (default): the latest stable 2.0.x release that supports `net8.0`, not the old 2.0 betas.
- **Testable console** (default): the menu takes an `IAnsiConsole`, so tests use `TestConsole` instead of a real terminal.
- **Version** (default): `0.1.0`, set once in the project and shown by `--version`.
- **Packages** (implementation): `System.CommandLine` 2.0.14 (stable), `Spectre.Console` 0.57.2, and `Spectre.Console.Testing` 0.57.2 in the tests.
- **Own `--version` action** (implementation): the built-in one prints the entry assembly's version, which is the test host under `dotnet test`; the app prints its own assembly's informational version, with the source revision suffix turned off so it reads `0.1.0`.
- **Non-interactive terminal** (implementation): started without arguments and without an interactive terminal (e.g. input from a pipe), the menu prints "Menyn kräver en interaktiv terminal." and exits with 1 instead of a stack trace. Not in the spec's criteria, but within the menu's scope and in line with "no stack trace".
- **Structure** (implementation): `Cli/AppCommand.Run` parses and runs, `Cli/MainMenu` takes an `IAnsiConsole`, and `Program.Main` only wires them to the real console, which is created lazily so `--help` and `--version` never probe the terminal. (Code review.)
- **Swedish parse and runtime errors** (code review): System.CommandLine has no Swedish resources, so the app prints its own messages: "Okänt argument: … Kör med --help för att se vad som finns." for parse errors, and "Ett oväntat fel inträffade: …" for an exception in the menu, both with exit code 1 and no stack trace. Only the `--help` headings stay English.
- **Interactive check is explicit** (code review): Spectre treats every macOS session as interactive, so `Program` turns interaction off when input is redirected.
- **Ctrl+C restores the cursor** (code review): the prompt hides the cursor and SIGINT skips its cleanup, so a `CancelKeyPress` handler shows it again. Confirming this in a real terminal stays a manual check for the owner.

- **Superseded by E6-2:** the menu is no longer empty; it shows the current choices, one item per choice, `Starta live` (preselected) and `Avsluta` (`specs/E6-2-menu.md`).

## Open Questions
- None
