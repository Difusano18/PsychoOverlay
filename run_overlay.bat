@echo off
setlocal
cd /d "%~dp0"

REM Always run the current source when the project is present.
REM The checked-in PsychoOverlay.exe can be older than Program.cs after a git pull.
if exist "%~dp0PsychoOverlay.csproj" (
    dotnet run --project "%~dp0PsychoOverlay.csproj" -c Release
) else if exist "%~dp0PsychoOverlay.exe" (
    start "" "%~dp0PsychoOverlay.exe"
) else (
    echo PsychoOverlay.csproj and PsychoOverlay.exe were not found.
    pause
)

endlocal
