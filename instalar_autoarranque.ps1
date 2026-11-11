# instalar_autoarranque.ps1
# Deja Botonera.exe arrancando SOLO con Windows (acceso directo en shell:startup).
# Botonera.exe es winexe (sin ventana) y tiene Mutex (no se duplica).
# Uso:
#   .\instalar_autoarranque.ps1          -> instala el autoarranque
#   .\instalar_autoarranque.ps1 -quitar  -> lo saca
param([switch]$quitar)

$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe     = Join-Path $here "Botonera.exe"
$startup = [Environment]::GetFolderPath('Startup')
$lnk     = Join-Path $startup "Botonera.lnk"

if ($quitar) {
  if (Test-Path $lnk) { Remove-Item $lnk -Force; Write-Host "Autoarranque QUITADO: $lnk" -ForegroundColor Yellow }
  else { Write-Host "No estaba instalado." }
  return
}

if (-not (Test-Path $exe)) { Write-Host "No encuentro Botonera.exe en $here. Compilalo primero." -ForegroundColor Red; return }

$sh = New-Object -ComObject WScript.Shell
$s  = $sh.CreateShortcut($lnk)
$s.TargetPath       = $exe
$s.WorkingDirectory = $here
$s.WindowStyle      = 7          # minimizado (winexe igual no muestra ventana)
$s.Description       = "Botonera deck - servidor de la botonera tactil"
$s.Save()

Write-Host "Autoarranque INSTALADO:" -ForegroundColor Green
Write-Host "  $lnk  ->  $exe"
Write-Host "Se va a iniciar solo en el proximo arranque de Windows. Para probar ahora: ejecuta Botonera.exe."
