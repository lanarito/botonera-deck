// Botonera para la tablet (por USB).
// - Sirve la pagina web (web\index.html) en http://localhost:8790/
// - Mantiene "adb reverse tcp:8790" para que la tablet llegue a la PC por el cable USB (sin WiFi).
// - Cada toque llega como /press?id=xxx y ejecuta su accion: abrir programa, abrir URL, teclas o volumen.
// Las acciones de cada boton se definen abajo en "Buttons" (facil de editar; a futuro: botones.json).
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
    const byte CTRL = 0x11, SHIFT = 0x10, ALT = 0x12, WIN = 0x5B;
    const byte VOL_UP = 0xAF, VOL_DN = 0xAE, VOL_MUTE = 0xAD;

    class Act { public string type, val, args; public Act(string t, string v, string a) { type = t; val = v; args = a; } }

    // ===== BOTONES: id -> accion. Tipos: launch (programa), url (pagina), key (teclas), vol (up/down/mute) =====
    // OJO: las rutas de programas que NO estan en esta PC (Discord, Content Manager, SetupHub) se ajustan
    //      cuando estemos en la PC del simulador. Si una ruta no existe, el boton simplemente no hace nada.
    static readonly Dictionary<string, Act> Buttons = new Dictionary<string, Act>
    {
        { "discord",     new Act("launch", @"%LOCALAPPDATA%\Discord\Update.exe", "--processStart Discord.exe") },
        { "simhub",      new Act("launch", @"C:\Program Files (x86)\SimHub\SimHubWPF.exe", null) },
        { "youtube",     new Act("url",    "https://www.youtube.com", null) },
        { "google",      new Act("url",    "https://www.google.com", null) },
        { "ac",          new Act("launch", @"%LOCALAPPDATA%\AcTools Content Manager\Content Manager.exe", null) },
        { "setuphub",    new Act("launch", "", null) },   // ruta pendiente (PC del simu)
        { "antigravity", new Act("launch", @"%LOCALAPPDATA%\Programs\Antigravity\Antigravity.exe", null) },
        { "mic",         new Act("key",    "ctrl+shift+alt+f9", null) },   // asignar en Discord: silenciar micro
        { "deaf",        new Act("key",    "ctrl+shift+alt+f10", null) },  // asignar en Discord: ensordecer
        { "voldn",       new Act("vol",    "down", null) },
        { "volup",       new Act("vol",    "up", null) },
    };
    static readonly Dictionary<string, bool> Toggles = new Dictionary<string, bool> { { "mic", false }, { "deaf", false } };
    static readonly object Lk = new object();

    static void Log(string m) { try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + m + Environment.NewLine); } catch { } }

    static byte Vk(string t)
    {
        t = t.Trim().ToLowerInvariant();
        switch (t) { case "ctrl": return CTRL; case "shift": return SHIFT; case "alt": return ALT; case "win": return WIN;
                     case "space": return 0x20; case "enter": return 0x0D; case "tab": return 0x09; case "esc": return 0x1B; }
        if (t.Length == 1) { char c = t[0]; if (c >= 'a' && c <= 'z') return (byte)(0x41 + (c - 'a')); if (c >= '0' && c <= '9') return (byte)(0x30 + (c - '0')); }
        if (t.Length >= 2 && t[0] == 'f') { int n; if (int.TryParse(t.Substring(1), out n) && n >= 1 && n <= 24) return (byte)(0x70 + (n - 1)); }
        return 0;
    }

    static void SendKeys(string combo)
    {
        var keys = new List<byte>();
        foreach (var p in combo.Split('+')) { byte k = Vk(p); if (k != 0) keys.Add(k); }
        if (keys.Count == 0) return;
        foreach (var k in keys) keybd_event(k, (byte)MapVirtualKey(k, 0), 0, UIntPtr.Zero);
        Thread.Sleep(30);
        for (int i = keys.Count - 1; i >= 0; i--) keybd_event(keys[i], (byte)MapVirtualKey(keys[i], 0), KEYUP, UIntPtr.Zero);
    }

    static void Volume(string dir)
    {
        byte k = dir == "up" ? VOL_UP : (dir == "mute" ? VOL_MUTE : VOL_DN);
        keybd_event(k, (byte)MapVirtualKey(k, 0), EXTENDED, UIntPtr.Zero);
        keybd_event(k, (byte)MapVirtualKey(k, 0), EXTENDED | KEYUP, UIntPtr.Zero);
    }

    static void Launch(string path, string args)
    {
        try
        {
            string p = Environment.ExpandEnvironmentVariables(path ?? "");
            if (string.IsNullOrEmpty(p)) { Log("launch vacio (sin configurar)"); return; }
            var psi = new ProcessStartInfo(p) { UseShellExecute = true };
            if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
            try { string d = Path.GetDirectoryName(p); if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) psi.WorkingDirectory = d; } catch { }
            Process.Start(psi);
            Log("launch OK: " + p);
        }
        catch (Exception e) { Log("launch ERROR " + path + ": " + e.Message); }
    }

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); Log("url OK: " + url); }
        catch (Exception e) { Log("url ERROR " + url + ": " + e.Message); }
    }

    static void DoAction(string id)
    {
        Act a; if (!Buttons.TryGetValue(id, out a)) { Log("id desconocido: " + id); return; }
        switch (a.type)
        {
            case "launch": Launch(a.val, a.args); break;
            case "url":    OpenUrl(a.val); break;
            case "key":    SendKeys(a.val); break;
            case "vol":    Volume(a.val); break;
        }
        lock (Lk) { if (Toggles.ContainsKey(id)) Toggles[id] = !Toggles[id]; }
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
                if (id.Length > 0) DoAction(id);
                Send(c.Response, 200, "application/json", Encoding.UTF8.GetBytes(StateJson()));
                return;
            }
            if (path == "/state") { Send(c.Response, 200, "application/json", Encoding.UTF8.GetBytes(StateJson())); return; }

            string rel = path == "/" ? "index.html" : path.TrimStart('/').Replace('/', '\\');
            string full = Path.GetFullPath(Path.Combine(WebDir, rel));
            if (!full.StartsWith(WebDir, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) { Send(c.Response, 404, "text/plain", Encoding.UTF8.GetBytes("no")); return; }
            string ext = Path.GetExtension(full).ToLowerInvariant();
            string type = ext == ".html" ? "text/html; charset=utf-8" : ext == ".svg" ? "image/svg+xml" : ext == ".png" ? "image/png" : ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : ext == ".css" ? "text/css" : ext == ".js" ? "application/javascript" : ext == ".json" ? "application/json" : "application/octet-stream";
            Send(c.Response, 200, type, File.ReadAllBytes(full));
        }
        catch { try { c.Response.Abort(); } catch { } }
    }

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
