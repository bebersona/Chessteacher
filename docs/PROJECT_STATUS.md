# Project status

## Implemented in this source milestone

- Multi-project C# solution using .NET 10 and Avalonia
- MVVM board UI with dark theme and navigation shell
- Click-to-move, legal highlights, last-move and check highlights
- Legal move generation independent of Stockfish
- Castling, en passant, promotion, checkmate, stalemate
- Threefold, fifty-move and insufficient-material checks
- FEN parsing/generation, SAN move history and basic PGN import/export
- Undo and redo
- Asynchronous, cancellable Stockfish UCI service with one child process
- MultiPV parsing, evaluation, best move and principal variation
- Computer move, hint and current-position analysis controls
- SQLite schema and saved-game repository
- JSON settings plus sample lessons, puzzles and openings
- Original move-classification thresholds, ACPL and basic explanations
- Unit-test project, self-contained publish script and Inno Setup script
- GitHub Actions workflow that builds the Windows installer without running it

## Intentionally staged for later milestones

The navigation shell contains entries for puzzles, lessons, openings, progress
and settings, but the complete interactive workflows and charts for every
advanced requirement are not yet implemented. Drag-and-drop, clocks, full
post-game batch review, advanced tactical detection, audio, accessibility
polish, database migrations and full PGN variations also need expansion.

This file prevents the source milestone from being mistaken for completion of
every phase in the original specification.
