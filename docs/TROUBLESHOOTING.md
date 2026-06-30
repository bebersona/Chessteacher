# Troubleshooting

## Stockfish is missing

Run `tools\Download-Stockfish.ps1`, then publish again. In a published build,
confirm that `engines\stockfish\stockfish.exe` exists beside ChessTeacher.exe.

## Windows blocks the installer

Unsigned development installers can trigger SmartScreen. For public
distribution, sign the installer and executable with a trusted code-signing
certificate. Do not disable Windows security globally.

## Database error

ChessTeacher stores its SQLite database under `%LocalAppData%\ChessTeacher`.
Check folder permissions. Rename the database only after making a backup.

## Engine timeout or crash

Stop analysis, then retry. `StockfishService` resets the child process after an
I/O failure. Logs are written to the local application data folder in a future
logging sink milestone; console/trace logging is active in this source build.

## Build fails before compilation

Confirm `dotnet --info` reports SDK 10.0.301 or a compatible later 10.0 patch.
Run `dotnet restore ChessTeacher.sln` and read the first error rather than the
final summary.
