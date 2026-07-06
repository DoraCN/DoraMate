@echo off
title DoraMate v0.10.0
echo === DoraMate v0.10.0 ===
echo.
echo Starting LocalAgent...
start /B "" "%~dp0doramate-localagent.exe"
echo Waiting for LocalAgent...
:wait
timeout /t 1 /nobreak >nul
netstat -an 2>nul | findstr ":52100 " >nul
if errorlevel 1 goto wait
echo.
echo DoraMate is ready at http://127.0.0.1:52100
start http://127.0.0.1:52100
echo.
echo Press any key to stop DoraMate...
pause >nul
echo Stopping...
taskkill /F /IM doramate-localagent.exe >nul 2>&1
echo Done.
pause
