# Botonera Deck - asigna en Assetto Corsa las teclas que usan los botones de CARRERA / PANTALLA / PITS.
# - Hace BACKUP de controls.ini antes de tocar nada.
# - Solo cambia la TECLA de cada accion; los botones del volante/joystick quedan como estaban.
# - Correrlo con Assetto Corsa CERRADO (AC lee los controles al arrancar).
param([string]$Ini = '', [switch]$SinPausa)
$ErrorActionPreference = 'Stop'
if ($SinPausa) { function Read-Host { param($x) } }
$ini = if ($Ini) { $Ini } else { Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Assetto Corsa\cfg\controls.ini' }
Write-Host ''
Write-Host '=== Botonera Deck: asignar teclas en Assetto Corsa ===' -ForegroundColor Cyan
if (-not (Test-Path $ini)) { Write-Host "No encuentro $ini . Abri Assetto Corsa una vez (o Content Manager > Controles) y volve a correr esto." -ForegroundColor Red; Read-Host 'Enter para salir'; exit 1 }
if (Get-Process acs -ErrorAction SilentlyContinue) { Write-Host 'Assetto Corsa esta ABIERTO. Cerralo y volve a correr esto.' -ForegroundColor Red; Read-Host 'Enter para salir'; exit 1 }

$bak = $ini + '.bak_botonera_' + (Get-Date -Format yyyyMMdd_HHmmss)
Copy-Item $ini $bak
Write-Host "Backup: $bak"

# seccion, tecla (codigo virtual), modificadores (17=Ctrl 18=Alt 16=Shift), descripcion
$map = @(
  @('ACTION_CHANGE_CAMERA',   '0x81', '',           'CAMARA = F18'),
  @('GLANCELEFT',             '0x51', '',           'MIRAR IZQ = Q'),
  @('GLANCERIGHT',            '0x45', '',           'MIRAR DER = E'),
  @('GLANCEBACK',             '0x57', '',           'MIRAR ATRAS = W'),
  @('__EXT_SEAT_FOV_INCREASE','0x6B', '',           'FOV + = Num+'),
  @('__EXT_SEAT_FOV_DECREASE','0x6D', '',           'FOV - = Num-'),
  @('__EXT_SEAT_UP',          '0x68', '',           'CAM ARRIBA = Num8'),
  @('__EXT_SEAT_DOWN',        '0x62', '',           'CAM ABAJO = Num2'),
  @('__EXT_SEAT_FORWARD',     '0x7D', '',           'ADELANTE = F14'),
  @('__EXT_SEAT_BACKWARD',    '0x7E', '',           'ATRAS = F15'),
  @('__EXT_SEAT_RESET',       '0x7F', '',           'VISTA ORIGINAL = F16'),
  @('__EXT_QMP_SHOW',         '0x87', '',           'PITS (menu rapido de boxes CSP) = F24'),
  @('DRIVER_NAMES',           '0x4C', '',           'NOMBRES = L'),
  @('HIDE_APPS',              '0x48', '',           'APPS / HUD = H'),
  @('__EXT_HIDE_UI',          '0x48', '17, 18, 16', 'OCULTAR TODO = Ctrl+Alt+Shift+H'),
  @('__CM_NEXT_APPS_DESKTOP', '0x7C', '',           'CAMBIAR APPS = F13'),
  @('__EXT_VIRTUAL_MIRROR',   '0x7A', '',           'ESPEJO = F11'),
  @('IDEAL_LINE',             '0x49', '',           'LINEA IDEAL = I'),
  @('SHOW_DAMAGE',            '0x4A', '',           'DANOS = J'),
  @('__EXT_SAVE_CLIP',        '0x53', '17, 16',     'GUARDAR CLIP = Ctrl+Shift+S'),
  @('__EXT_SIM_PAUSE',        '0x82', '',           'PAUSA = F19'),
  @('__CM_SETUP_CAR',         '0x80', '',           'SETUP AUTO = F17')
)

$t = [IO.File]::ReadAllText($ini)
if ($t -notmatch "`r`n") { $t = $t -replace "`n", "`r`n" }
foreach ($m in $map) {
  $sec = $m[0]; $key = $m[1] + ' ; botonera'; $mod = $m[2]
  $rx = '(?m)^\[' + [regex]::Escape($sec) + '\]\r\n((?:[^\[\r\n][^\r\n]*\r\n)*)'
  $hit = [regex]::Match($t, $rx)
  if ($hit.Success) {
    $body = $hit.Groups[1].Value
    if ($body -match '(?m)^KEY=') { $body = [regex]::Replace($body, '(?m)^KEY=[^\r\n]*', 'KEY=' + $key) }
    else { $body = $body + 'KEY=' + $key + "`r`n" }
    if ($mod -ne '') {
      if ($body -match '(?m)^KEY_MODIFICATOR=') { $body = [regex]::Replace($body, '(?m)^KEY_MODIFICATOR=[^\r\n]*', 'KEY_MODIFICATOR=' + $mod) }
      else { $body = $body + 'KEY_MODIFICATOR=' + $mod + "`r`n" }
    }
    $t = $t.Substring(0, $hit.Groups[1].Index) + $body + $t.Substring($hit.Groups[1].Index + $hit.Groups[1].Length)
    $estado = 'ok'
  } else {
    if (-not $t.EndsWith("`r`n")) { $t += "`r`n" }
    $t += "`r`n[" + $sec + "]`r`nBUTTON=-1`r`nBUTTON_MODIFICATOR=-1`r`nJOY=-1`r`nKEY=" + $key + "`r`nKEY_MODIFICATOR=" + $mod + "`r`n"
    $estado = 'agregada'
  }
  Write-Host ('  {0,-9} {1}' -f $estado, $m[3])
}
[IO.File]::WriteAllText($ini, $t, (New-Object Text.UTF8Encoding $false))
Write-Host ''
Write-Host 'LISTO. Ya podes abrir Assetto Corsa.' -ForegroundColor Green
Write-Host 'Nota: OCULTAR TODO, FOV, CAMARA arriba/abajo, adelante/atras, vista original, PITS y PAUSA necesitan Custom Shaders Patch (CSP).'
Write-Host 'Si algo sale mal, volve al backup: renombra el .bak a controls.ini'
Read-Host 'Enter para cerrar'
