# Arma el kit para regalar (sin datos personales) en el Escritorio: carpeta BotoneraDeck-Kit + BotoneraDeck-Kit.zip
$repo = Split-Path -Parent $PSScriptRoot
$desk = [Environment]::GetFolderPath('Desktop')
$out  = Join-Path $desk 'BotoneraDeck-Kit'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$app = Join-Path $out 'BotoneraDeck'
New-Item -ItemType Directory -Force -Path (Join-Path $app 'web\icons') | Out-Null

foreach ($f in 'INSTALAR.bat', 'instalar.ps1', 'DESINSTALAR.bat', 'asignar_teclas_AC.ps1', 'LEEME - Como instalar y configurar.txt', 'Manual - Botonera Deck.pdf') { Copy-Item (Join-Path $PSScriptRoot $f) $out }
Copy-Item (Join-Path $repo 'Botonera.exe') $app
Copy-Item (Join-Path $PSScriptRoot 'reiniciar.bat') $app
# botonera IGUAL a la del simu (orden, SETUPHUB, ANTIGRAVITY...), salvo lo que es de cada persona:
#  - servidores de Discord: se sacan; Botonera.exe detecta los de cada uno solo al arrancar la primera vez
#  - boton VOLANTE: queda en automatico; Botonera.exe le pone la marca instalada (MOZA, Logitech, Simagic...)
$t = [IO.File]::ReadAllText((Join-Path $repo 'botones.json'))
$t = ($t -split "`n" | Where-Object { $_ -notmatch 'discord://-/channels/' }) -join "`n"
$t = [regex]::Replace($t, '(?m)^(\s*)\{ "id": "volante".*?\}', '$1{ "id": "volante", "label": "VOLANTE", "icon": "", "type": "launch", "val": "", "auto": "volante" }')
$t = [regex]::Replace($t, ',(\s*\n\s*\])', '$1')   # sin coma despues del ultimo boton de cada pagina
Add-Type -AssemblyName System.Web.Extensions
$null = (New-Object Web.Script.Serialization.JavaScriptSerializer).DeserializeObject($t)   # que siga siendo valido
[IO.File]::WriteAllText((Join-Path $app 'botones.json'), $t, (New-Object Text.UTF8Encoding $false))
foreach ($w in 'index.html', 'editor.html', 'atajos.json', 'conectar.html', 'manifest.json') { Copy-Item (Join-Path $repo "web\$w") (Join-Path $app 'web') }
Get-ChildItem (Join-Path $repo 'web\icons') -File | Where-Object { $_.Name -notmatch '^spotify|^dc_' } | Copy-Item -Destination (Join-Path $app 'web\icons')

$zip = Join-Path $desk 'BotoneraDeck-Kit.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
"Kit armado: $zip  ($([math]::Round((Get-Item $zip).Length/1KB)) KB)"
