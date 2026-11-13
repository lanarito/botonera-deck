@echo off
title Botonera Deck - Desinstalar
taskkill /im Botonera.exe /f >nul 2>nul
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Botonera.lnk" >nul 2>nul
del "%USERPROFILE%\Desktop\Botonera - *.lnk" >nul 2>nul
echo Botonera quitada del inicio de Windows y del Escritorio.
echo La carpeta C:\BotoneraDeck queda (borrala a mano si queres).
pause
