@echo off
setlocal
cd /d "%~dp0"
if not exist "LinkShelfMover.Tests.exe" (
  echo Cannot find LinkShelfMover.Tests.exe. Run this script from the unpacked LinkShelf package Tests folder.
  pause
  exit /b 2
)
"%~dp0LinkShelfMover.Tests.exe"
set "RESULT=%ERRORLEVEL%"
echo.
if "%RESULT%"=="0" (echo ALL TESTS PASSED.) else (echo TESTS FAILED: %RESULT%)
pause
exit /b %RESULT%
