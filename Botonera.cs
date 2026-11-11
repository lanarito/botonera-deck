// Botonera para la tablet (por USB).
// - Sirve la pagina web (web\index.html) en http://localhost:8790/
// - Lee los botones de botones.json (fuente unica; se puede editar sin recompilar).
// - Mantiene "adb reverse tcp:8790" solo, y se recupera del replug (reconnect + reabrir pagina).
// - /press?id=xxx ejecuta la accion del boton: launch (programa), url (pagina), key (teclas), vol (volumen).
// Compilar: csc /nologo /target:winexe /optimize /r:System.Web.Extensions.dll /out:Botonera.exe Botonera.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

class Botonera
{
    const int Port = 8790;
    static readonly string Dir = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string WebDir = Path.Combine(Dir, "web");
    static readonly string LogPath = Path.Combine(Dir, "estado_botonera.txt");
    static readonly string CfgPath = Path.Combine(Dir, "botones.json");
    static readonly string Adb = FindAdb();

    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
    const uint KEYUP = 0x2, EXTENDED = 0x1;
    const byte CTRL = 0x11, SHIFT = 0x10, ALT = 0x12, WIN = 0x5B;
    const byte VOL_UP = 0xAF, VOL_DN = 0xAE, VOL_MUTE = 0xAD;

    // ===== Modelo de configuracion (botones.json) =====
    public class BtnDef { public string id = "", label = "", labelOn = "", icon = "", type = "", val = "", args = ""; public bool toggle = false; }
    public class Page { public string name = ""; public BtnDef[] buttons = new BtnDef[0]; }
    public class Config { public Page[] pages = new Page[0]; }
    class Act { public string type, val, args; public Act(string t, string v, string a) { type = t; val = v; args = a; } }

    static readonly object CfgLk = new object();
    static Dictionary<string, Act> Buttons = new Dictionary<string, Act>();
    static Dictionary<string, bool> Toggles = new Dictionary<string, bool>();
    static DateTime CfgMtime = DateTime.MinValue;
    static readonly object Lk = new object();

    static string FindAdb()
    {
        string[] cands = new string[] {
            Path.Combine(Dir, @"tablet\platform-tools\adb.exe"),
            Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", @"dev\android-sdk\platform-tools\adb.exe"),
            Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", @"Android\Sdk\platform-tools\adb.exe"),
        };
        foreach (var c in cands) { try { if (File.Exists(c)) return c; } catch { } }
        return "adb.exe"; // en el PATH
    }

    static void Log(string m) { try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + m + Environment.NewLine); } catch { } }

    // ===== Config =====
    static Config DefaultConfig()
    {
        var L = new List<BtnDef>();
        Action<string, string, string, string, string, string, string, bool> add = (id, label, labelOn, icon, type, val, args, tog) =>
        { var b = new BtnDef(); b.id = id; b.label = label; b.labelOn = labelOn; b.icon = icon; b.type = type; b.val = val; b.args = args; b.toggle = tog; L.Add(b); };
        add("discord", "DISCORD", "", "icons/discord.png", "launch", @"%LOCALAPPDATA%\Discord\Update.exe", "--processStart Discord.exe", false);
        add("simhub", "SIMHUB", "", "icons/simhub.png", "launch", @"C:\Program Files (x86)\SimHub\SimHubWPF.exe", "", false);
        add("youtube", "YOUTUBE", "", "icons/youtube.png", "url", "https://www.youtube.com", "", false);
        add("google", "GOOGLE", "", "icons/google.png", "url", "https://www.google.com", "", false);
        add("ac", "ASSETTO", "", "icons/assetto.png", "launch", @"%LOCALAPPDATA%\AcTools Content Manager\Content Manager.exe", "", false);
        add("setuphub", "SETUPHUB", "", "svg:M3 17v2h6v-2H3zM3 5v2h10V5H3zm10 16v-2h8v-2h-8v-2h-2v6h2zM7 9v2H3v2h4v2h2V9H7zm14 4v-2H11v2h10zm-6-4h2V7h4V5h-4V3h-2v6z", "launch", "", "", false);
        add("antigravity", "ANTIGRAVITY", "", "icons/antigravity.png", "launch", @"%LOCALAPPDATA%\Programs\Antigravity\Antigravity.exe", "", false);
        add("mic", "MIC", "MIC MUTEADO", "svg:M12 14a3 3 0 0 0 3-3V6a3 3 0 0 0-6 0v5a3 3 0 0 0 3 3zm5-3a5 5 0 0 1-10 0H5a7 7 0 0 0 6 6.9V21h2v-3.1A7 7 0 0 0 19 11h-2z", "key", "ctrl+shift+alt+f9", "", true);
        add("deaf", "AURICULARES", "SIN AUDIO", "svg:M12 3a9 9 0 0 0-9 9v6a3 3 0 0 0 3 3h2v-8H5v-1a7 7 0 0 1 14 0v1h-3v8h2a3 3 0 0 0 3-3v-6a9 9 0 0 0-9-9z", "key", "ctrl+shift+alt+f10", "", true);
        add("voldn", "VOLUMEN \u2212", "", "svg:M4 9v6h4l5 5V4L8 9H4zm12.5 3A4.5 4.5 0 0 0 14 8v8a4.5 4.5 0 0 0 2.5-4z", "vol", "down", "", false);
        add("volup", "VOLUMEN +", "", "svg:M3 9v6h4l5 5V4L7 9H3zm13.5 3A4.5 4.5 0 0 0 14 8v8a4.5 4.5 0 0 0 2.5-4zM14 3.2v2.1a7 7 0 0 1 0 13.4v2.1a9 9 0 0 0 0-17.6z", "vol", "up", "", false);
        var pg = new Page(); pg.name = "INICIO"; pg.buttons = L.ToArray();
        var c = new Config(); c.pages = new Page[] { pg }; return c;
    }

    static void LoadConfig(bool force)
    {
        lock (CfgLk)
        {
            try
            {
                if (!File.Exists(CfgPath))
                    File.WriteAllText(CfgPath, new JavaScriptSerializer().Serialize(DefaultConfig()), Encoding.UTF8);
                DateTime mt = File.GetLastWriteTimeUtc(CfgPath);
                if (!force && mt == CfgMtime) return;
                string txt = File.ReadAllText(CfgPath);
                Config c = new JavaScriptSerializer().Deserialize<Config>(txt);
                if (c == null || c.pages == null) { Log("Config invalida, ignorada."); return; }
                var map = new Dictionary<string, Act>();
                foreach (var pg in c.pages)
                {
                    if (pg == null || pg.buttons == null) continue;
                    foreach (var b in pg.buttons)
                    {
                        if (b == null || b.id == null || b.id.Length == 0) continue;
                        map[b.id] = new Act(b.type ?? "", b.val ?? "", b.args ?? "");
                        if (b.toggle) lock (Lk) { if (!Toggles.ContainsKey(b.id)) Toggles[b.id] = false; }
                    }
                }
                Buttons = map; CfgMtime = mt;
                Log("Config cargada: " + map.Count + " botones.");
            }
            catch (Exception e) { Log("Config ERROR: " + e.Message); }
        }
    }

    // ===== Acciones =====
    static byte Vk(string t)
    {
        t = t.Trim().ToLowerInvariant();
        switch (t) { case "ctrl": return CTRL; case "shift": return SHIFT; case "alt": return ALT; case "win": return WIN;
                     case "space": return 0x20; case "enter": case "return": return 0x0D; case "tab": return 0x09; case "esc": case "escape": return 0x1B;
                     case "up": return 0x26; case "down": return 0x28; case "left": return 0x25; case "right": return 0x27;
                     case "backspace": return 0x08; case "del": case "delete": return 0x2E; case "home": return 0x24; case "end": return 0x23;
                     case "pageup": return 0x21; case "pagedown": return 0x22;
                     case "num+": case "numadd": return 0x6B; case "num-": case "numsub": return 0x6D;
                     case "num*": return 0x6A; case "num/": return 0x6F; case "num.": return 0x6E; }
        if (t.Length == 4 && t[0] == 'n' && t[1] == 'u' && t[2] == 'm') { char d = t[3]; if (d >= '0' && d <= '9') return (byte)(0x60 + (d - '0')); }
        if (t.Length == 1) { char c = t[0]; if (c >= 'a' && c <= 'z') return (byte)(0x41 + (c - 'a')); if (c >= '0' && c <= '9') return (byte)(0x30 + (c - '0')); }
        if (t.Length >= 2 && t[0] == 'f') { int n; if (int.TryParse(t.Substring(1), out n) && n >= 1 && n <= 24) return (byte)(0x70 + (n - 1)); }
        return 0;
    }
    static bool Ext(byte k) { return (k >= 0x21 && k <= 0x2E) || k == VOL_UP || k == VOL_DN || k == VOL_MUTE; }
    static void SendKeys(string combo)
    {
        var keys = new List<byte>();
        foreach (var p in combo.Split('+')) { byte k = Vk(p); if (k != 0) keys.Add(k); }
        if (keys.Count == 0) return;
        foreach (var k in keys) keybd_event(k, (byte)MapVirtualKey(k, 0), Ext(k) ? EXTENDED : 0, UIntPtr.Zero);
        Thread.Sleep(30);
        for (int i = keys.Count - 1; i >= 0; i--) keybd_event(keys[i], (byte)MapVirtualKey(keys[i], 0), KEYUP | (Ext(keys[i]) ? EXTENDED : 0), UIntPtr.Zero);
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
            if (p.Length == 0) { Log("launch vacio (sin configurar)"); return; }
            var psi = new ProcessStartInfo(p); psi.UseShellExecute = true;
            if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
            try { string d = Path.GetDirectoryName(p); if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) psi.WorkingDirectory = d; } catch { }
            Process.Start(psi); Log("launch OK: " + p);
        }
        catch (Exception e) { Log("launch ERROR " + path + ": " + e.Message); }
    }
    static void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); Log("url OK: " + url); } catch (Exception e) { Log("url ERROR " + url + ": " + e.Message); } }

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
                LoadConfig(false);
                if (id.Length > 0) DoAction(id);
                Send(c.Response, 200, "application/json", Encoding.UTF8.GetBytes(StateJson()));
                return;
            }
            if (path == "/state") { Send(c.Response, 200, "application/json", Encoding.UTF8.GetBytes(StateJson())); return; }
            if (path == "/config")
            {
                LoadConfig(false);
                string json = File.Exists(CfgPath) ? File.ReadAllText(CfgPath) : "{\"buttons\":[]}";
                Send(c.Response, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
                return;
            }

            string rel = path == "/" ? "index.html" : path.TrimStart('/').Replace('/', '\\');
            string full = Path.GetFullPath(Path.Combine(WebDir, rel));
            if (!full.StartsWith(WebDir, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) { Send(c.Response, 404, "text/plain", Encoding.UTF8.GetBytes("no")); return; }
            string ext = Path.GetExtension(full).ToLowerInvariant();
            string type = ext == ".html" ? "text/html; charset=utf-8" : ext == ".svg" ? "image/svg+xml" : ext == ".png" ? "image/png" : ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : ext == ".css" ? "text/css" : ext == ".js" ? "application/javascript" : ext == ".json" ? "application/json" : "application/octet-stream";
            Send(c.Response, 200, type, File.ReadAllBytes(full));
        }
        catch { try { c.Response.Abort(); } catch { } }
    }

    // ===== adb: rearma el reverse solo + se recupera del replug =====
    // force-stop antes: si Jelly reusa una pestaña vieja la pagina queda en blanco (visto en la PC del simu 2026-11-11)
    static readonly string OpenPage = "shell \"am force-stop org.lineageos.jelly; sleep 1; am start -a android.intent.action.VIEW -d http://localhost:" + Port + "/index.html -n org.lineageos.jelly/.MainActivity\"";
    static void AdbLoop()
    {
        bool lastOk = false;
        Log("adb: " + Adb);
        while (true)
        {
            try
            {
                string dev = Run(Adb, "devices");
                if (dev.Contains("\toffline")) { Run(Adb, "reconnect offline"); Thread.Sleep(1500); dev = Run(Adb, "devices"); }
                bool hasDevice = dev.Contains("\tdevice");
                bool ok = false;
                if (hasDevice)
                {
                    bool rev = Run(Adb, "reverse --list").Contains("tcp:" + Port);
                    if (!rev) { Run(Adb, "reverse tcp:" + Port + " tcp:" + Port); rev = Run(Adb, "reverse --list").Contains("tcp:" + Port); }
                    ok = rev;
                }
                if (ok && !lastOk) { Log("Tablet OK por USB. Reabriendo botonera..."); Run(Adb, OpenPage); }
                else if (!ok && lastOk) { Log("Tablet desconectada. Esperando..."); }
                lastOk = ok;
            }
            catch (Exception e) { Log("AdbLoop err: " + e.Message); }
            Thread.Sleep(lastOk ? 8000 : 3000);
        }
    }

    static string Run(string exe, string args)
    {
        try
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            p.Start();
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(6000);
            return o ?? "";
        }
        catch { return ""; }
    }

    static void Main()
    {
        bool created;
        using (var m = new Mutex(true, "Local\\BotoneraTabletLanaro", out created))
        {
            if (!created) return;
            LoadConfig(true);
            var l = new HttpListener();
            l.Prefixes.Add("http://localhost:" + Port + "/");
            l.Prefixes.Add("http://127.0.0.1:" + Port + "/");
            l.Start();
            Log("Iniciada en puerto " + Port + ".");
            var t = new Thread(AdbLoop) { IsBackground = true }; t.Start();
            while (true)
            {
                var c = l.GetContext();
                ThreadPool.QueueUserWorkItem(delegate { Handle(c); });
            }
        }
    }
}
