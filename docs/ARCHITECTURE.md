# Architecture

ChessTeacher is split into six projects:

- **ChessTeacher.App** — Avalonia views, view models, commands and dependency injection.
- **ChessTeacher.Core** — board representation, legal move generation, FEN, SAN and PGN.
- **ChessTeacher.Engine** — one managed Stockfish child process and asynchronous UCI parsing.
- **ChessTeacher.Data** — SQLite initialization, game repository, settings and user-data paths.
- **ChessTeacher.Teaching** — original move classification, ACPL and rule-based explanations.
- **ChessTeacher.Tests** — core, parser and teaching tests.

The UI does not ask Stockfish whether a move is legal. `ChessGame` owns chess
rules. `StockfishService` owns the process and serializes analysis with a
semaphore so unnecessary parallel engine instances are not created.

User-changing data is stored under `%LocalAppData%\ChessTeacher`, never under
Program Files. Stockfish remains beside the published executable as a separate
component.
