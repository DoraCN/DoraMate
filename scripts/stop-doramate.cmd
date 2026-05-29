@echo off
echo Stopping DoraMate...
taskkill /F /IM doramate-localagent.exe >nul 2>&1
taskkill /F /IM dora.exe >nul 2>&1
echo DoraMate stopped.
pause
