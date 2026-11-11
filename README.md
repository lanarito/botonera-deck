# Botonera en la tablet (Galaxy Tab 2 GT-P5110) — estado al 2026-11-11

## Tablet
- Actualizada a **LineageOS 14.1 (Android 7.1.2)**, build unofficial de Android-Andi (2020-06-23). Recovery: **TWRP 3.4.0** (espressowifi).
  Archivos en `tablet/` (rom/, twrp-3.4.0-0-espressowifi.img, heimdall/, platform-tools/adb.exe).
- Sin Google Play: se instaló **Aurora Store** (F-Droid, v4.8.4) para bajar apps.
- Depuración USB activada. Pantalla siempre encendida mientras está enchufada (`stay_on_while_plugged_in 7`).
- Driver WinUSB (Zadig) puesto SOLO para la tablet en modo descarga (04E8:685D). No se tocó nada de MOZA.
- Cargando por USB de la PC se descarga lento (USB no alcanza): dejarla con cargador de pared si hace falta.

## Botonera (muestra funcionando)
- `Botonera.exe` (fuente `Botonera.cs`): servidor en `http://localhost:8790`, mantiene `adb reverse tcp:8790`
  → la tablet llega a la PC **por el cable USB** (no WiFi). Toque → `/press?id=` → teclas en la PC (keybd_event).
- `web/index.html`: teclas 3D (placa de aluminio con tornillos, hueco, paredes laterales, LED que se escapa,
  difusor retroiluminado, LED indicador, clic sintetizado, vibración). 4×3 horizontal / 3×4 vertical.
- Botones de muestra: MIC DISCORD (Ctrl+Shift+Alt+F9), AURICULARES (Ctrl+Shift+Alt+F10), VOLUMEN −/+.
  El usuario tiene que asignar esos atajos en Discord → Atajos de teclado.
- **NO arranca solo todavía** (ni Botonera.exe en la PC ni la página en la tablet). Se abre en el navegador Jelly.

## PENDIENTE (lo acordado para seguir)
1. Página **Inicio**: abrir en la PC Google, YouTube, Discord, Antigravity, Assetto Corsa (Content Manager), SetupHub Pro, SimHub;
   volumen Windows y **volumen por programa** (Discord, AC, navegador) con Core Audio.
2. Página **Carrera**: botones de AC (luces, ráfaga, balizas, limpia, pit limiter, cámaras, mirar costados, ocultar apps, boxes)
   con LEDs según el estado real del juego (Local\cm2_estado / acpmf_physics).
3. Página **Datos**: radar, gomas (temp/presión/desgaste), vueltas, delta, combustible, posición.
4. Investigar: configurar la parada en boxes desde botones (AC no trae teclas; ver CSP) y mostrar/ocultar nombres.
5. Sacar la barra del navegador (pantalla completa), autoarranque de Botonera.exe con Windows y de la página en la tablet.
- Volumen de partes del auto dentro de AC (motor/gomas): NO se puede en vivo → solo volumen total de AC.
