# Arma el kit para regalar (sin datos personales) en el Escritorio: carpeta BotoneraDeck-Kit + BotoneraDeck-Kit.zip
$repo = Split-Path -Parent $PSScriptRoot
$desk = [Environment]::GetFolderPath('Desktop')
$out  = Join-Path $desk 'BotoneraDeck-Kit'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$app = Join-Path $out 'BotoneraDeck'
New-Item -ItemType Directory -Force -Path (Join-Path $app 'web\icons') | Out-Null

foreach ($f in 'INSTALAR.bat', 'instalar.ps1', 'DESINSTALAR.bat', 'asignar_teclas_AC.ps1', 'LEEME - Como instalar y configurar.txt') { Copy-Item (Join-Path $PSScriptRoot $f) $out }
Copy-Item (Join-Path $repo 'Botonera.exe') $app
Copy-Item (Join-Path $PSScriptRoot 'reiniciar.bat') $app
Copy-Item (Join-Path $PSScriptRoot 'botones.json') $app
Copy-Item (Join-Path $repo 'web\index.html') (Join-Path $app 'web')
# iconos: todos menos los personales (servidores de Discord, programas propios)
Get-ChildItem (Join-Path $repo 'web\icons') -File | Where-Object { $_.Name -notmatch '^dc_|^setuphub|^antigravity|^spotify' } | Copy-Item -Destination (Join-Path $app 'web\icons')

$zip = Join-Path $desk 'BotoneraDeck-Kit.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
"Kit armado: $zip  ($([math]::Round((Get-Item $zip).Length/1KB)) KB)"
