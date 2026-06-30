@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Configuration Release
if errorlevel 1 (
  echo.
  echo Build failed. Read the message above.
  exit /b 1
)
echo.
echo Build and tests completed.
