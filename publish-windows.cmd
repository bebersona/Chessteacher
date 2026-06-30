@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -DownloadStockfish -BuildInstaller
if errorlevel 1 (
  echo.
  echo Publishing failed. Read the message above.
  exit /b 1
)
echo.
echo Publish and installer steps completed.
