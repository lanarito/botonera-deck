# Botonera Deck - instalador (lo llama INSTALAR.bat). No necesita administrador.
# Copia la botonera a C:\BotoneraDeck, baja la herramienta USB de Google (adb) y la app de pantalla completa
# para la tablet, la deja arrancando con Windows y crea los accesos directos dentro de C:\BotoneraDeck.
$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot 'BotoneraDeck'
$dst = 'C:\BotoneraDeck'
function Paso($t) { Write-Host ''; Write-Host ">> $t" -ForegroundColor Cyan }
try {
  Write-Host '==============================================' -ForegroundColor Green
  Write-Host '   BOTONERA DECK - instalacion' -ForegroundColor Green
  Write-Host '==============================================' -ForegroundColor Green

  Paso 'Copiando la botonera a C:\BotoneraDeck ...'
  Get-Process Botonera -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Milliseconds 500
  New-Item -ItemType Directory -Force -Path $dst | Out-Null
  $teniaBotones = Test-Path (Join-Path $dst 'botones.json')
  Copy-Item (Join-Path $src '*') $dst -Recurse -Force -Exclude 'botones.json'
  if ($teniaBotones) {
    Copy-Item (Join-Path $dst 'botones.json') (Join-Path $dst 'botones_anterior.json') -Force
    Write-Host '   (tu botones.json anterior quedo guardado como botones_anterior.json)'
  }
  Copy-Item (Join-Path $src 'botones.json') $dst -Force
  Copy-Item (Join-Path $PSScriptRoot 'asignar_teclas_AC.ps1') $dst -Force
  Copy-Item (Join-Path $PSScriptRoot 'LEEME - Como instalar y configurar.txt') $dst -Force

  $pt = Join-Path $dst 'tablet\platform-tools\adb.exe'
  if (-not (Test-Path $pt)) {
    Paso 'Bajando la herramienta USB oficial de Google (adb) ...'
    $zip = Join-Path $env:TEMP 'platform-tools.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' -OutFile $zip -UseBasicParsing
    Expand-Archive $zip -DestinationPath (Join-Path $dst 'tablet') -Force
    Remove-Item $zip
  } else { Paso 'adb ya estaba bajado.' }

  $apk = Join-Path $dst 'tablet\webviewkiosk.apk'
  if (-not (Test-Path $apk)) {
    Paso 'Bajando la app de pantalla completa para la tablet (Webview Kiosk, gratis, de F-Droid) ...'
    Invoke-WebRequest 'https://f-droid.org/repo/uk.nktnet.webviewkiosk_141.apk' -OutFile $apk -UseBasicParsing
  }

  Paso 'Dejando la botonera para que arranque sola con Windows ...'
  $sh = New-Object -ComObject WScript.Shell
  $exe = Join-Path $dst 'Botonera.exe'
  $l = $sh.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Startup')) 'Botonera.lnk'))
  $l.TargetPath = $exe; $l.WorkingDirectory = $dst; $l.Save()

  Paso 'Creando los accesos en la carpeta C:\BotoneraDeck (nada en el Escritorio) ...'
  # versiones viejas los ponian en el Escritorio: se borran
  Get-ChildItem ([Environment]::GetFolderPath('Desktop')) -Filter 'Botonera - *.lnk' -ErrorAction SilentlyContinue | Remove-Item -Force
  Remove-Item (Join-Path $dst 'Botonera - Editar botones.lnk') -ErrorAction SilentlyContinue
  $a = $sh.CreateShortcut((Join-Path $dst 'Botonera - EDITOR de botones.url'))
  $a.TargetPath = 'http://localhost:8790/editor.html'; $a.Save()
  $a = $sh.CreateShortcut((Join-Path $dst 'Botonera - Conectar Celu o Tablet (QR).url'))
  $a.TargetPath = 'http://localhost:8790/conectar.html'; $a.Save()
  if (Test-Path (Join-Path $PSScriptRoot 'Manual - Botonera Deck.pdf')) { Copy-Item (Join-Path $PSScriptRoot 'Manual - Botonera Deck.pdf') $dst -Force }
  $a = $sh.CreateShortcut((Join-Path $dst 'Botonera - botones.json (avanzado).lnk'))
  $a.TargetPath = 'notepad.exe'; $a.Arguments = '"' + (Join-Path $dst 'botones.json') + '"'; $a.Save()
  $a = $sh.CreateShortcut((Join-Path $dst 'Botonera - Carpeta de iconos.lnk'))
  $a.TargetPath = Join-Path $dst 'web\icons'; $a.Save()
  $a = $sh.CreateShortcut((Join-Path $dst 'Botonera - Asignar teclas en AC.lnk'))
  $a.TargetPath = 'powershell.exe'; $a.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $dst 'asignar_teclas_AC.ps1') + '"'; $a.Save()
  $a = $sh.CreateShortcut((Join-Path $dst 'Botonera - Reiniciar.lnk'))
  $a.TargetPath = Join-Path $dst 'reiniciar.bat'; $a.WorkingDirectory = $dst; $a.Save()
  $a = $sh.CreateShortcut((Join-Path $dst 'Botonera - Guia.lnk'))
  $a.TargetPath = Join-Path $dst 'LEEME - Como instalar y configurar.txt'; $a.Save()

  Paso 'Buscando la tablet por USB ...'
  & $pt start-server 2>$null | Out-Null
  $dev = (& $pt devices) -join "`n"
  if ($dev -match "`tdevice") {
    Write-Host '   Tablet encontrada. Instalandole la app de pantalla completa...'
    & $pt install -r $apk | Out-Null
    Write-Host '   Listo.'
  } elseif ($dev -match "`tunauthorized") {
    Write-Host '   La tablet esta conectada pero falta AUTORIZAR: en la tablet acepta el cartel "Permitir depuracion USB"' -ForegroundColor Yellow
    Write-Host '   (marca "Permitir siempre") y despues volve a correr INSTALAR.bat.' -ForegroundColor Yellow
  } else {
    Write-Host '   No veo la tablet todavia (es normal si no la preparaste). Segui la guia, paso 2, y volve a correr INSTALAR.bat.' -ForegroundColor Yellow
  }

  Paso 'Arrancando la botonera ...'
  Start-Process $exe -WorkingDirectory $dst
  Start-Sleep -Milliseconds 1200
  Start-Process 'http://localhost:8790/conectar.html'
  Write-Host ''
  Write-Host 'INSTALACION TERMINADA.' -ForegroundColor Green
  Write-Host 'Todo quedo en la carpeta C:\BotoneraDeck (te la abro ahora) y se abrio el QR para conectar tu celu o tablet por WiFi.'
  Start-Process explorer.exe $dst
} catch {
  Write-Host ''
  Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red
  Write-Host 'Revisa que tengas internet y volve a correr INSTALAR.bat.'
}
Read-Host 'Enter para cerrar'
