Stockfish is not checked into this source archive.

Run tools\Download-Stockfish.ps1 on Windows, or use the included GitHub Actions
workflow. The script downloads the official Windows x64 Stockfish release,
copies stockfish.exe into this directory, and preserves the upstream licence
files. The script does not start the engine.

The application looks for:
  engines\stockfish\stockfish.exe
beside ChessTeacher.exe after publishing.
