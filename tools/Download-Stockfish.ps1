[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot "..\engines\stockfish"),
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$target = Join-Path $destinationPath "stockfish.exe"

if ((Test-Path $target) -and -not $Force) {
    Write-Host "Stockfish already exists at $target"
    exit 0
}

New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("ChessTeacher-Stockfish-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $temp | Out-Null

try {
    $zip = Join-Path $temp "stockfish.zip"
    $url = "https://github.com/official-stockfish/Stockfish/releases/latest/download/stockfish-windows-x86-64.zip"
    Write-Host "Downloading the official Stockfish Windows x64 archive..."
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath (Join-Path $temp "expanded") -Force

    $candidate = Get-ChildItem (Join-Path $temp "expanded") -Recurse -File |
        Where-Object { $_.Name -match '^stockfish.*\.exe$' } |
        Select-Object -First 1

    if (-not $candidate) {
        throw "The downloaded archive did not contain a Stockfish executable."
    }

    Copy-Item $candidate.FullName $target -Force

    Get-ChildItem (Join-Path $temp "expanded") -Recurse -File |
        Where-Object { $_.Name -match '^(Copying|LICENSE|README)(\..*)?$' } |
        ForEach-Object {
            Copy-Item $_.FullName (Join-Path $destinationPath ("UPSTREAM_" + $_.Name)) -Force
        }

    $sourceUrl = "https://github.com/official-stockfish/Stockfish/archive/refs/tags/sf_18.zip"
    $sourceArchive = Join-Path $destinationPath "stockfish-18-corresponding-source.zip"
    Write-Host "Downloading the corresponding Stockfish 18 source archive for GPL distribution..."
    Invoke-WebRequest -Uri $sourceUrl -OutFile $sourceArchive -UseBasicParsing

    Set-Content -Path (Join-Path $destinationPath "DOWNLOAD_SOURCE.txt") -Encoding UTF8 -Value @(
        "Official Windows x64 binary archive:"
        $url
        "Corresponding Stockfish 18 source archive:"
        $sourceUrl
        ("Downloaded UTC: " + [DateTime]::UtcNow.ToString("O"))
        "Neither the executable nor the source archive was opened or launched by this script."
    )

    Write-Host "Stockfish was placed at $target"
    Write-Host "Corresponding source was placed at $sourceArchive"
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
