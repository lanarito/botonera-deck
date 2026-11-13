// Botonera para la tablet (por USB).
// - Sirve la pagina web (web\index.html) en http://localhost:8790/
// - Lee los botones de botones.json (fuente unica; se puede editar sin recompilar).
// - Mantiene "adb reverse tcp:8790" solo, y se recupera del replug (reconnect + reabrir pagina).
// - /press?id=xxx ejecuta la accion del boton: launch (programa), url (pagina), key (teclas), vol (volumen).
// Compilar: csc /nologo /target:winexe /optimize /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:Botonera.exe Botonera.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class Botonera
{
    const int Port = 8790;
    static bool LanEnabled = false;
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

    static void Log(string m) { try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + m + Environment.NewLine); } catch { } }

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
        add("deaf", "AURICULARES", "SIN AUDIO", "svg:M12 3a9 9 0 0 0-9 9v6a3 3 0 0 0 3 3h2v-8H5v-1a7 7 0 0 1 14 0v1h-3v8h2a3 3 0 0 0 3-3v-6a9 9 0 0 0-9-9z", "vol", "mute", "", true);
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
    static bool Ext(byte k) { return (k >= 0x21 && k <= 0x2E) || k == WIN || k == 0x6F /*num/*/ || k == VOL_UP || k == VOL_DN || k == VOL_MUTE; }
    // Teclas por CODIGO FISICO (scancode, KEYEVENTF_SCANCODE): AC/CSP leen el teclado por DirectInput/raw input
    // y no siempre ven las teclas mandadas solo por codigo virtual. Se mantienen 60 ms (AC lee el teclado una vez por cuadro).
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTU { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTU u; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    const uint SCANCODE = 0x8;

    static void KeyScan(byte vk, bool up)
    {
        uint sc = MapVirtualKey(vk, 0);
        var inp = new INPUT[1];
        inp[0].type = 1;
        if (sc == 0) { inp[0].u.ki.wVk = vk; inp[0].u.ki.dwFlags = (up ? KEYUP : 0) | (Ext(vk) ? EXTENDED : 0); }
        else { inp[0].u.ki.wScan = (ushort)sc; inp[0].u.ki.dwFlags = SCANCODE | (up ? KEYUP : 0) | (Ext(vk) ? EXTENDED : 0); }
        SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
    }
    static string Foreground()
    {
        try { uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid); return Process.GetProcessById((int)pid).ProcessName; } catch { return "?"; }
    }
    static readonly Dictionary<string, DateTime> LastPress = new Dictionary<string, DateTime>();
    static void SendKeys(string combo)
    {
        var keys = new List<byte>();
        foreach (var p in combo.Split('+')) { byte k = Vk(p); if (k != 0) keys.Add(k); }
        if (keys.Count == 0) return;
        foreach (var k in keys) KeyScan(k, false);
        Thread.Sleep(60);
        for (int i = keys.Count - 1; i >= 0; i--) KeyScan(keys[i], true);
        Log("tecla " + combo + " -> ventana al frente: " + Foreground());
    }
    // Volumen GENERAL de Windows usando Core Audio directamente (Endpoint.SetMasterVolumeLevelScalar / SetMute)
    // up/down: sube/baja 4%. mute: silencia/activa con lectura real.
    static void Volume(string dir)
    {
        try
        {
            var ep = Endpoint();
            Guid g = Guid.Empty;
            if (dir == "mute")
            {
                bool m; ep.GetMute(out m);
                ep.SetMute(!m, ref g);
            }
            else
            {
                float lvl; ep.GetMasterVolumeLevelScalar(out lvl);
                float step = 0.04f;
                float next = dir == "up" ? Math.Min(1.0f, lvl + step) : Math.Max(0.0f, lvl - step);
                ep.SetMasterVolumeLevelScalar(next, ref g);
                if (dir == "up" && next > 0) ep.SetMute(false, ref g);
            }
        }
        catch (Exception ex) { Log("Volume CoreAudio err: " + ex.Message); }

        try
        {
            byte k = dir == "up" ? VOL_UP : (dir == "mute" ? VOL_MUTE : VOL_DN);
            keybd_event(k, (byte)MapVirtualKey(k, 0), EXTENDED, UIntPtr.Zero);
            keybd_event(k, (byte)MapVirtualKey(k, 0), EXTENDED | KEYUP, UIntPtr.Zero);
        }
        catch { }

        Thread.Sleep(50);
        Log("volumen " + dir + " -> " + VolInfo());
    }

    // ===== Core Audio (solo para LEER el estado real: mute y nivel del parlante/auricular por defecto) =====
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr p); int UnregisterControlChangeNotify(IntPtr p); int GetChannelCount(out uint n);
        int SetMasterVolumeLevel(float l, ref Guid g); int SetMasterVolumeLevelScalar(float l, ref Guid g);
        int GetMasterVolumeLevel(out float l); int GetMasterVolumeLevelScalar(out float l);
        int SetChannelVolumeLevel(uint c, float l, ref Guid g); int SetChannelVolumeLevelScalar(uint c, float l, ref Guid g);
        int GetChannelVolumeLevel(uint c, out float l); int GetChannelVolumeLevelScalar(uint c, out float l);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, ref Guid g); int GetMute([MarshalAs(UnmanagedType.Bool)] out bool m);
    }
    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice { int Activate(ref Guid id, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o); }
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator { int EnumAudioEndpoints(int f, int s, out IntPtr d); int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice d); }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

    static IAudioEndpointVolume Endpoint()
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDevice d; Marshal.ThrowExceptionForHR(en.GetDefaultAudioEndpoint(0, 1, out d));
        Guid g = typeof(IAudioEndpointVolume).GUID; object o;
        Marshal.ThrowExceptionForHR(d.Activate(ref g, 23, IntPtr.Zero, out o));
        return (IAudioEndpointVolume)o;
    }
    static bool? IsMuted() { try { bool m; Endpoint().GetMute(out m); return m; } catch { return null; } }
    static string VolInfo()
    {
        try { var e = Endpoint(); float l; bool m; e.GetMasterVolumeLevelScalar(out l); e.GetMute(out m); return Math.Round(l * 100) + "%" + (m ? " (MUTE)" : ""); }
        catch (Exception x) { return "? " + x.Message; }
    }

    // ===== Core Audio por programa (IAudioSessionManager2) =====
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        int GetAudioSessionControl(IntPtr g, uint f, out IntPtr sc);
        int GetSimpleAudioVolume(IntPtr g, uint f, out IntPtr sv);
        int GetSessionEnumerator(out IAudioSessionEnumerator se);
    }
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        int GetCount(out int count);
        int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        int GetState(out int state);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid g);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid g);
        int GetGroupingParam(out Guid g);
        int SetGroupingParam(ref Guid g, ref Guid e);
        int RegisterAudioSessionNotification(IntPtr c);
        int UnregisterAudioSessionNotification(IntPtr c);
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetProcessId(out uint pid);
        int IsSystemSoundsSession();
        int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        int SetMasterVolume(float level, ref Guid g);
        int GetMasterVolume(out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid g);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    static bool MatchProc(string procName, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        string f = filter.Trim();
        if (f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) f = f.Substring(0, f.Length - 4);
        return string.Equals(procName, f, StringComparison.OrdinalIgnoreCase) ||
               procName.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void ForEachAppSession(string filter, Action<ISimpleAudioVolume, string> fn)
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDevice d; Marshal.ThrowExceptionForHR(en.GetDefaultAudioEndpoint(0, 1, out d));
        Guid g = typeof(IAudioSessionManager2).GUID; object o;
        Marshal.ThrowExceptionForHR(d.Activate(ref g, 23, IntPtr.Zero, out o));
        var mgr = (IAudioSessionManager2)o;
        IAudioSessionEnumerator se;
        Marshal.ThrowExceptionForHR(mgr.GetSessionEnumerator(out se));
        int n; se.GetCount(out n);
        for (int i = 0; i < n; i++)
        {
            object sess = null;
            try
            {
                if (se.GetSession(i, out sess) != 0 || sess == null) continue;
                var sc2 = sess as IAudioSessionControl2;
                var sav = sess as ISimpleAudioVolume;
                if (sc2 == null || sav == null) continue;
                uint pid = 0; sc2.GetProcessId(out pid);
                if (pid == 0) continue;
                string pname = "";
                try { pname = Process.GetProcessById((int)pid).ProcessName; } catch { continue; }
                if (MatchProc(pname, filter)) fn(sav, pname);
            }
            catch { }
            finally { if (sess != null && Marshal.IsComObject(sess)) try { Marshal.ReleaseComObject(sess); } catch { } }
        }
    }

    static void AppVolume(string proc, string dir)
    {
        if (string.IsNullOrEmpty(proc)) return;
        string d = string.IsNullOrEmpty(dir) ? "mute" : dir.ToLowerInvariant();
        Guid ctx = Guid.Empty;
        int hits = 0;
        bool? targetMute = null;
        float lastLvl = 0f; bool lastMute = false;
        try
        {
            ForEachAppSession(proc, (sav, pname) =>
            {
                hits++;
                if (d == "mute")
                {
                    bool m; sav.GetMute(out m);
                    if (!targetMute.HasValue) targetMute = !m;
                    sav.SetMute(targetMute.Value, ref ctx);
                    lastMute = targetMute.Value;
                    sav.GetMasterVolume(out lastLvl);
                }
                else
                {
                    float l; sav.GetMasterVolume(out l);
                    l = Math.Max(0f, Math.Min(1f, l + (d == "up" ? 0.06f : -0.06f)));
                    sav.SetMasterVolume(l, ref ctx);
                    if (d == "up" && l > 0f) sav.SetMute(false, ref ctx);
                    lastLvl = l;
                    sav.GetMute(out lastMute);
                }
            });
            Log("appvol " + proc + " " + d + " -> " + (hits == 0 ? "sin sesion activa" : (Math.Round(lastLvl * 100) + "%" + (lastMute ? " (MUTE)" : "") + " [" + hits + "]")));
        }
        catch (Exception e) { Log("appvol err " + proc + ": " + e.Message); }
    }

    static bool? IsAppMuted(string proc)
    {
        if (string.IsNullOrEmpty(proc)) return null;
        int hits = 0; bool anyUnmuted = false;
        try
        {
            ForEachAppSession(proc, (sav, pname) =>
            {
                hits++;
                bool m; sav.GetMute(out m);
                if (!m) anyUnmuted = true;
            });
            return hits == 0 ? (bool?)null : !anyUnmuted;
        }
        catch { return null; }
    }

    static List<string> ActiveAudioApps()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { ForEachAppSession("", (sav, pname) => { if (!string.IsNullOrEmpty(pname)) set.Add(pname); }); } catch { }
        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
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
    static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Log("url OK: " + url);
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(300);
                try
                {
                    foreach (var name in new string[] { "chrome", "msedge", "firefox", "brave" })
                    {
                        var procs = Process.GetProcessesByName(name);
                        foreach (var p in procs)
                        {
                            IntPtr h = p.MainWindowHandle;
                            if (h != IntPtr.Zero)
                            {
                                if (IsIconic(h)) ShowWindow(h, 9);
                                else ShowWindow(h, 5);
                                SetForegroundWindow(h);
                                break;
                            }
                        }
                    }
                }
                catch { }
            });
        }
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
            case "appvol": AppVolume(a.val, a.args); break;
        }
        lock (Lk) { if (Toggles.ContainsKey(id)) Toggles[id] = !Toggles[id]; }
    }

    static string StateJson()
    {
        // Los toggles de "vol mute" y "appvol ... mute" muestran el estado REAL de Windows.
        bool? muted = null; bool asked = false;
        var appMuteCache = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
        lock (Lk)
        {
            foreach (var id in new List<string>(Toggles.Keys))
            {
                Act a; if (!Buttons.TryGetValue(id, out a)) continue;
                if (a.type == "vol" && a.val == "mute")
                {
                    if (!asked) { muted = IsMuted(); asked = true; }
                    if (muted.HasValue) Toggles[id] = muted.Value;
                }
                else if (a.type == "appvol" && (string.IsNullOrEmpty(a.args) || a.args.Equals("mute", StringComparison.OrdinalIgnoreCase)))
                {
                    bool? am;
                    if (!appMuteCache.TryGetValue(a.val, out am)) { am = IsAppMuted(a.val); appMuteCache[a.val] = am; }
                    if (am.HasValue) Toggles[id] = am.Value;
                }
            }
        }
        lock (Lk)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in Toggles) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value ? "true" : "false"); }
            // version de botones.json: si cambia (se guardo desde el editor o a mano) la tablet se recarga sola
            long ver = 0; try { ver = File.GetLastWriteTimeUtc(CfgPath).Ticks; } catch { }
            if (!first) sb.Append(',');
            sb.Append("\"_cfg\":\"").Append(ver).Append('"');
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
                // anti-doble: el mismo boton dos veces en < 300 ms es un toque repetido por la tablet, no del usuario
                // (el volumen +/- NO se filtra: tocarlo rapido varias veces es a proposito)
                bool dup = false;
                Act pa; bool isVolStep = Buttons.TryGetValue(id, out pa) && ((pa.type == "vol" && pa.val != "mute") || (pa.type == "appvol" && pa.args != "" && !pa.args.Equals("mute", StringComparison.OrdinalIgnoreCase)));
                if (!isVolStep) lock (Lk) { DateTime prev; dup = LastPress.TryGetValue(id, out prev) && (DateTime.Now - prev).TotalMilliseconds < 300; LastPress[id] = DateTime.Now; }
                if (dup) Log("ignorado doble toque: " + id);
                else if (id.Length > 0) { Log("toque: " + id); DoAction(id); }
                Send(c.Response, 200, "application/json", Encoding.UTF8.GetBytes(StateJson()));
                return;
            }
            if (path == "/state") { Send(c.Response, 200, "application/json", Encoding.UTF8.GetBytes(StateJson())); return; }
            if (path == "/audioapps") { SendJs(c, ActiveAudioApps()); return; }
            if (path == "/netinfo")
            {
                string ip = GetLocalIp();
                SendJs(c, new Dictionary<string, object> {
                    { "lan", LanEnabled },
                    { "ip", ip },
                    { "port", Port },
                    { "url", "http://" + ip + ":" + Port + "/index.html" }
                });
                return;
            }
            if (path == "/netenable")
            {
                bool ok = EnableLanAndRestart();
                SendJs(c, new Dictionary<string, object> { { "ok", ok } });
                return;
            }
            if (path.StartsWith("/editor/")) { Editor(c, path); return; }
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

    // ===== EDITOR en la PC (web/editor.html): elegir programas e iconos y guardar botones.json =====
    static readonly string IconDir = Path.Combine(WebDir, "icons");
    static string Js(object o) { return new JavaScriptSerializer().Serialize(o); }
    static void SendJs(HttpListenerContext c, object o) { Send(c.Response, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(Js(o))); }

    static void Editor(HttpListenerContext c, string path)
    {
        try
        {
            var q = c.Request.QueryString;
            if (path == "/editor/save" && c.Request.HttpMethod == "POST")
            {
                string body; using (var rd = new StreamReader(c.Request.InputStream, Encoding.UTF8)) body = rd.ReadToEnd();
                var test = new JavaScriptSerializer().Deserialize<Config>(body);   // que sea un botones.json valido
                if (test == null || test.pages == null || test.pages.Length == 0) throw new Exception("configuracion vacia");
                lock (CfgLk)
                {
                    if (File.Exists(CfgPath)) File.Copy(CfgPath, Path.Combine(Dir, "botones_backup.json"), true);
                    File.WriteAllText(CfgPath, body, new UTF8Encoding(false));
                }
                LoadConfig(true);
                Log("editor: botones.json guardado (backup en botones_backup.json)");
                SendJs(c, new Dictionary<string, object> { { "ok", true } }); return;
            }
            if (path == "/editor/apps") { SendJs(c, InstalledApps()); return; }
            if (path == "/editor/programa") { SendJs(c, ProgramaInfo(int.Parse(q["n"] ?? "-1"))); return; }
            if (path == "/editor/discord") { SendJs(c, DetectDiscord()); return; }
            if (path == "/editor/app") { SendJs(c, AppInfo(q["path"] ?? "")); return; }
            if (path == "/editor/pickexe")
            {
                string f = PickFile("Elegi el programa (o su acceso directo)", "Programas y accesos|*.exe;*.lnk;*.url;*.bat;*.cmd|Todos los archivos|*.*",
                                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), false);
                SendJs(c, f == null ? (object)new Dictionary<string, object> { { "cancel", true } } : AppInfo(f)); return;
            }
            if (path == "/editor/icons")
            {
                var l = new List<string>();
                if (Directory.Exists(IconDir)) foreach (var f in Directory.GetFiles(IconDir))
                    { string e = Path.GetExtension(f).ToLowerInvariant(); if (e == ".png" || e == ".jpg" || e == ".jpeg" || e == ".svg") l.Add("icons/" + Path.GetFileName(f)); }
                l.Sort(StringComparer.OrdinalIgnoreCase); SendJs(c, l); return;
            }
            if (path == "/editor/pickimg")
            {
                string f = PickFile("Elegi la imagen para el boton", "Imagenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.ico;*.svg|Todos los archivos|*.*",
                                    Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", "Downloads"), false);
                if (f == null) { SendJs(c, new Dictionary<string, object> { { "cancel", true } }); return; }
                SendJs(c, new Dictionary<string, object> { { "icon", ImportImage(f) } }); return;
            }
            Send(c.Response, 404, "text/plain", Encoding.UTF8.GetBytes("no"));
        }
        catch (Exception e)
        {
            Log("editor ERROR " + path + ": " + e.Message);
            SendJs(c, new Dictionary<string, object> { { "error", e.Message } });
        }
    }

    // --- ventana "Abrir" de Windows al frente (Botonera.exe no tiene ventanas: se usa una duena invisible y TopMost) ---
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    static readonly object PickLk = new object();
    static string PickFile(string title, string filter, string dir, bool deref)
    {
        lock (PickLk)
        {
            string res = null;
            var t = new Thread(() =>
            {
                using (var owner = new System.Windows.Forms.Form())
                {
                    owner.TopMost = true; owner.ShowInTaskbar = false; owner.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                    owner.StartPosition = System.Windows.Forms.FormStartPosition.Manual; owner.Location = new System.Drawing.Point(-4000, -4000); owner.Size = new System.Drawing.Size(1, 1);
                    owner.Show();
                    uint pid; uint fg = GetWindowThreadProcessId(GetForegroundWindow(), out pid), me = GetCurrentThreadId();
                    AttachThreadInput(me, fg, true); SetForegroundWindow(owner.Handle); owner.Activate(); AttachThreadInput(me, fg, false);
                    using (var d = new System.Windows.Forms.OpenFileDialog())
                    {
                        d.Title = title; d.Filter = filter; d.DereferenceLinks = deref; d.RestoreDirectory = true;
                        if (Directory.Exists(dir)) d.InitialDirectory = dir;
                        if (d.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK) res = d.FileName;
                    }
                }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            return res;
        }
    }

    // --- lista de programas instalados = accesos del menu Inicio y del Escritorio ---
    static List<Dictionary<string, object>> InstalledApps()
    {
        var dirs = new List<KeyValuePair<string, string>>();
        Action<Environment.SpecialFolder, string> add = (sf, g) => { try { string d = Environment.GetFolderPath(sf); if (d.Length > 0 && Directory.Exists(d)) dirs.Add(new KeyValuePair<string, string>(d, g)); } catch { } };
        add(Environment.SpecialFolder.DesktopDirectory, "Escritorio"); add(Environment.SpecialFolder.CommonDesktopDirectory, "Escritorio");
        add(Environment.SpecialFolder.StartMenu, "Menu Inicio"); add(Environment.SpecialFolder.CommonStartMenu, "Menu Inicio");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var res = new List<Dictionary<string, object>>();
        var skip = new Regex(@"(?i)uninstall|desinstal|readme|l[eé]ame|help|ayuda|manual|license|licencia|documentation|website|sitio web|release notes|changelog|^remove");
        foreach (var kv in dirs)
        {
            var files = new List<string>(); var pend = new Stack<string>(); pend.Push(kv.Key);
            while (pend.Count > 0)   // carpeta por carpeta: una sin permiso no corta toda la busqueda
            {
                string d = pend.Pop();
                try { files.AddRange(Directory.GetFiles(d)); } catch { }
                try { foreach (var s in Directory.GetDirectories(d)) pend.Push(s); } catch { }
            }
            foreach (var f in files)
            {
                string e = Path.GetExtension(f).ToLowerInvariant();
                if (e != ".lnk" && e != ".url" && e != ".exe") continue;
                string n = Path.GetFileNameWithoutExtension(f);
                if (skip.IsMatch(n) || !seen.Add(n)) continue;
                res.Add(new Dictionary<string, object> { { "name", n }, { "path", f }, { "group", kv.Value } });
            }
        }
        res.Sort((a, b) => string.Compare((string)a["name"], (string)b["name"], StringComparison.CurrentCultureIgnoreCase));
        return res;
    }

    // --- de un .exe / .lnk / .url saca: que hace el boton, nombre sugerido e icono (PNG grande en web\icons) ---
    static Dictionary<string, object> AppInfo(string f)
    {
        if (!File.Exists(f)) throw new Exception("no existe " + f);
        string e = Path.GetExtension(f).ToLowerInvariant();
        string type = "launch", val = f, args = "", label = Path.GetFileNameWithoutExtension(f);
        if (e == ".lnk")
        {
            try
            {
                Type st = Type.GetTypeFromProgID("WScript.Shell"); object sh = Activator.CreateInstance(st);
                object lnk = st.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { f });
                Type lt = lnk.GetType();
                string tp = (string)lt.InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, lnk, null);
                string ar = (string)lt.InvokeMember("Arguments", System.Reflection.BindingFlags.GetProperty, null, lnk, null);
                if (!string.IsNullOrEmpty(tp) && File.Exists(tp)) { val = tp; args = ar ?? ""; }   // si no (accesos "de tienda"), se abre el .lnk mismo
            }
            catch { }
        }
        else if (e == ".url")
        {
            foreach (var line in File.ReadAllLines(f)) if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) { type = "url"; val = line.Substring(4).Trim(); args = ""; }
        }
        else if (e == ".exe")
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(f);
                string d = (vi.FileDescription ?? "").Trim(), p = (vi.ProductName ?? "").Trim();
                if (d.Length > 1 && d.Length <= 18) label = d; else if (p.Length > 1 && p.Length <= 18) label = p;
            }
            catch { }
        }
        string icon = "";
        try { icon = SaveIcon(f, label); } catch (Exception x) { Log("icono ERROR " + f + ": " + x.Message); }
        return new Dictionary<string, object> { { "type", type }, { "val", val }, { "args", args }, { "label", label.ToUpperInvariant() }, { "icon", icon } };
    }

    static string SafeName(string s)
    {
        string n = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char ch in n)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            char l = char.ToLowerInvariant(ch);
            sb.Append((l >= 'a' && l <= 'z') || (l >= '0' && l <= '9') ? l : '_');
        }
        string r = Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        if (r.Length > 30) r = r.Substring(0, 30);
        return r.Length == 0 ? "icono" : r;
    }

    // icono grande (256) del sistema; si no hay, el chico. Se recorta lo transparente y se guarda cuadrado.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint attr; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string name; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string type; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string p, uint attr, ref SHFILEINFO fi, uint cb, uint flags);
    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IImageList { int Add(IntPtr a, IntPtr b, ref int i); int ReplaceIcon(int i, IntPtr h, ref int r); int SetOverlayImage(int a, int b); int Replace(int i, IntPtr a, IntPtr b);
                           int AddMasked(IntPtr a, int c, ref int i); int Draw(IntPtr p); int Remove(int i); int GetIcon(int i, int flags, ref IntPtr h); }
    [DllImport("shell32.dll", EntryPoint = "#727")] static extern int SHGetImageList(int list, ref Guid riid, out IImageList il);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    static string SaveIcon(string file, string label)
    {
        System.Drawing.Bitmap bmp = null;
        try
        {
            var fi = new SHFILEINFO();
            SHGetFileInfo(file, 0, ref fi, (uint)Marshal.SizeOf(fi), 0x4000 /*SYSICONINDEX*/);
            Guid g = typeof(IImageList).GUID; IImageList il;
            if (SHGetImageList(4 /*JUMBO 256*/, ref g, out il) == 0)
            {
                IntPtr h = IntPtr.Zero; il.GetIcon(fi.iIcon, 1 /*TRANSPARENT*/, ref h);
                if (h != IntPtr.Zero) { using (var ic = System.Drawing.Icon.FromHandle(h)) bmp = ic.ToBitmap(); DestroyIcon(h); }
            }
        }
        catch { bmp = null; }
        if (bmp == null) { using (var ic = System.Drawing.Icon.ExtractAssociatedIcon(file)) bmp = ic.ToBitmap(); }
        using (bmp)
        {
            // recortar el borde transparente (muchos programas traen el dibujo chico dentro del cuadro de 256)
            int x0 = bmp.Width, y0 = bmp.Height, x1 = -1, y1 = -1;
            for (int y = 0; y < bmp.Height; y++) for (int x = 0; x < bmp.Width; x++)
                if (bmp.GetPixel(x, y).A > 16) { if (x < x0) x0 = x; if (y < y0) y0 = y; if (x > x1) x1 = x; if (y > y1) y1 = y; }
            if (x1 < 0) throw new Exception("icono vacio");
            int side = Math.Max(x1 - x0 + 1, y1 - y0 + 1), outSide = Math.Max(side, 128);
            string name = "app_" + SafeName(label) + ".png";
            using (var o = new System.Drawing.Bitmap(outSide, outSide, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var gr = System.Drawing.Graphics.FromImage(o))
            {
                gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                int w = x1 - x0 + 1, hh = y1 - y0 + 1; float k = (float)outSide / side;
                gr.DrawImage(bmp, new System.Drawing.RectangleF((outSide - w * k) / 2, (outSide - hh * k) / 2, w * k, hh * k),
                             new System.Drawing.RectangleF(x0, y0, w, hh), System.Drawing.GraphicsUnit.Pixel);
                Directory.CreateDirectory(IconDir);
                o.Save(Path.Combine(IconDir, name), System.Drawing.Imaging.ImageFormat.Png);
            }
            return "icons/" + name;
        }
    }

    // imagen elegida por el usuario -> se copia a web\icons (PNG, maximo 256x256). SVG se copia tal cual.
    static string ImportImage(string f)
    {
        Directory.CreateDirectory(IconDir);
        string e = Path.GetExtension(f).ToLowerInvariant(), baseN = SafeName(Path.GetFileNameWithoutExtension(f));
        if (e == ".webp") throw new Exception("WEBP no sirve: abrila con Paint y Guardar como PNG");
        if (Path.GetFullPath(Path.GetDirectoryName(f)).TrimEnd('\\').Equals(IconDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return "icons/" + Path.GetFileName(f);
        string ext = e == ".svg" ? ".svg" : ".png", name = baseN + ext; int n = 2;
        while (File.Exists(Path.Combine(IconDir, name))) name = baseN + "_" + (n++) + ext;
        string dst = Path.Combine(IconDir, name);
        if (e == ".svg") { File.Copy(f, dst); return "icons/" + name; }
        using (var src = e == ".ico" ? new System.Drawing.Icon(f, 256, 256).ToBitmap() : new System.Drawing.Bitmap(f))
        {
            float k = Math.Min(1f, 256f / Math.Max(src.Width, src.Height));
            int w = Math.Max(1, (int)(src.Width * k)), h = Math.Max(1, (int)(src.Height * k));
            using (var o = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var gr = System.Drawing.Graphics.FromImage(o))
            {
                gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                gr.DrawImage(src, 0, 0, w, h);
                o.Save(dst, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        return "icons/" + name;
    }

    // ===== DISCORD: detectar los servidores DE QUIEN INSTALA (automatico, sin login) =====
    // 1) IDs + logos: cache local de Discord (URLs cdn.discordapp.com/icons/<servidor>/<hash>).
    // 2) Nombre + confirmar que sigue en el servidor: se abre discord://-/channels/<id> y se lee el titulo
    //    de la ventana ("#canal | Servidor - Discord"). Si el titulo no cambia, no es miembro -> se descarta.
    // 3) Se rearma la pagina DISCORD (solo sus botones discord://-/channels/...; el resto queda).
    // Arranca solo la primera vez si la pagina DISCORD no tiene servidores (kit recien instalado).
    static readonly string DcMarker = Path.Combine(Dir, "discord_detectado.txt");
    static readonly object DcLk = new object();

    static Process DiscordProc()
    {
        foreach (var n in new[] { "Discord", "DiscordPTB", "DiscordCanary" })
            foreach (var p in Process.GetProcessesByName(n)) { try { p.Refresh(); if (p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.Length > 0) return p; } catch { } }
        return null;
    }
    static string DiscordTitle() { var p = DiscordProc(); return p == null ? "" : p.MainWindowTitle; }
    static string GuildFromTitle(string t)
    {
        if (!t.EndsWith(" - Discord")) return null;
        t = t.Substring(0, t.Length - 10);
        int i = t.LastIndexOf(" | "); return i < 0 ? null : t.Substring(i + 3).Trim();
    }
    static string ShortLabel(string name)
    {
        var sb = new StringBuilder();
        foreach (char ch in name) { if (char.IsSurrogate(ch) || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherSymbol) continue; sb.Append(ch); }
        string s = Regex.Replace(sb.ToString(), @"\s+", " ").Trim(' ', '|', '-', '・', '.');
        var m = Regex.Match(s, @"^(.{3,}?)\s+[-|–—:]\s+");   // "E. Cavalli - Racing School..." -> "E. Cavalli"
        if (m.Success) s = m.Groups[1].Value;
        s = s.Trim(' ', '|', '-', '・').ToUpperInvariant();
        if (s.Length > 20) { int cut = s.LastIndexOf(' ', 20); s = cut > 3 ? s.Substring(0, cut) : s.Substring(0, 20); }
        return s.Length == 0 ? "SERVIDOR" : s;
    }

    // Discord nuevo ya no deja los logos en la cache: los servidores salen del Local Storage
    // ("selectedChannelIds": ultimo canal visto de cada servidor) y de sus logs ("Transitioning to /channels/<id>/").
    static List<string[]> DiscordGuildIds()
    {
        var res = DiscordCacheGuilds(); var seen = new HashSet<string>(); foreach (var g in res) seen.Add(g[0]);
        string ad = Environment.GetEnvironmentVariable("APPDATA") ?? "";
        var rxObj = new Regex("\"(?:selectedChannelIds|mostRecentSelectedTextChannelIds)\":\\{([^}]*)\\}");
        var rxKey = new Regex("\"(\\d{17,20})\":");
        var rxLog = new Regex(@"Transitioning to /channels/(\d{17,20})/");
        foreach (var flavor in new[] { "discord", "discordptb", "discordcanary" })
        {
            var files = new List<string>();
            try { files.AddRange(Directory.GetFiles(Path.Combine(ad, flavor + @"\Local Storage\leveldb"))); } catch { }
            try { files.AddRange(Directory.GetFiles(Path.Combine(ad, flavor + @"\logs"), "renderer_js*")); } catch { }
            foreach (var f in files)
            {
                string t;
                try { using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) using (var ms = new MemoryStream()) { fs.CopyTo(ms); t = Encoding.ASCII.GetString(ms.ToArray()); } }
                catch { continue; }
                foreach (Match m in rxObj.Matches(t)) foreach (Match k in rxKey.Matches(m.Groups[1].Value)) if (seen.Add(k.Groups[1].Value)) res.Add(new[] { k.Groups[1].Value, "" });
                foreach (Match m in rxLog.Matches(t)) if (seen.Add(m.Groups[1].Value)) res.Add(new[] { m.Groups[1].Value, "" });
            }
        }
        return res;
    }

    // --- logo recortado de la ventana de Discord: el servidor abierto tiene la rayita blanca a la izquierda ---
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    struct RECT { public int L, T, R, B; }
    static System.Drawing.Bitmap ShotWindow(IntPtr h)
    {
        RECT r; GetClientRect(h, out r); if (r.R < 200 || r.B < 200) return null;
        var b = new System.Drawing.Bitmap(r.R, r.B);
        using (var g = System.Drawing.Graphics.FromImage(b)) { var dc = g.GetHdc(); PrintWindow(h, dc, 3); g.ReleaseHdc(dc); }
        return b;
    }
    static int[] FindPill(System.Drawing.Bitmap b)   // [arriba, alto] de la rayita blanca mas larga en la columna 0
    {
        int best = 0, top = -1, run = 0;
        for (int y = 0; y < b.Height; y++)
        {
            var c = b.GetPixel(0, y);
            if (c.R > 200 && c.G > 200 && c.B > 200) { run++; if (run > best) { best = run; top = y - run + 1; } } else run = 0;
        }
        return best >= 20 && best <= 200 ? new[] { top, best } : null;
    }
    static System.Drawing.Bitmap RoundedIcon(Action<System.Drawing.Graphics, int> draw)
    {
        const int S = 128; var o = new System.Drawing.Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(o))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            int rr = 38; var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, rr, rr, 180, 90); path.AddArc(S - rr, 0, rr, rr, 270, 90); path.AddArc(S - rr, S - rr, rr, rr, 0, 90); path.AddArc(0, S - rr, rr, rr, 90, 90); path.CloseFigure();
            g.SetClip(path); draw(g, S);
        }
        return o;
    }
    static bool CaptureGuildIcon(IntPtr h, string dst)
    {
        using (var shot = ShotWindow(h))
        {
            if (shot == null) return false;
            var p = FindPill(shot); if (p == null) return false;
            int L = p[1], x0 = (int)Math.Round(0.4 * L);
            using (var o = RoundedIcon((g, S) => g.DrawImage(shot, new System.Drawing.Rectangle(0, 0, S, S), new System.Drawing.Rectangle(x0, p[0], L, L), System.Drawing.GraphicsUnit.Pixel)))
                o.Save(dst, System.Drawing.Imaging.ImageFormat.Png);
            return true;
        }
    }
    static void InitialsIcon(string name, string dst)   // como Discord para servidores sin logo
    {
        var words = Regex.Split(ShortLabel(name), @"[\s\-_.]+"); string ini = "";
        foreach (var w in words) if (w.Length > 0 && ini.Length < 3) ini += w[0];
        using (var o = RoundedIcon((g, S) =>
        {
            g.Clear(System.Drawing.Color.FromArgb(88, 101, 242));
            using (var f = new System.Drawing.Font("Segoe UI", ini.Length > 2 ? 34 : 44, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel))
            using (var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center })
                g.DrawString(ini.Length > 0 ? ini : "?", f, System.Drawing.Brushes.White, new System.Drawing.RectangleF(0, 0, S, S), sf);
        })) o.Save(dst, System.Drawing.Imaging.ImageFormat.Png);
    }
    static bool DiscordFront(IntPtr h) { return GetForegroundWindow() == h; }
    static void ZoomKey(bool inc) { byte k = inc ? (byte)0xBB : (byte)0xBD; keybd_event(CTRL, 0, 0, UIntPtr.Zero); keybd_event(k, 0, 0, UIntPtr.Zero); keybd_event(k, 0, KEYUP, UIntPtr.Zero); keybd_event(CTRL, 0, KEYUP, UIntPtr.Zero); Thread.Sleep(250); }

    static List<string[]> DiscordCacheGuilds()   // [id, hash] en orden de aparicion
    {
        var res = new List<string[]>(); var seen = new Dictionary<string, int>();
        var rx = new Regex(@"cdn\.discordapp\.com/icons/(\d{17,20})/((?:a_)?[0-9a-f]{32})");
        string ad = Environment.GetEnvironmentVariable("APPDATA") ?? "";
        var files = new List<FileInfo>();
        foreach (var flavor in new[] { "discord", "discordptb", "discordcanary" })
            foreach (var sub in new[] { @"Cache\Cache_Data", "Cache" })
            {
                string d = Path.Combine(ad, flavor + "\\" + sub);
                try { if (Directory.Exists(d)) files.AddRange(new DirectoryInfo(d).GetFiles()); } catch { }
            }
        files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
        var buf = new byte[8192];
        foreach (var f in files)
        {
            try
            {
                int n; using (var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) n = fs.Read(buf, 0, buf.Length);
                foreach (Match m in rx.Matches(Encoding.ASCII.GetString(buf, 0, n)))
                {
                    string id = m.Groups[1].Value, h = m.Groups[2].Value; int idx;
                    if (seen.TryGetValue(id, out idx)) res[idx][1] = h;   // el mas nuevo gana
                    else { seen[id] = res.Count; res.Add(new[] { id, h }); }
                }
            }
            catch { }
        }
        return res;
    }

    static List<Dictionary<string, object>> DetectDiscord()
    {
        lock (DcLk)
        {
            var guilds = DiscordGuildIds();
            Log("discord: " + guilds.Count + " servidores encontrados en los datos de Discord");
            if (guilds.Count == 0) throw new Exception("No encontre servidores de Discord en esta PC (abri Discord con tu cuenta y entra a tus servidores una vez)");
            // Discord abierto y con sesion iniciada
            if (DiscordProc() == null) Launch(@"%LOCALAPPDATA%\Discord\Update.exe", "--processStart Discord.exe");
            for (int i = 0; i < 90 && GuildFromTitle(DiscordTitle()) == null && !DiscordTitle().EndsWith(" - Discord"); i++) Thread.Sleep(1000);
            string start = DiscordTitle();
            if (!start.EndsWith(" - Discord")) throw new Exception("Discord no termino de abrir (o no tiene la sesion iniciada)");
            string startGuild = GuildFromTitle(start), backId = null;

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;   // TLS 1.2
            Directory.CreateDirectory(IconDir);
            var found = new List<Dictionary<string, object>>();
            var dproc = DiscordProc(); IntPtr hw = dproc != null ? dproc.MainWindowHandle : IntPtr.Zero;
            bool wasMin = hw != IntPtr.Zero && IsIconic(hw);
            if (wasMin) { ShowWindow(hw, 9); Thread.Sleep(800); }
            int zoomed = 0;
            string prev = start;
            try
            {
                foreach (var g in guilds)
                {
                    Process.Start(new ProcessStartInfo("discord://-/channels/" + g[0]) { UseShellExecute = true });
                    string t = prev;
                    for (int i = 0; i < 25 && t == prev; i++) { Thread.Sleep(200); t = DiscordTitle(); }
                    string name = t != prev ? GuildFromTitle(t) : null;
                    if (name == null) { Log("discord: " + g[0] + " descartado (no es miembro o sin titulo)"); continue; }
                    prev = t;
                    if (startGuild != null && name == startGuild) backId = g[0];
                    // Discord agrandado al 200% (5 pasos) para que el logo recortado salga nitido; solo si Discord esta al frente
                    if (zoomed == 0 && hw != IntPtr.Zero && DiscordFront(hw)) { for (int z = 0; z < 5 && DiscordFront(hw); z++) { ZoomKey(true); zoomed++; } }
                    string icon = "icons/dc_" + g[0] + ".png", dst = Path.Combine(IconDir, "dc_" + g[0] + ".png"), how = "";
                    if (g[1].Length > 0)
                        try { using (var wc = new WebClient()) wc.DownloadFile("https://cdn.discordapp.com/icons/" + g[0] + "/" + g[1] + ".png?size=128", dst); how = "logo original"; } catch { }
                    if (how == "" && hw != IntPtr.Zero)
                    {
                        Thread.Sleep(900);   // que termine de dibujar el servidor elegido
                        try { if (CaptureGuildIcon(hw, dst)) how = "logo de la ventana"; } catch (Exception x) { Log("discord: captura " + g[0] + " " + x.Message); }
                    }
                    if (how == "") { try { InitialsIcon(name, dst); how = "iniciales"; } catch { icon = "icons/discord.png"; } }
                    found.Add(new Dictionary<string, object> { { "id", "dc_" + g[0] }, { "label", ShortLabel(name) }, { "icon", icon }, { "type", "url" }, { "val", "discord://-/channels/" + g[0] } });
                    Log("discord: " + g[0] + " = " + name + " (" + how + ")");
                }
            }
            finally
            {
                // dejar Discord como estaba: servidor, zoom y minimizado
                Process.Start(new ProcessStartInfo("discord://-/channels/" + (backId ?? "@me")) { UseShellExecute = true });
                Thread.Sleep(1200);
                if (zoomed > 0)
                {
                    for (int i = 0; i < 20 && !DiscordFront(hw); i++) Thread.Sleep(250);
                    if (DiscordFront(hw)) { for (; zoomed > 0; zoomed--) ZoomKey(false); }
                    else Log("discord: no pude volver el zoom (Discord no esta al frente): apretar Ctrl+0 en Discord");
                }
                if (wasMin) { Thread.Sleep(400); ShowWindow(hw, 6); }
            }
            WriteDiscordPage(found);
            try { File.WriteAllText(DcMarker, DateTime.Now + " - " + found.Count + " servidores"); } catch { }
            return found;
        }
    }

    // rearma la pagina DISCORD en botones.json, conservando todo lo demas y el formato (un boton por linea)
    static void WriteDiscordPage(List<Dictionary<string, object>> servers)
    {
        lock (CfgLk)
        {
            var js = new JavaScriptSerializer();
            var root = (Dictionary<string, object>)js.DeserializeObject(File.ReadAllText(CfgPath));
            var pages = new List<object>((object[])root["pages"]);
            Dictionary<string, object> dp = null;
            foreach (Dictionary<string, object> p in pages) if ((p["name"] as string) == "DISCORD") dp = p;
            if (dp == null)
            {
                dp = new Dictionary<string, object> { { "name", "DISCORD" }, { "hidden", true }, { "buttons", new object[] {
                    new Dictionary<string, object> { { "id", "dc_volver" }, { "label", "INICIO" }, { "icon", "icons/em_back.png" }, { "type", "page" }, { "val", "INICIO" } } } } };
                pages.Add(dp); root["pages"] = pages.ToArray();
            }
            var keep = new List<object>();
            foreach (Dictionary<string, object> b in (object[])(dp.ContainsKey("buttons") ? dp["buttons"] : new object[0]))
                if (!((b.ContainsKey("val") ? b["val"] as string : "") ?? "").StartsWith("discord://-/channels/")) keep.Add(b);
            foreach (var s in servers) keep.Add(s);
            dp["buttons"] = keep.ToArray();
            if (File.Exists(CfgPath)) File.Copy(CfgPath, Path.Combine(Dir, "botones_backup.json"), true);
            File.WriteAllText(CfgPath, FormatConfig(root), new UTF8Encoding(false));
        }
        LoadConfig(true);
    }
    static string FormatConfig(Dictionary<string, object> root)
    {
        var js = new JavaScriptSerializer(); var sb = new StringBuilder("{\n  \"pages\": [\n");
        var pages = root["pages"] as object[] ?? new object[0];
        for (int i = 0; i < pages.Length; i++)
        {
            var p = (Dictionary<string, object>)pages[i];
            sb.Append("    {\n");
            foreach (var kv in p) if (kv.Key != "buttons") sb.Append("      ").Append(js.Serialize(kv.Key)).Append(": ").Append(js.Serialize(kv.Value)).Append(",\n");
            sb.Append("      \"buttons\": [\n");
            var bs = p.ContainsKey("buttons") ? (object[])p["buttons"] : new object[0];
            for (int k = 0; k < bs.Length; k++)
            {
                var parts = new List<string>();
                foreach (var kv in (Dictionary<string, object>)bs[k]) parts.Add(js.Serialize(kv.Key) + ": " + js.Serialize(kv.Value));
                sb.Append("        { ").Append(string.Join(", ", parts.ToArray())).Append(" }").Append(k < bs.Length - 1 ? ",\n" : "\n");
            }
            sb.Append("      ]\n    }").Append(i < pages.Length - 1 ? ",\n" : "\n");
        }
        return sb.Append("  ]\n}\n").ToString();
    }
    // ===== Programas de simracing (atajos.json > programas): buscar el instalado y sacar ruta + icono =====
    const string WheelSvg = "svg:M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4C16.1,4 19.5,7.1 20,11H17C16.5,9.9 14.4,9 12,9C9.6,9 7.5,9.9 7,11H4C4.5,7.1 7.9,4 12,4M4,13H7C7.2,14.3 9.1,16.6 11,17V20C7.4,19.6 4.4,16.6 4,13M13,17C14.9,16.6 16.7,14.3 17,13H20C19.6,16.6 16.6,19.6 13,20V17Z";
    static object[] Programas()
    {
        try { var r = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(WebDir, "atajos.json"))); return r["programas"] as object[] ?? new object[0]; }
        catch { return new object[0]; }
    }
    static string S(Dictionary<string, object> d, string k) { object o; return d.TryGetValue(k, out o) && o != null ? o.ToString() : ""; }
    // null si no esta instalado
    static Dictionary<string, object> FindPrograma(Dictionary<string, object> pr, List<Dictionary<string, object>> apps)
    {
        string buscar = S(pr, "buscar"), ruta = S(pr, "ruta"), path = null;
        if (buscar.Length > 0)
        {
            var rx = new Regex(buscar, RegexOptions.IgnoreCase);
            foreach (var a in apps) if (rx.IsMatch((string)a["name"])) { path = (string)a["path"]; break; }
        }
        if (path == null && ruta.Length > 0 && File.Exists(Environment.ExpandEnvironmentVariables(ruta))) path = Environment.ExpandEnvironmentVariables(ruta);
        if (path == null) return null;
        var info = AppInfo(path);
        if (S(pr, "label").Length > 0) info["label"] = S(pr, "label");
        return info;
    }
    static Dictionary<string, object> ProgramaInfo(int n)
    {
        var ps = Programas(); if (n < 0 || n >= ps.Length) throw new Exception("programa desconocido");
        var pr = (Dictionary<string, object>)ps[n];
        var info = FindPrograma(pr, InstalledApps());
        if (info != null) return info;
        return new Dictionary<string, object> { { "type", "launch" }, { "val", S(pr, "ruta") }, { "args", "" }, { "label", S(pr, "label") }, { "icon", WheelSvg }, { "noInstalado", true } };
    }
    // botones con "auto": "volante" y sin ruta -> se completan con la marca instalada (MOZA, Logitech, Simagic...)
    static void AutoVolante()
    {
        try
        {
            if (!File.Exists(CfgPath) || !File.ReadAllText(CfgPath).Contains("\"auto\"")) return;
            Dictionary<string, object> found = null; var apps = InstalledApps();
            foreach (Dictionary<string, object> pr in Programas())
            {
                object v; if (!pr.TryGetValue("volante", out v) || !(v is bool) || !(bool)v) continue;
                found = FindPrograma(pr, apps); if (found != null) break;
            }
            if (found == null) { Log("volante: no hay programa de volante instalado"); return; }
            lock (CfgLk)
            {
                var root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(CfgPath));
                bool changed = false;
                foreach (Dictionary<string, object> p in (object[])root["pages"])
                    foreach (Dictionary<string, object> b in (p.ContainsKey("buttons") ? (object[])p["buttons"] : new object[0]))
                    {
                        if (S(b, "auto") != "volante" || S(b, "val").Length > 0) continue;
                        b["label"] = found["label"]; b["icon"] = found["icon"]; b["type"] = found["type"]; b["val"] = found["val"];
                        if (S(found, "args").Length > 0) b["args"] = found["args"];
                        b.Remove("auto"); changed = true;
                        Log("volante: boton " + S(b, "id") + " = " + found["label"] + " (" + found["val"] + ")");
                    }
                if (changed) File.WriteAllText(CfgPath, FormatConfig(root), new UTF8Encoding(false));
            }
            LoadConfig(true);
        }
        catch (Exception e) { Log("volante automatico: " + e.Message); }
    }

    static void DiscordFirstRun()
    {
        AutoVolante();
        try
        {
            if (File.Exists(DcMarker) || !File.Exists(CfgPath)) return;
            string txt = File.ReadAllText(CfgPath);
            if (txt.Contains("discord://-/channels/")) { File.WriteAllText(DcMarker, "ya tenia servidores"); return; }   // botonera ya armada: no tocar
            if (!Directory.Exists(Path.Combine(Environment.GetEnvironmentVariable("APPDATA") ?? "", "discord"))) return;   // no tiene Discord
            Thread.Sleep(15000);   // dejar arrancar Windows/Discord
            DetectDiscord();
        }
        catch (Exception e) { Log("discord automatico: " + e.Message); }
    }

    // ===== adb: rearma el reverse solo + se recupera del replug =====
    // force-stop antes: si Jelly reusa una pestaña vieja la pagina queda en blanco (visto en la PC del simu 2026-11-11)
    // Se abre en "Webview Kiosk" (F-Droid, uk.nktnet.webviewkiosk): pantalla completa SIN barras desde el arranque
    // (configurado en la tablet: Home URL = esta pagina, Address bar/Floating toolbar = Hidden, Immersive = Always On, Inset = None).
    // Si la tablet no tiene Webview Kiosk (ej. tablet de un amigo recien instalada), abre el navegador que tenga.
    static readonly string OpenPage = "shell \"am force-stop org.lineageos.jelly; am force-stop uk.nktnet.webviewkiosk; sleep 1; " +
        "if pm list packages uk.nktnet.webviewkiosk | grep -q kiosk; then am start -a android.intent.action.VIEW -d http://localhost:" + Port + "/index.html -n uk.nktnet.webviewkiosk/.MainActivity; " +
        "else am start -a android.intent.action.VIEW -d http://localhost:" + Port + "/index.html; fi\"";
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

    static Mutex AppMutex;
    static HttpListener Listener;

    static string GetLocalIp()
    {
        try
        {
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                s.Connect("8.8.8.8", 65530);
                var ep = s.LocalEndPoint as IPEndPoint;
                if (ep != null) return ep.Address.ToString();
            }
        }
        catch { }
        try
        {
            foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) return a.ToString();
        }
        catch { }
        return "127.0.0.1";
    }

    static bool EnableLanAndRestart()
    {
        try
        {
            string cmds = "/c netsh http delete urlacl url=http://+:" + Port + "/ & " +
                          "netsh http add urlacl url=http://+:" + Port + "/ sddl=D:(A;;GX;;;WD) & " +
                          "netsh advfirewall firewall delete rule name=\"BotoneraDeck\" & " +
                          "netsh advfirewall firewall add rule name=\"BotoneraDeck\" dir=in action=allow protocol=TCP localport=" + Port;
            var p = Process.Start(new ProcessStartInfo("cmd.exe", cmds) { Verb = "runas", CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            if (p == null) return false;
            p.WaitForExit(15000);
            if (p.ExitCode != 0) return false;
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            Log("Red WiFi/Firewall habilitada. Reiniciando server...");
            new Thread(() => {
                Thread.Sleep(400);
                try { if (Listener != null) Listener.Stop(); } catch { }
                try { if (AppMutex != null) { AppMutex.ReleaseMutex(); AppMutex.Dispose(); AppMutex = null; } } catch { }
                try { Process.Start(exe); } catch { }
                Environment.Exit(0);
            }).Start();
            return true;
        }
        catch (Exception e) { Log("netenable err: " + e.Message); return false; }
    }

    static void Main()
    {
        bool created;
        AppMutex = new Mutex(true, "Local\\BotoneraTabletLanaro", out created);
        if (!created) return;
        LoadConfig(true);
        Listener = new HttpListener();
        try
        {
            Listener.Prefixes.Add("http://+:" + Port + "/");
            Listener.Start();
            LanEnabled = true;
        }
        catch
        {
            try { Listener.Close(); } catch { }
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://localhost:" + Port + "/");
            Listener.Prefixes.Add("http://127.0.0.1:" + Port + "/");
            Listener.Start();
            LanEnabled = false;
        }
        Log("Iniciada en puerto " + Port + (LanEnabled ? " (WiFi/LAN + USB, IP " + GetLocalIp() + ")" : " (solo localhost/USB; falta urlacl para WiFi)") + ".");
        var t = new Thread(AdbLoop) { IsBackground = true }; t.Start();
        new Thread(DiscordFirstRun) { IsBackground = true }.Start();   // volante + servidores de Discord, solos la primera vez
        while (true)
        {
            try
            {
                var c = Listener.GetContext();
                ThreadPool.QueueUserWorkItem(delegate { Handle(c); });
            }
            catch { break; }
        }
    }
}
