// Botonera para la tablet (GT-P5110) por USB.
// - Sirve la pagina web (web\index.html) en http://localhost:8790/
// - Mantiene "adb reverse tcp:8790" para que la tablet llegue a la PC por el cable USB (sin WiFi).
// - Cada toque llega como /press?id=xxx y se convierte en teclas en la PC.
// Compilar: csc /nologo /target:winexe /optimize /out:Botonera.exe Botonera.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class Botonera
{
    const int Port = 8790;
    static readonly string Dir = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string WebDir = Path.Combine(Dir, "web");
    static readonly string LogPath = Path.Combine(Dir, "estado_botonera.txt");
    static readonly string Adb = Path.GetFullPath(Path.Combine(Dir, @"tablet\platform-tools\adb.exe"));

    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
    const uint KEYUP = 0x2, EXTENDED = 0x1;

    const byte CTRL = 0x11, SHIFT = 0x10, ALT = 0x12;

    // id -> teclas (modificadores primero, la ultima es la tecla principal)
    static readonly Dictionary<string, byte[]> Actions = new Dictionary<string, byte[]>
    {
        { "mic",   new byte[] { CTRL, SHIFT, ALT, 0x78 } }, // Ctrl+Shift+Alt+F9  -> Discord: silenciar microfono (asignar en Discord)
        { "deaf",  new byte[] { CTRL, SHIFT, ALT, 0x79 } }, // Ctrl+Shift+Alt+F10 -> Discord: ensordecer (asignar en Discord)
        { "voldn", new byte[] { 0xAE } },                   // bajar volumen de Windows
        { "volup", new byte[] { 0xAF } },                   // subir volumen de Windows
    };
    static readonly Dictionary<string, bool> Toggles = new Dictionary<string, bool> { { "mic", false }, { "deaf", false } };
    static readonly object Lk = new object();

    static void Log(string m)
    {
        try { File.WriteAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + m + Environment.NewLine); } catch { }
    }

    static void Press(byte[] keys)
    {
        foreach (var k in keys) keybd_event(k, (byte)MapVirtualKey(k, 0), (k == 0xAE || k == 0xAF) ? EXTENDED : 0, UIntPtr.Zero);
        Thread.Sleep(30);
        for (int i = keys.Length - 1; i >= 0; i--)
            keybd_event(keys[i], (byte)MapVirtualKey(keys[i], 0), KEYUP | ((keys[i] == 0xAE || keys[i] == 0xAF) ? EXTENDED : 0), UIntPtr.Zero);
    }

    static string StateJson()
    {
        lock (Lk)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in Toggles) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value ? "true" : "false"); }
            return sb.Append('}').ToString();
        }
    }

    static void Send(HttpListenerResponse r, int code, string type, byte[] body)
    {
        r.StatusCode = code; r.ContentType = type;
        r.AddHeader("Cache-Control", "no-store");
        r.ContentLength64 = body.Length;
        r.OutputStream.Write(body, 0, body.Length);
        r.OutputStream.Close();
    }

    static void Handle(HttpListenerContext c)
    {
        try
        {
            string path = c.Request.Url.AbsolutePath;
            if (path == "/press")
            {
                string id = c.Request.QueryString["id"] ?? "";
                byte[] keys;
                if (Actions.TryGetValue(id, out keys))
                {
                    Press(keys);
                    lock (Lk) { if (Toggles.ContainsKey(id)) Toggles[id] = !Toggles[id]; }
                }
                Send(c.Response, 200, "application/json", Encoding.UTF8.GetBytes(StateJson()));
                return;
            }
            if (path == "/state") { Send(c.Response, 200, "application/json", Encoding.UTF8.GetBytes(StateJson())); return; }

            string rel = path == "/" ? "index.html" : path.TrimStart('/').Replace('/', '\\');
            string full = Path.GetFullPath(Path.Combine(WebDir, rel));
            if (!full.StartsWith(WebDir, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) { Send(c.Response, 404, "text/plain", Encoding.UTF8.GetBytes("no")); return; }
            string ext = Path.GetExtension(full).ToLowerInvariant();
            string type = ext == ".html" ? "text/html; charset=utf-8" : ext == ".svg" ? "image/svg+xml" : ext == ".png" ? "image/png" : ext == ".css" ? "text/css" : ext == ".js" ? "application/javascript" : "application/octet-stream";
            Send(c.Response, 200, type, File.ReadAllBytes(full));
        }
        catch { try { c.Response.Abort(); } catch { } }
    }

    // Mantiene el tunel USB: la tablet abre http://localhost:8790 y llega a esta PC por el cable.
    static void AdbLoop()
    {
        bool lastOk = false;
        while (true)
        {
            try
            {
                if (File.Exists(Adb))
                {
                    string list = Run(Adb, "reverse --list");
                    bool ok = list.Contains("tcp:" + Port);
                    if (!ok) { Run(Adb, "reverse tcp:" + Port + " tcp:" + Port); ok = Run(Adb, "reverse --list").Contains("tcp:" + Port); }
                    if (ok != lastOk) Log(ok ? "Tablet conectada por USB." : "Esperando tablet por USB...");
                    lastOk = ok;
                }
            }
            catch { }
            Thread.Sleep(lastOk ? 10000 : 4000);
        }
    }

    static string Run(string exe, string args)
    {
        var p = new Process();
        p.StartInfo = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        p.Start();
        string o = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        return o;
    }

    static void Main()
    {
        bool created;
        using (var m = new Mutex(true, "Local\\BotoneraTabletLanaro", out created))
        {
            if (!created) return;
            var l = new HttpListener();
            l.Prefixes.Add("http://localhost:" + Port + "/");
            l.Prefixes.Add("http://127.0.0.1:" + Port + "/");
            l.Start();
            Log("Iniciada. Esperando tablet por USB...");
            var t = new Thread(AdbLoop) { IsBackground = true }; t.Start();
            while (true)
            {
                var c = l.GetContext();
                ThreadPool.QueueUserWorkItem(_ => Handle(c));
            }
        }
    }
}
