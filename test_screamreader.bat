@echo off
echo Testing ScreamReader with your command...
echo.
echo Command: ScreamReader.exe --unicast --port 4011 --bit-width 32 --rate 48000
echo.
echo Expected output should show:
echo - Audio mode: Shared
echo - Mixer visibility: Visible
echo.
pause
ScreamReader.exe --unicast --port 4011 --bit-width 32 --rate 48000