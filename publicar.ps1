# publicar.ps1  -  Publica una actualizacion de la Botonera.
# A todos los que tengan la botonera les va a aparecer el cartel "Actualizacion disponible".
#
# Uso:
#   .\publicar.ps1 "Arregle tal cosa"            -> sube el ultimo numero de version solo (1.0.3 -> 1.0.4)
#   .\publicar.ps1 "Cambios grandes" 1.1.0       -> fija la version a mano
#
# Que hace: si tocaste Botonera.cs lo recompila, actualiza version.json, hace commit y push.

param([string]$notas, [string]$version)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

# 1) version
$vp = Join-Path $here "version.json"
$v  = Get-Content $vp -Raw | ConvertFrom-Json
if (-not $version) {
  $p = [string]$v.version -split '\.'
  $p[-1] = [string]([int]$p[-1] + 1)
  $version = ($p -join '.')
}
if (-not $notas) { $notas = "Mejoras y correcciones." }

# 2) recompilar si Botonera.cs cambio despues del .exe
$cs  = Join-Path $here "Botonera.cs"
$exe = Join-Path $here "Botonera.exe"
if ((Test-Path $cs) -and (Test-Path $exe) -and (Get-Item $cs).LastWriteTime -gt (Get-Item $exe).LastWriteTime) {
  Write-Host "Botonera.cs cambio -> recompilando..." -ForegroundColor Yellow
  Get-Process Botonera -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Milliseconds 500
  $csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
  & $csc /nologo /target:winexe /optimize /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:$exe $cs
  if ($LASTEXITCODE -ne 0) { throw "Error al compilar Botonera.cs" }
  Write-Host "compilado OK" -ForegroundColor Green
  Start-Process $exe   # la vuelve a arrancar
}

# 3) escribir version.json (sin BOM)
$fecha = (Get-Date).ToString('yyyy-MM-dd')
$out = [ordered]@{ version = $version; fecha = $fecha; notas = $notas }
[System.IO.File]::WriteAllText($vp, ($out | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "version.json -> v$version  ($notas)" -ForegroundColor Cyan

# 4) commit + push
git add -A
git commit -m "Actualizacion v$version - $notas"
git push origin main

Write-Host ""
Write-Host "LISTO. Publicada la v$version." -ForegroundColor Green
Write-Host "A los que tengan la botonera abierta les va a aparecer el aviso en unos minutos (o al reabrirla)." -ForegroundColor Green
