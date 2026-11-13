# Botonera Deck — arrancá por acá

Botonera táctil estilo Stream Deck en la tablet **Galaxy Tab 2 (GT-P5110)**, conectada a la PC del simulador **por USB**.
Todo el estado, cómo está armada y lo pendiente: 👉 **[README.md](README.md)**.

- Repo PÚBLICO: https://github.com/lanarito/botonera-deck — para guardar: `git add -A`, commit y push. Para **publicar una actualización** a todos: `.\publicar.ps1 "que cambie"` (sube version.json, recompila si hace falta, commit + push).
- Proyectos hermanos (NO mezclar): `C:\Github repos\luces-sim` (luces ambiente) y `C:\Github repos\moza-cm2-dash` (dash MOZA CM2 + joystick).
- No tocar nada de iflag (`C:\Github repos\iflag`).
- Pesados que NO van a GitHub (se vuelven a bajar): `tablet/platform-tools` (adb oficial de Google),
  `tablet/rom` (LineageOS 14.1 espressowifi 20200623, AndroidFileHost fid 8889791610682876961),
  `tablet/heimdall` (heimdall-suite-1.4.0-win32 de bitbucket benjamin_dobell). TWRP sí está (`tablet/twrp-3.4.0-0-espressowifi.img`).

## Para trabajar desde otra PC
1. `git clone https://github.com/lanarito/botonera-deck "C:\Github repos\botonera-deck"`
2. Bajar adb: https://dl.google.com/android/repository/platform-tools-latest-windows.zip → descomprimir en `tablet\`.
3. Compilar: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:Botonera.exe Botonera.cs`
4. La página (`web/index.html`) se puede editar y ver en cualquier navegador de PC (los botones fallan sin el servidor, pero el diseño se ve).

## Usuario
- Habla castellano rioplatense. Cuando pregunta "¿se puede…?", responder y NO cambiar nada.
- Le gusta el estilo actual de teclas 3D retroiluminadas ("así me gusta mucho más").
