# ChessTeacher

ChessTeacher is an offline-first Windows chess training desktop application
built with C#, .NET 10, Avalonia UI, Stockfish over UCI, SQLite and MVVM.

## First working milestone

The source includes a playable legal chessboard, free-board mode, optional
Stockfish opponent, current-position analysis, hints, evaluation lines,
teaching feedback, FEN/PGN foundations, saved games and local settings.

Read `docs/PROJECT_STATUS.md` before treating this as the final implementation
of every advanced feature in the full specification.

## Build a directly installable Windows app

On a Windows development machine:

```powershell
.\publish-windows.cmd
```

This downloads the official Stockfish binary without launching it, builds and
tests the solution, publishes a self-contained Windows x64 app, and creates:

```text
installer\Output\ChessTeacherSetup.exe
```

The installer has no automatic launch step.

A GitHub Actions workflow is also included for users who do not want to install
developer tools locally. See `docs/BUILD.md`.

## Safety and stability decisions

- No Python, Node.js, browser, local web server or Electron runtime
- Stockfish starts only after an engine action is requested
- Analysis is asynchronous and cancellable
- One Stockfish process is reused and disposed on exit
- Engine defaults: 2 threads, 128 MB hash, MultiPV 3, depth 16, 1 second
- User data lives under `%LocalAppData%\ChessTeacher`
- Installer preserves user data during uninstall/update
- No application file is automatically run by the build or installer scripts
