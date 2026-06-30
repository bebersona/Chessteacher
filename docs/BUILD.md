# Build and release

## Local Windows build

Requirements:

1. Windows x64
2. .NET SDK 10
3. Internet access for NuGet restore and the official Stockfish archive
4. Inno Setup 6 only when producing `ChessTeacherSetup.exe`

From PowerShell:

```powershell
.\tools\Download-Stockfish.ps1
.\build.ps1
.\publish.ps1 -BuildInstaller
```

Or double-click `publish-windows.cmd`.

The release is self-contained; the final user does not need a separately
installed .NET runtime. Stockfish and data assets remain beside the executable.

## GitHub build without local developer tools

Push the folder to a GitHub repository, open **Actions**, choose
**Build Windows installer**, and run the workflow. It uploads:

- `ChessTeacher-Windows-x64`
- `ChessTeacherSetup` containing `ChessTeacherSetup.exe`

The workflow builds and tests but never launches ChessTeacher.
