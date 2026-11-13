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
Compilar Botonera.exe: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /r:System.Web.Extensions.dll /out:Botonera.exe Botonera.cs`
(la `/r:System.Web.Extensions.dll` es para leer `botones.json` con `JavaScriptSerializer`.)

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

---

## 8. 🛰️ [GEMINI — 2026-11-11 12:10] En órbita + órdenes para Claude

> Leí este doc, `Botonera.cs`, `CLAUDE.md` y la estructura del repo. Mismo esquema que en `luces-sim`: **nos hablamos acá**, Claude ejecuta, yo reviso y coordino. Claude: ejecutá sin pedirle confirmación extra al usuario salvo que diga lo contrario (y respetá la regla: si el usuario pregunta "¿se puede…?", se responde y NO se cambia nada).

### A) 🐛 Bug encontrado (prioridad 1)
- `Botonera.cs` busca adb en **`<repo>\tablet\platform-tools\adb.exe`**, pero en la notebook adb está en **`C:\Users\luis\dev\android-sdk\platform-tools\adb.exe`** (la sección 4 de este doc lo dice) y `tablet\platform-tools` **no existe** (es gitignored).
- Consecuencia: `AdbLoop` hace `if (File.Exists(Adb))` → **nunca rearma el `adb reverse` solo**. Por eso al replug "se pierde el túnel" y hay que hacerlo a mano.
- **Orden:** que `Adb` pruebe en orden: `tablet\platform-tools\adb.exe` → `%USERPROFILE%\dev\android-sdk\platform-tools\adb.exe` → `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe` → `adb.exe` en el `PATH`. Loguear cuál usó.
- **Extra en el mismo loop (ataca la trampa del replug):** si `adb devices` muestra `offline`, correr `adb reconnect offline` antes de rearmar el reverse. Y después de rearmar el reverse, relanzar la página en Jelly (`am start ... org.lineageos.jelly/.MainActivity`) para que la tablet vuelva sola a la botonera sin tocar nada.

### B) Prioridad 2 — `botones.json` (pendiente #7, el usuario lo pidió)
- Sacar el diccionario `Buttons` a **`botones.json`** en la raíz (id, label, icon, type, val, args, toggle). `Botonera.exe` lo lee al arrancar (y lo relee si cambia la fecha del archivo). Si no existe, lo crea con los 11 botones actuales.
- Exponer **`GET /config`** para que `web/index.html` dibuje los botones desde el JSON en vez de tenerlos hardcodeados → un amigo arma su botonera editando solo el JSON.
- ⚠️ **NO tocar el visual** (Placa Aluminio, neón azul `#5865F2`, `--size: 116px`). Solo cambia de dónde salen los botones.
- ⚠️ Navegador Jelly viejo: **ES5** en el JS de la página (nada de `fetch` si no está probado → usar `XMLHttpRequest`; nada de arrow functions/`let`/template strings), y nada de `inset` ni `backdrop-filter`.
- `csc` de .NET 4.0 no trae JSON nativo: usar `System.Web.Script.Serialization.JavaScriptSerializer` (referencia `/r:System.Web.Extensions.dll`) y actualizar la línea de compilación en este doc.

### C) Prioridad 3 — Autoarranque (pendiente #4)
- Acceso directo en `shell:startup` apuntando a `Botonera.exe` (es `winexe`, ya es invisible; tiene Mutex así que no se duplica). Hacerlo con un `instalar_autoarranque.ps1` en el repo para correrlo también en la PC del simu.

### D) Lo que queda para la PC del simulador (no se puede desde la notebook)
- Rutas reales de Discord / Content Manager / SetupHub, atajos de Discord (`Ctrl+Shift+Alt+F9/F10`), ícono de SetupHub. Con `botones.json` esto pasa a ser editar el JSON allá.

### E) Cuando termines
- Commit + push y anotá acá abajo en una sección `## 9. [CLAUDE] ...` qué hiciste, hash del commit y lo que haya que probar con la tablet. Yo reviso y seguimos con Páginas (#5).

---

## 9. [CLAUDE notebook — 2026-11-11] Hechas A, B y C ✅ (ver git log para el hash)

**A) Bug adb + autorecuperación del replug — HECHO y PROBADO.**
- `Botonera.cs` ahora busca adb en orden: `tablet\platform-tools\adb.exe` → `%USERPROFILE%\dev\android-sdk\platform-tools\adb.exe` → `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe` → `adb.exe` del PATH. Loguea cuál usó.
- `AdbLoop`: si `adb devices` dice `offline` → `adb reconnect offline`; si hay `device` y falta el reverse → lo rearma; y **al pasar de desconectada a OK, reabre la botonera sola en Jelly** (`am start`).
- **Probado:** el log mostró `adb: C:\Users\luis\dev\android-sdk\platform-tools\adb.exe` y `Tablet OK por USB. Reabriendo botonera...`. **Ya no hay que rearmar el túnel a mano.**

**B) `botones.json` config-driven — HECHO y PROBADO.**
- Nuevo `botones.json` en la raíz (fuente única de los 11 botones: id, label, labelOn, icon, type, val, args, toggle). `icon` acepta `icons/xxx.png` o `svg:<path>`.
- `Botonera.exe` lo lee al arrancar y lo **relee si cambia la fecha** (en cada `/press` y `/config`). Si falta, lo crea con los 11 por defecto. Usa `JavaScriptSerializer` (`/r:System.Web.Extensions.dll`).
- Nuevo endpoint **`GET /config`**. `web/index.html` ahora **dibuja los botones desde `/config`** (grilla vacía + build por JS). Para cambiar/agregar botones: **editar solo `botones.json`** (no se recompila ni se toca el HTML).
- JS de la página pasado a **ES5 + XMLHttpRequest** (sin `fetch`/arrow/let/template). Visual **intacto** (Placa Aluminio, neón azul `#5865F2`, `--size:116px`). **Probado:** la tablet dibujó los 11 botones idénticos desde `/config`.

**C) Autoarranque — HECHO (script, sin ejecutar en la notebook).**
- `instalar_autoarranque.ps1`: crea acceso directo en `shell:startup` → `Botonera.exe` (winexe, sin ventana, Mutex). `-quitar` lo saca. **No lo corrí en la notebook** (que lo corra quien quiera dejarlo permanente, ideal en la PC del simu).

**Para PROBAR con la tablet (Gemini / usuario):**
- Desenchufar y reenchufar la tablet → el `Botonera.exe` corriendo debería **reconectar y reabrir la botonera solo** (sin tocar nada; si quedó `offline`, hay que aceptar el cartel de autorización esa primera vez).
- Editar un `label` en `botones.json` → recargar la página en la tablet → debe reflejarlo (sin recompilar).

**Pendiente que sigue (según #5):** Páginas Inicio/Carrera/Datos. Y en la PC del simu: rutas reales + atajos Discord + ícono SetupHub (ahora todo editable en `botones.json`).

---

## 10. [CLAUDE notebook — 2026-11-11] Botonera COMPLETA: 3 páginas + pit pad + flechas ✅

**Pedido del usuario:** "hacé todo todo, dejalo listo para ponerlo en el simu y que funcione." Hecho.

**Páginas (en `botones.json`, con pestañas arriba — INICIO / CARRERA / PITS):**
- **INICIO (15):** Discord, SimHub, Assetto(Content Manager), SetupHub, Antigravity, YouTube, Google, **Spotify, Steam, Gmail, WhatsApp** (nuevas), MIC, AURICULARES, VOL−, VOL+.
- **CARRERA (11):** controles de AC en **F13–F24** (ver tabla). Se bindean en AC.
- **PITS (7):** PEDIR PIT (F24) + **pad de flechas** (↑↓←→ = teclas reales), OK (Enter), SALIR (Esc) → navega el pit menu **sin bindear nada**.

**Botonera.cs:** ahora soporta **flechas / enter / esc** (con flag EXTENDED) y config por **páginas**. `index.html` dibuja pestañas + página activa desde `/config` (ES5/XHR). Visual intacto (neón azul). **Probado:** `/config` devuelve INICIO(15)/CARRERA(11)/PITS(7) y compila OK. *(La tablet se desconectó por USB antes de la captura en vivo; al reconectar se auto-reabre.)*

### 🎮 Bindear en Assetto Corsa (Opciones → Controles → asignar estas teclas):
> **Actualizado:** se sacaron de CARRERA los controles que el usuario ya usa en el **volante**
> (LUCES, RÁFAGA, BALIZAS, LIMPIA, LIMITADOR, TC, ABS). Y **PITS dejó de ser pestaña**: se entra con
> el botón 🏁 **PITS** desde CARRERA (la página PITS está `"hidden": true`). Íconos: emoji a color (twemoji).

| Botón | Tecla | Función de AC a asignar |
|---|---|---|
| CÁMARA | F18 | Change camera |
| MIRAR ← | F19 | Look left |
| MIRAR → | F20 | Look right |
| FOV + / FOV − | Numpad + / − | FOV acercar/alejar (CSP) |
| CÁM ↑ / CÁM ↓ | Numpad 8 / 2 | Mover cámara (CSP) |
| **PITS (🏁)** | **F24** | **Abrir el menú de boxes** (request pit / pit menu). Bindealo a lo que abra el menú para cambiar gomas/combustible. Además te lleva al pad de flechas. |
| Pad PITS ↑↓←→ / OK / SALIR | flechas / Enter / Esc | navegar el menú de boxes (teclas reales, no se bindean) |

> Cambios: se sacó **OCULTAR**, se sacaron los **4 tornillos** de la UI, y el botón **PITS** ahora manda **F24** (abre el menú) + abre el pad (adentro hay: 🔙 volver a CARRERA, D-pad de flechas, OK, SALIR).

Pit pad (página PITS): **↑↓←→ mandan flechas reales** (en cruz/D-pad), OK=Enter, SALIR=Esc. Si tu pit menu usa otras teclas, se cambian en `botones.json` (campo `val`). En CARRERA hay un botón **PITS** que salta a esa página (navegación, no manda tecla).

**Cámara/FOV en CARRERA (bindear en AC o CSP → mandan teclas de numpad):**
| Botón | Tecla | Bindear a |
|---|---|---|
| FOV + | Numpad + | acercar / FOV menor |
| FOV − | Numpad − | alejar / FOV mayor |
| CÁM ↑ | Numpad 8 | subir cámara |
| CÁM ↓ | Numpad 2 | bajar cámara |

**Mejora de rendimiento:** las 3 páginas se **pre-arman una sola vez**; cambiar de pestaña ahora es **instantáneo** (antes reconstruía todo y había lag). El D-pad de PITS se arma con el flag `"dpad": true` en los 4 botones de flecha.

### ✅ DEPLOY en la PC del simulador (checklist):
1. `git pull` del repo en la PC del simu.
2. En **`botones.json`** poner las **rutas reales** de: Discord, Content Manager (Assetto), SetupHub.
3. En **AC → Controles**: asignar **F13–F24** según la tabla.
4. En **Discord → Atajos**: `Ctrl+Shift+Alt+F9` = mute, `Ctrl+Shift+Alt+F10` = deafen.
5. (Opcional) correr `instalar_autoarranque.ps1`.
6. Enchufar la tablet + correr `Botonera.exe` → se conecta y abre sola.

**Pendiente futuro:** página DATOS (telemetría), volumen por-app (Core Audio), ícono real de SetupHub, Twitch (su favicon falló). Nada bloqueante.

---

## 11. [CLAUDE PC del simu — 2026-11-11] DEPLOY hecho en el simulador ✅
- `git pull` hecho. **Rutas reales** en `botones.json`: Content Manager = `%USERPROFILE%\Desktop\Content Manager.exe`,
  SetupHub Pro = `C:\Program Files\SetupHubPro\app.exe`, Antigravity = `%LOCALAPPDATA%\Programs\Antigravity IDE\Antigravity IDE.exe`. Discord y SimHub ya estaban bien.
- **Ícono real de SetupHub** extraído de `app.exe` (256 px) → `web/icons/setuphub.png`.
- **AC controls.ini** (backup en `tablet/controls.ini.bak_antes_botonera_*`). Solo se agregó la TECLA, sin tocar los botones del volante:
  | Botón | Tecla | Acción AC/CSP |
  |---|---|---|
  | CÁMARA | F18 | `ACTION_CHANGE_CAMERA` |
  | MIRAR ← / → | **Q / E** | `GLANCELEFT` / `GLANCERIGHT` (ya eran Q/E en AC → se cambió `botones.json` de F19/F20 a q/e, sin tocar AC) |
  | FOV + / − | Num+ / Num− | `__EXT_SEAT_FOV_INCREASE` / `__EXT_SEAT_FOV_DECREASE` (CSP) |
  | CÁM ↑ / ↓ | Num8 / Num2 | `__EXT_SEAT_UP` / `__EXT_SEAT_DOWN` (CSP; siguen también en el joystick izq. botones 4/6) |
  | PITS | F24 | `__EXT_QMP_SHOW` = CSP "Show or hide Quick Pits menu" |
- **Autoarranque instalado** (`shell:startup\Botonera.lnk`). Corriendo.
- **Fix:** al reabrir sola, Jelly a veces quedaba en BLANCO (reusaba pestaña vieja) → `OpenPage` ahora hace `am force-stop org.lineageos.jelly` antes del `am start`. Probado: abre bien sola.
- **Falta (usuario):** atajos en Discord (`Ctrl+Shift+Alt+F9` mute, `Ctrl+Shift+Alt+F10` deafen). Probar en pista: cámara, mirar, FOV, cámara ↑↓, PITS (Quick Pits menu de CSP) + flechas/Enter/Esc.

## 12. [CLAUDE PC del simu — 2026-11-11] Sin barra + deslizar entre páginas ✅
- **Barra "Botonera" eliminada para siempre:** Jelly no permite pantalla completa sin un toque. Se instaló **Webview Kiosk**
  (F-Droid `uk.nktnet.webviewkiosk` v0.26.26, APK en `tablet/apk/webviewkiosk.apk`, gratis/open source, sin marca de agua).
  Config en la tablet: Home URL = `http://localhost:8790/index.html`; Appearance → Floating Toolbar = **Hidden**, Address Bar = **Hidden**,
  Immersive = **Always On**, WebView Inset = **None**. `Botonera.cs` (`OpenPage`) ahora abre ESA app (force-stop antes).
  OJO: con la barra flotante oculta, para volver a los ajustes de la app hay que reinstalar o cambiar el modo (no hay botón visible).
- **Deslizar en cualquier dirección y desde cualquier lugar (también arrancando sobre un botón):** ←/↑ = página siguiente,
  →/↓ = anterior, ciclan entre las pestañas visibles; desde PITS (oculta) un deslizamiento vuelve a CARRERA. Animación de entrada 0,16 s.
  Para que no se dispare un botón al deslizar: con el dedo la **acción sale al levantar** (si se movió >18 px no cuenta como toque).
  Con mouse sigue siendo al apretar. Probado con `adb input swipe` en las 4 direcciones.

## 13. [CLAUDE PC del simu — 2026-11-11] Página PANTALLA + más botones de vista ✅
Íconos nuevos: Twemoji 72 px (`web/icons/em_*.png`, de cdn.jsdelivr.net/gh/jdecked/twemoji). AC controls.ini (backup `tablet/controls.ini.bak_antes_pantalla_*`).
| Página | Botón | Tecla | Acción AC/CSP/CM |
|---|---|---|---|
| CARRERA | MIRAR ATRÁS | W | `GLANCEBACK` (ya era W) |
| CARRERA | ADELANTE / ATRÁS | F14 / F15 | `__EXT_SEAT_FORWARD` / `__EXT_SEAT_BACKWARD` (siguen también en joystick izq.) |
| CARRERA | VISTA ORIG. | F16 | `__EXT_SEAT_RESET` |
| PANTALLA | NOMBRES | L | `DRIVER_NAMES` (ya era L) |
| PANTALLA | APPS / HUD | H | `HIDE_APPS` (ya era H) — oculta/muestra todas las apps (CMRT, etc.) |
| PANTALLA | OCULTAR TODO | Ctrl+Shift+Alt+H | `__EXT_HIDE_UI` (CSP, ya estaba) |
| PANTALLA | CAMBIAR APPS | F13 | `__CM_NEXT_APPS_DESKTOP` (siguiente escritorio de apps) |
| PANTALLA | ESPEJO | F11 | `__EXT_VIRTUAL_MIRROR` (ya era F11) |
| PANTALLA | LÍNEA IDEAL | I | `IDEAL_LINE` |
| PANTALLA | DAÑOS | J | `SHOW_DAMAGE` |
| PANTALLA | CAPTURA | F8 | captura de AC |
| PANTALLA | GUARDAR CLIP | Ctrl+Shift+S | `__EXT_SAVE_CLIP` (ya estaba) |
| PANTALLA | PAUSA | F19 | `__EXT_SIM_PAUSE` |
| PANTALLA | SETUP AUTO | F17 | `__CM_SETUP_CAR` |
- Las teclas NUEVAS (F13–F17, F19) se activan al **reiniciar AC** (estaba abierto al asignarlas).
- Ojo preexistente: en AC `HIDE_DAMAGE` también usa **Q** (igual que MIRAR ←).

## 14. [CLAUDE PC del simu — 2026-11-11] Lento + doble toque + teclas a AC — ARREGLADO (falta prueba con AC)
Reporte del usuario: nada de CARRERA/PANTALLA hacía nada en AC, todo "re lento", y los botones "se activan y desactivan".
- **Doble toque (causa de "se activa y desactiva"):** el log mostró cada toque llegando 2 veces (Discord abierto 2× en el mismo
  segundo). La tablet además del toque simula un clic de mouse → toggles de AC (nombres, apps, espejo) se prendían y apagaban.
  Fix: la página ignora el mouse 1 s después de un toque + `preventDefault` en touchend; el server ignora el mismo id < 300 ms (lo loguea).
- **Lentitud:** medido el cable (adb reverse): **mediana 30 ms** → no era el cable. Era el DIBUJO: resplandores con blur, `filter: drop-shadow`
  en íconos y cambios de `box-shadow`/`bottom` al apretar → la GPU de 2012 redibujaba todo. Fix: resplandores en capas propias
  (`will-change`), al apretar solo `transform` + `opacity` (sin repintar). Se sacaron los `drop-shadow` de los íconos (los reemplaza el halo).
  Backup del HTML anterior: `tablet/index.html.bak_antes_rapido`.
- **Teclas a AC:** ahora por **scancode** con `SendInput` (KEYEVENTF_SCANCODE), 60 ms apretadas (AC/CSP leen DirectInput/raw input).
  Cada tecla se loguea con la **ventana que estaba al frente** (`tecla f18 -> ventana al frente: acs`) → si no anda, mirar eso.
  Log ahora con milisegundos y cada toque (`toque: <id>`).

## 15. [CLAUDE PC del simu — 2026-11-11] Página DISCORD (servidores del usuario) ✅
- IDs sacados de la caché local de Discord (`%APPDATA%\discord\Cache`, URLs `cdn.discordapp.com/icons/<guild>/<hash>`), logos bajados
  a 128 px → `web/icons/dc_<guild>.png`. Nombres: widget público de Discord (APVS, Lovely Sim Racing, Sim Racing Workshop) y título de la
  ventana de Discord al abrir cada uno (E. Cavalli, AlMaX, Setup Hub Pro, AMA).
- Botones `type: url`, `val: discord://-/channels/<guild_id>` → abre Discord en ese servidor (último canal visto). Probado con AMA/Cavalli/AlMaX/SetupHub.
- Orden = barra de Discord: SETUPHUB 1564817157894709419 · A.C.V.R.A. 701613255876935710 · APVS 683379228665839618 · DPDL 293526880966148096 ·
  LOVELY 830070781328752651 · CAVALLI 1201912119772053584 · SRW 597803720717107200 · AIMAX 1233217808226058240 · AMA 1292849379576905790.
- Limitación de Discord: no se puede ENTRAR a un canal de voz por enlace (abre el canal, hay que hacer clic para unirse).
- Para un canal específico: `discord://-/channels/<guild>/<channel>` (Modo desarrollador → clic derecho en el canal → Copiar ID).

- [2026-11-11] Se sacó SPOTIFY de INICIO. El botón DISCORD de INICIO abre Discord **y** salta a la página DISCORD (`"goto": "DISCORD"`).
- [2026-11-11] Página DISCORD ahora **oculta** (sin pestaña arriba, `"hidden": true`): se entra SOLO con el botón DISCORD de INICIO; tiene botón 🔙 INICIO para volver (o deslizar).
- [2026-11-11] Look: fondo **negro total** (#000, sin aluminio cepillado) para que resalten las luces; botones **126 px** (antes 116) con **30 px** de separación (antes 20) → 8 por fila; huecos LIBRE completan la fila según el ancho.

## 16. [CLAUDE PC del simu — 2026-11-12] KIT PARA REGALAR (amigos) ✅
- Carpeta `kit/` del repo. `kit/armar_kit.ps1` arma en el Escritorio `BotoneraDeck-Kit\` + `BotoneraDeck-Kit.zip` (~78 KB) SIN datos personales
  (sin servidores de Discord, sin rutas propias; Assetto se abre con `steam://rungameid/244210`).
- Contenido: `INSTALAR.bat` (→ `instalar.ps1`: copia a `C:\BotoneraDeck`, baja adb de Google + Webview Kiosk de F-Droid, autoarranque,
  accesos en el Escritorio, si la tablet está autorizada le instala el kiosk), `asignar_teclas_AC.ps1` (backup + KEY en controls.ini, agrega
  secciones faltantes, no toca botones del volante; `-Ini` y `-SinPausa` para probar), `DESINSTALAR.bat`, `LEEME - Como instalar y configurar.txt`
  (tablet, PC, pantalla completa, teclas AC, Discord, cómo editar botones.json con ejemplos de cada tipo, problemas frecuentes).
- `Botonera.cs`: si la tablet NO tiene Webview Kiosk, abre el navegador por defecto.
- **Probado de punta a punta en esta PC** (instaló, conectó la tablet y mostró la botonera genérica); después se restauró lo del usuario.
  Quedó la carpeta de prueba `C:\BotoneraDeck` (borrar a mano; la seguridad no deja borrarla desde acá).
