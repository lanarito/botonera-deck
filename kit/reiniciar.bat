@echo off
taskkill /im Botonera.exe /f >nul 2>nul
timeout /t 1 >nul
start "" "%~dp0Botonera.exe"
