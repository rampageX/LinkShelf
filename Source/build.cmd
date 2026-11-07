@echo off
cd /d "%~dp0"
dotnet publish LinkShelf.csproj -c Release -r win-x64 --self-contained false -o ..\Build
pause
