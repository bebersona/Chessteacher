[CmdletBinding()]
param(
    [switch]$DownloadStockfish,
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw ".NET SDK 10 is required to publish ChessTeacher."
    }

    $engine = Join-Path $PSScriptRoot "engines\stockfish\stockfish.exe"
    if (-not (Test-Path $engine)) {
        if ($DownloadStockfish) {
            & (Join-Path $PSScriptRoot "tools\Download-Stockfish.ps1")
        } else {
            throw "Stockfish is missing. Run tools\Download-Stockfish.ps1 or pass -DownloadStockfish."
        }
    }

    & (Join-Path $PSScriptRoot "build.ps1") -Configuration Release

    $publish = Join-Path $PSScriptRoot "publish\win-x64"
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

    dotnet publish src\ChessTeacher.App\ChessTeacher.App.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        --no-restore `
        -o $publish

    Copy-Item README.md, LICENSE.txt, THIRD_PARTY_NOTICES.md $publish -Force
    Copy-Item docs (Join-Path $publish "docs") -Recurse -Force

    if ($BuildInstaller) {
        $iscc = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1

        if (-not $iscc) {
            throw "Inno Setup 6 was not found. Install it, then rerun with -BuildInstaller."
        }

        & $iscc installer\ChessTeacher.iss
    }

    Write-Host "Published application: $publish"
}
finally {
    Pop-Location
}
