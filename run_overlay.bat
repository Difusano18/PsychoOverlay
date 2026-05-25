@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "%~dp0PsychoOverlay.csproj" -c Release
endlocal
