@echo off
setlocal

for /f "tokens=2 delims=<>" %%A in ('findstr "<Version>" "LemiCraft Launcher.csproj"') do set VERSION=%%A

echo.
echo ========================================
echo  LemiCraft Launcher %VERSION% - Build
echo ========================================
echo.

set DIST=publish\dist
if not exist "%DIST%" mkdir "%DIST%"

echo [1/2] Portable...
dotnet publish "LemiCraft Launcher.csproj" -p:PublishProfile=Portable
if errorlevel 1 ( echo FAILED & exit /b 1 )
copy /Y "publish\portable\LemiCraft_Launcher.exe" "%DIST%\LemiCraft_Launcher.exe" >nul

echo.
echo [2/2] Installer...
dotnet publish "LemiCraft Launcher.csproj" -p:PublishProfile=Installer
if errorlevel 1 ( echo FAILED & exit /b 1 )
copy /Y "publish\installer\LemiCraft_Installer.exe" "%DIST%\LemiCraft_Installer.exe" >nul

echo.
echo ========================================
echo  Done! Files in publish\dist\
echo   LemiCraft_Launcher.exe
echo   LemiCraft_Installer.exe
echo ========================================
echo.
pause
