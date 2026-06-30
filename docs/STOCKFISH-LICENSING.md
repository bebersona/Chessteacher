# Stockfish distribution checklist

Stockfish is GPLv3 software and is a separate executable component.

Before redistributing an installer that contains Stockfish:

1. Record the exact Stockfish version and binary package.
2. Include upstream copyright and GPLv3 licence text.
3. Provide the complete corresponding source code, or a compliant source
   location/offer that remains available for the required period.
4. Preserve modification notices if the engine is modified.
5. State clearly that Stockfish is separate from ChessTeacher.
6. Do not describe Stockfish as proprietary ChessTeacher code.

The download script records the official release endpoint and copies upstream
licence/readme files when present. The distributor remains responsible for
reviewing the final package for GPL compliance.

## Included release behavior

The download script uses the official generic Windows x64 binary endpoint and also downloads the exact `sf_18` corresponding-source ZIP. Both are copied into the published distribution; the engine is never started during download, build, or installation.
