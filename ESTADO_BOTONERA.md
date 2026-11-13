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
| deaf | AURICULARES | vol `mute` = silencia TODO Windows (toggle con estado real) |
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
Compilar Botonera.exe: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:Botonera.exe Botonera.cs`
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

## 17. [2026-11-12] INICIO: AURICULARES y VOLUMEN = volumen GENERAL de Windows ✅
- AURICULARES ya no es atajo de Discord: es `vol mute` (tecla multimedia). La luz muestra el mute REAL de Windows (Core Audio, se lee en `/state`).
- VOL −/+: cada toque = 4% (2 pasos). Ya no pasan por el filtro anti-doble toque (antes se comía los toques rápidos).
- Mismo cambio en `kit/botones.json` y en la config por defecto de `Botonera.cs`. MIC sigue siendo el atajo de Discord.

## 18. [2026-11-12] KIT = botonera IDÉNTICA a la del simu ✅
- Pedido del usuario: al amigo le aparecía otro orden y sin SETUPHUB (el kit tenía un `kit/botones.json` genérico). Se borró.
- `armar_kit.ps1` copia el `botones.json` del repo + TODOS los íconos (incluye SetupHub, Antigravity, servidores dc_*).
- `instalar.ps1` ahora REEMPLAZA botones.json (guarda el viejo como `botones_anterior.json`).
- Ojo: rutas propias (ASSETTO = `%USERPROFILE%\Desktop\Content Manager.exe`, SETUPHUB = `C:\Program Files\SetupHubPro\app.exe`) tienen que existir igual en la PC del amigo.

## 19. [2026-11-12] EDITOR en la PC + kit sin accesos en el Escritorio ✅
- `http://localhost:8790/editor.html` (`web/editor.html`): copia de la botonera; tocar boton -> panel. Programas: DESPLEGABLE con los accesos del menu Inicio + Escritorio (`/editor/apps`), o `Buscar…` (ventana Abrir de Windows, `/editor/pickexe`). De .lnk/.url/.exe saca ruta+args, nombre y el ICONO grande (jumbo 256, recortado) a `web/icons/app_*.png`.
- Teclas: desplegable de `web/atajos.json` (Assetto = las de asignar_teclas_AC; iRacing = SUGERIDAS ctrl+shift+f1..f12, se asignan en iRacing tocando el boton; Discord; Windows) + Grabar.
- Iconos: elegir de la lista o `Imagen de la PC…` (`/editor/pickimg`: copia, pasa a PNG <=256).
- GUARDAR -> POST `/editor/save` (valida, backup `botones_backup.json`). `/state` trae `_cfg` (fecha de botones.json): la tablet se RECARGA SOLA en la misma pagina. Vale tambien para editarlo a mano.
- Kit: accesos ya NO en el Escritorio, todos dentro de `C:\BotoneraDeck` (y borra los viejos del Escritorio); guia con seccion de ICONOS paso a paso y del EDITOR.

## 20. [2026-11-12] DISCORD y VOLANTE automáticos por persona ✅
- Discord 1.0.9267 ya NO deja logos en la caché. Servidores: Local Storage (`selectedChannelIds` / `mostRecentSelectedTextChannelIds`) + `logs\renderer_js*` (`Transitioning to /channels/<id>/`) + caché (por si acaso).
- Nombre: se abre `discord://-/channels/<id>` y se lee el título (`#canal | Servidor - Discord`); si no cambia, no es miembro → se descarta. Nombres cortos: lo de antes de ` - `, máx 20 letras cortando entre palabras.
- Logo: original de la caché si hay hash; si no, se RECORTA de la ventana (PrintWindow): la rayita blanca de la columna 0 marca el servidor abierto (alto L; logo en x=0.4L, LxL). Discord se agranda al 200% (5× Ctrl+OEM_PLUS) para que salga nítido y después vuelve (5× Ctrl+OEM_MINUS), servidor original y minimizado. Sin rayita → ícono con iniciales.
- Corre solo al arrancar (15 s) si botones.json no tiene servidores y no existe `discord_detectado.txt`; en el editor: `Actualizar mis servidores de Discord` (`/editor/discord`). El kit sale SIN servidores ni íconos dc_*.
- VOLANTE: botón con `"auto": "volante"`; al arrancar se completa con el primer programa de volante instalado de `web/atajos.json > programas` (MOZA, G HUB, Simagic, Fanatec, Thrustmaster). Editor: desplegable Simracing en Abrir un programa (`/editor/programa?n=`).
- Probado: en esta PC detectó los 9 servidores con logos; simulación de kit limpio de punta a punta OK. En el botones.json del usuario quedaron SUS nombres/logos de siempre (se restauró tras la prueba).

## 21. [GEMINI — 2026-11-13] WiFi + QR + PWA + Responsive + Volumen por App + Kit ZIP + PDF ✅
- **Conexión WiFi/LAN + QR + PWA (para Samsung A51 y cualquier celu/tablet):**
  - `Botonera.cs`: intenta escuchar en `http://+:8790/` (WiFi/LAN + USB); si aún falta `netsh urlacl`, hace fallback a `localhost` sin fallar y expone `/netinfo` y `/netenable` (eleva `netsh http add urlacl` + regla de Firewall TCP 8790 y reinicia solo).
  - `web/conectar.html`: página con IP local + **código QR generado offline** en `<canvas>` + botón 1-clic `Habilitar WiFi / Firewall` + instrucciones para instalar como app en Chrome.
  - `web/manifest.json` + meta tags PWA en `web/index.html` para abrir en pantalla completa desde la pantalla de inicio del celu/tablet.
- **Responsive automático (`web/index.html`, ES5 puro):** `calcLayout()` calcula `--size`, `--gap`, `--dgap`, `--lbl` y re-rellena huecos `LIBRE` al cargar y al rotar (`resize`/`orientationchange`), adaptándose a celular vertical (3 cols), celular horizontal (2 filas), tablet 7"/8" y Galaxy Tab 2.
- **Volumen por programa (`appvol` via Core Audio `IAudioSessionManager2`):**
  - Nuevo tipo `appvol` en `Botonera.cs` (`val` = proceso como `Discord`, `acs`, `chrome`, `Spotify`; `args` = `up` / `down` / `mute`), con lectura real de mute en `/state` y endpoint `/audioapps`.
  - Integrado en `web/editor.html` (desplegable de apps activas + botón `Conectar Celu / QR` en el header).
- **Sin ícono en la bandeja del reloj:** quitado por pedido del usuario; todo queda limpio en segundo plano y se accede por URL o desde los accesos directos en `C:\BotoneraDeck`.
- **Instalador + Kit ZIP + Manual PDF:** `kit/armar_kit.ps1` y `kit/instalar.ps1` actualizados (incluyen `conectar.html`, `manifest.json` y `Manual - Botonera Deck.pdf`; al finalizar la instalación abre `conectar.html` con el QR). Generado `BotoneraDeck-Kit.zip` (282 KB) en el Escritorio.
- **Pendiente al ir al simu / probar Samsung A51:** `git pull` en la PC del simu (o instalar desde el `.zip`), abrir `http://localhost:8790/conectar.html`, tocar `Habilitar WiFi / Firewall` una vez, y escanear el QR con el A51. Queda para después el auto-updater contra GitHub Releases.

## 22. [GEMINI — 2026-11-13] Pruebas en vivo con Samsung A51 + Fixes ✅
- **WiFi en vivo probada:** El A51 se conectó por WiFi a `http://192.168.3.178:8790/index.html`.
- **Fix urlacl en español:** Se corrigió el comando `netsh` con `sddl=D:(A;;GX;;;WD)` para que funcione en Windows en español sin depender de `user=Everyone`.
- **Fix Volumen (Core Audio):** Antes `keybd_event` para teclas multimedia no movía el volumen y quedaba clavado en 100%. Se cambió a control directo via `Endpoint().SetMasterVolumeLevelScalar`: probado en vivo bajando de 64% a 36% y subiendo a 44% desde el teléfono.
- **Fix Enlaces Web (OpenUrl):** `Process.Start` abría pestañas en segundo plano si el navegador ya estaba abierto; ahora busca y trae la ventana del navegador (`chrome`, `msedge`, etc.) al frente con `SetForegroundWindow`.
- **Botón Pantalla Completa + PWA:** Se agregó botón `⛶` en la barra de pestañas para activar Fullscreen en Chrome del celular y ocultar la barra de navegación. Se ajustó el auto-escalado para que los botones llenen todo el ancho y alto sin márgenes vacíos.
- **Todo commiteado y pusheado a GitHub** (commits `56267c4`, `174e5f2`, `b05e1b2`).

## 23. [GEMINI — 2026-11-13] Ajuste 100% Pantalla + Cero Scroll en Teléfonos ✅
- **Cero botones afuera:** Se reescribió el motor de layout en `web/index.html`. Ahora calcula el tamaño exacto probando columnas para garantizar que todos los botones de la página entren 100% dentro del alto y ancho disponible sin scroll vertical ni salirse de los marcos.
- **Centrado automático:** `#pages` y `.page` ahora usan flexbox centrado con `overflow: hidden`, evitando desbordes o botones cortados.
- **Iconos propios de Botonera Deck:** Se crearon `web/icons/botonera.png` (192px) y `web/icons/botonera512.png` (512px) con diseño propio neón azul y se asignaron en `manifest.json`, favicons y PWA. Se eliminó el ícono de Assetto del acceso directo.
- **Pusheado a GitHub:** commit `fdd7834`. Kit ZIP actualizado en el Escritorio.

## 24. [CLAUDE - 2026-11-13] Fix: botones no disparaban + fullscreen tiraba botones afuera
El usuario reporto que tocando el celu "no pasaba nada" (solo el cursor de la PC "pensando") y que al tocar el boton de pantalla completa los botones se iban de la pantalla. Diagnostico en vivo:
- **La cadena accion->PC funciona** (el log mostraba `toque: volup -> volumen up 80/84/88%`). El problema era el lado del navegador.
- **Causa del "no dispara":** el toque salia al LEVANTAR el dedo (`touchend`) y SOLO si `!moved`, donde `moved` se activaba con apenas **18px** de movimiento. En un celu con pantalla sensible casi cualquier toque registra ese micro-movimiento -> se tomaba como "desliz" -> nunca llamaba a `/press`. Por eso no habia `toque:` en el log.
- **Causa del "se van de la pantalla":** `calcLayout()` para telefono dimensionaba para 15 botones en 3 filas, pero la pagina **PITS** necesita 4 filas (fila de 3 + cruz 3x3). La cruz quedaba fuera.

**Arreglos en `web/index.html`:**
1. La accion ahora sale **AL TOCAR** (`touchstart`), instantaneo y a prueba de fallos. Se mantiene el guard anti-doble-disparo de la tablet (ignora el clic sintetico que llega <1s despues del toque).
2. Se **quito el gesto de deslizar** entre paginas (era lo que se comia el toque). La navegacion queda por las pestañas de arriba y por los botones `page`/`goto`.
3. `calcLayout()` ahora **reserva 4 filas** al calcular el tamaño -> ninguna pagina (ni PITS) se sale, ni en pantalla completa.
4. Recalculo de layout mas robusto al entrar/salir de fullscreen (120ms + 400ms, y se cubre `mozfullscreenchange`).

**IMPORTANTE:** para que el celu tome el fix hay que **recargar la pagina** en el navegador del celu (el auto-reload solo se dispara si cambia `botones.json`, no `index.html`).

**Nota sobre la notebook de pruebas:** los botones de tipo `launch` (Discord, Assetto, SimHub, etc.) no tienen la app instalada aca, por eso Windows muestra el cursor "pensando" y no pasa nada visible; en la PC del simu abren bien. Los de volumen y URL (YouTube/Google) si funcionan en la notebook. Las teclas de CARRERA/PITS se mandan a la ventana enfocada (en el simu, a Assetto).

## 25. [CLAUDE - 2026-11-13] Fix: barra de volumen visible + paginas web al frente (Botonera.cs)
El usuario reporto que al modificar el volumen no aparecia la barra, y que YouTube/Google/Gmail no abrian pestaña visible ("queda detras" no alcanza). Arreglado en `Botonera.cs` (recompilado):
- **Volumen con barra (OSD):** `Volume()` mandaba la tecla multimedia como **scancode** (via `KeyScan`/`SendInput`), y asi Windows NO muestra la barra ni, en algunas PC, mueve el nivel. Ahora manda la tecla multimedia por **VK directo** (`keybd_event(0xAF/0xAE/0xAD)`), que mueve el volumen (pasos de 2%) **y muestra la barra** de Windows. Core Audio queda solo de respaldo: si la tecla no movio el nivel, lo mueve a mano (4%). Mute: solo tecla multimedia (evita doble toggle).
- **Paginas web al frente SI o SI:** `OpenUrl()` usaba `SetForegroundWindow` desde el hilo de segundo plano, y **Windows lo bloquea** (la pestaña abria detras de la ventana activa). Nuevo helper `ForceFront(hwnd)` que usa el truco **`AttachThreadInput`** (adjuntarse al hilo de la ventana con foco) + `BringWindowToTop` + `SetForegroundWindow`, con 2 intentos (350ms y 550ms) por si el navegador tarda en crear la pestaña. Restaura la ventana si estaba minimizada.
- **Import nuevo:** `BringWindowToTop` (user32).
- Probado en vivo: volumen 94%->92%->94% en pasos de 2% (tecla multimedia real -> OSD), y `url OK` + `ForceFront` sin error.

Linea de compilacion (sigue igual): `csc.exe /nologo /target:winexe /optimize /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:Botonera.exe Botonera.cs`

## 26. [CLAUDE - 2026-11-13] Layout por pagina: llena la pantalla en horizontal Y vertical
El usuario reporto que en horizontal los botones quedaban chicos en el medio con huecos a los lados, en vez de ocupar la pantalla. Causa: habia **un solo tamaño global** para todas las paginas y se reservaban 4 filas (por la cruz de PITS), asi que en horizontal todo quedaba chico y con pocas columnas centradas.

**Reescrito el motor de layout en `web/index.html`:**
- Se eliminaron `calcLayout`/`applyGridCols`/`fillGrid` globales. Ahora **cada pagina calcula su propio tamaño** con `layoutActive()`, que corre en cada `showPage()`, `resize`, `orientationchange` y cambio de fullscreen.
- El algoritmo prueba columnas 2..8 y, **entre las que dan buen tamaño (>=80% del maximo), elige la que MAS llena el ancho** -> en horizontal usa mas columnas y llena la pantalla (antes elegia el boton mas grande y quedaba angosto/centrado).
- PITS (dpad) se dimensiona aparte: 3 columnas x 4 filas (fila + cruz), queda compacta y centrada (un D-pad no se estira).
- La ultima fila se completa con teclas `LIBRE` para que la grilla quede rectangular y llena.
- Tope de tamaño 126px -> en la tablet grande (Tab 2) sigue dando 8 col x 126 como antes.
- Verificado por simulacion: INICIO llena 99-100% del ancho en vertical (3 col) y horizontal (7-8 col); CARRERA 100%; tablet 8x126. En pantallas muy anchas y bajas queda margen vertical (botones cuadrados) pero el ancho se llena y nada se corta.


