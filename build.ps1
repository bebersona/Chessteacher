[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw ".NET SDK 10 is required to build the source project."
    }

    dotnet --info
    dotnet restore ChessTeacher.sln
    dotnet build ChessTeacher.sln -c $Configuration --no-restore
    dotnet test src\ChessTeacher.Tests\ChessTeacher.Tests.csproj -c $Configuration --no-build
}
finally {
    Pop-Location
}
