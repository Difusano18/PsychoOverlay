@echo off
setlocal
cd /d "%~dp0"
if exist "%~dp0PsychoOverlay.exe" (
    start "" "%~dp0PsychoOverlay.exe"
) else if exist "%~dp0PsychoOverlay.csproj" (
    dotnet run --project "%~dp0PsychoOverlay.csproj" -c Release
) else (
    echo PsychoOverlay.exe and PsychoOverlay.csproj were not found.
    pause
)
endlocal
