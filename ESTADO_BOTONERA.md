# ESTADO — BOTONERA DECK (coordinación Claude + Gemini)
**Última actualización:** 2026-11-11

> Doc de trabajo compartido para la botonera táctil. Repo privado: https://github.com/lanarito/botonera-deck
> Hermanos (NO mezclar): `luces-sim` (luces ambiente) y `moza-cm2-dash` (dash MOZA CM2). No tocar `iflag`.

---

## 1. Qué es
Botonera táctil estilo **Stream Deck** en una **tablet Samsung Galaxy Tab 2 (GT-P5110)**, conectada a la PC **por cable USB** (sin WiFi). Se toca un botón en la tablet y ejecuta una acción en la PC (abrir app, abrir página, teclas, volumen).

## 2. Arquitectura (son solo 2 piezas + el cable)
- **La página** (`web/index.html`): se muestra en la tablet (navegador Jelly de LineageOS). Dibuja los botones 3D y detecta el toque. Al tocar hace `GET /press?id=xxx`.
- **`Botonera.exe`** (fuente `Botonera.cs`): corre en la PC. Sirve la página en `http://localhost:8790/`, mantiene `adb reverse tcp:8790` (la tablet llega a la PC por el cable), y en `/press?id=` ejecuta la acción del botón.
- **Túnel:** `adb reverse tcp:8790 tcp:8790` → la tablet abre `http://localhost:8790` y sale por el USB a la PC. **OJO: al enchufar/desenchufar la tablet se pierde el reverse (y a veces queda `offline`); hay que rearmarlo/reautorizar.**

## 3. Estado actual — HECHO ✅
### Visual (le gusta así al usuario, NO cambiar sin pedir)
- Teclas **3D retroiluminadas** (placa aluminio, hueco, LED que se escapa, difusor). Estilo elegido: "Placa Aluminio".
- **Neón AZUL unificado en TODOS los botones** (`--c: #5865F2`, el de MIC). Se quitaron los colores por-marca (rojo/amarillo). Los **íconos** sí van a color real.
- Botones **cuadrados de medida fija** (`#grid { --size: 116px }` — cambiar ahí y todos respetan).
- **Estado activado** (toggles mic/auriculares): ícono + luz más prendidos (`.key.on`).
- **Sin sonido** al tocar. **Touch rápido** (`touch-action: manipulation`). **Pantalla completa** al primer toque (saca la barra del navegador).
- **Íconos reales**: SimHub (extraído del .exe), Discord/YouTube/Google/Antigravity/Assetto (descargados de internet, en `web/icons/`). SetupHub = ícono provisorio (no se encontró el real).

### Acciones (Botonera.exe)
`Botonera.exe` ya ejecuta cada botón. El mapa está en `Botonera.cs` → diccionario **`Buttons`** (id → acción). Tipos:
- `launch` = abrir programa · `url` = abrir página · `key` = teclas · `vol` = volumen (up/down/mute).

**Botones actuales:**
| id | label | acción |
|---|---|---|
| discord | DISCORD | launch `%LOCALAPPDATA%\Discord\Update.exe --processStart Discord.exe` |
| simhub | SIMHUB | launch `C:\Program Files (x86)\SimHub\SimHubWPF.exe` |
| youtube | YOUTUBE | url `https://www.youtube.com` |
| google | GOOGLE | url `https://www.google.com` |
| ac | ASSETTO | launch `%LOCALAPPDATA%\AcTools Content Manager\Content Manager.exe` |
| setuphub | SETUPHUB | launch (ruta pendiente) |
| antigravity | ANTIGRAVITY | launch `%LOCALAPPDATA%\Programs\Antigravity\Antigravity.exe` |
| mic | MIC | key `ctrl+shift+alt+f9` (toggle) |
| deaf | AURICULARES | key `ctrl+shift+alt+f10` (toggle) |
| voldn | VOLUMEN − | vol down |
| volup | VOLUMEN + | vol up |

## 4. Cómo correrlo (en la PC que controla)
```
1) Enchufar la tablet por USB, con Depuración USB activada (y autorizar el cartel si sale).
2) adb: C:\Users\luis\dev\android-sdk\platform-tools\adb.exe
3) Arrancar: Botonera.exe (en la raíz del repo). Sirve en localhost:8790.
4) Túnel: adb reverse tcp:8790 tcp:8790
5) Abrir en la tablet (Jelly): adb shell am start -a android.intent.action.VIEW -d "http://localhost:8790/index.html" -n org.lineageos.jelly/.MainActivity
```
Compilar Botonera.exe: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:Botonera.exe Botonera.cs`

## 5. PENDIENTE
1. **Rutas reales** de Discord / Content Manager / SetupHub → **se ajustan en la PC del simulador** (no están en la notebook). Editar en `Botonera.cs` → `Buttons`. SimHub y Antigravity ya tienen la ruta real de la notebook.
2. **Atajos en Discord**: asignar en Discord → Ajustes → Atajos de teclado: `Ctrl+Shift+Alt+F9` = silenciar micro, `Ctrl+Shift+Alt+F10` = ensordecer. Si no, mic/auriculares no hacen nada.
3. **Ícono real de SetupHub** (buscar su sitio o el .exe en la PC del simu).
4. **Autoarranque** de `Botonera.exe` con Windows (pendiente acordado; dejarlo transparente).
5. **Páginas**: Inicio / Carrera (botones de AC con LEDs según estado del juego) / Datos (telemetría) + navegación entre páginas.
6. **Volumen por programa** (Discord/AC/navegador por separado con Core Audio) — avanzado; por ahora es volumen general.
7. **Config-driven (`botones.json`)**: para que cualquiera (ej. un amigo) arme su botonera editando un archivo simple, sin tocar código. (El usuario preguntó por esto.)
8. **Estilo opcional "Liquid Glass"**: el usuario lo está evaluando (muestra en `web/estilos.html` no la tiene; la muestra de liquid glass se envió aparte). OJO: el navegador viejo de la tablet NO soporta `backdrop-filter` → el liquid glass se vería plano en la tablet.

## 6. Datos técnicos / trampas conocidas
- **Tablet:** Galaxy Tab 2 (P5110), **LineageOS 14.1 (Android 7.1.2)**, navegador **Jelly** (`org.lineageos.jelly/.MainActivity`). Serial adb: `c16079d17f7c8af`.
- ⚠️ **Navegador viejo**: NO soporta la propiedad CSS **`inset`** (usar `top/right/bottom/left` explícitos) ni **`backdrop-filter`**. `conic-gradient` sí anda. Esto ya causó que botones no se dibujaran; quedó corregido.
- **Puerto:** 8790. Si queda ocupado por un server viejo, liberar antes de arrancar Botonera.exe.
- **Conexión inestable al replug:** tras enchufar/desenchufar, `adb devices` puede quedar `offline` → `adb reconnect offline` y reautorizar en la tablet; y **rearmar `adb reverse tcp:8790`**.
- **Autofoto/diagnóstico:** `adb shell screencap -p /sdcard/s.png` + `adb pull` para ver la pantalla de la tablet.

## 7. Archivos clave
- `web/index.html` — la botonera (visual + lógica de toque).
- `Botonera.cs` / `Botonera.exe` — server + acciones (editar `Buttons`).
- `web/icons/` — íconos reales (png).
- `web/estilos.html` — muestra de estilos de botón (para elegir look).
- `README.md` / `CLAUDE.md` — info general del repo.
