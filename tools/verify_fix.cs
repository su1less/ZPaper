// verify_fix.cs - exercises the EXACT ranking+activation sequence of the fixed dock
// (title-match ranking, junk blacklist, SW_RESTORE + ALT trick + SetForegroundWindow,
// NO cursor warps, NO screenshots) against the live apps. Usage: verify_fix <label> ...
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class VerifyFix
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    static string Title(IntPtr h)
    {
        int n = GetWindowTextLength(h);
        if (n <= 0) return "";
        StringBuilder sb = new StringBuilder(n + 1);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    static bool Junk(string t)
    {
        foreach (string p in new[] { "GDI+ Window", "TrayIcon", "MessageWindow", "Default IMC", "MSCTFIME", "桌面歌词" })
            if (t.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
    static bool LoginLike(string t)
    {
        return t.Contains("登录") || t.Contains("扫码") || t.Contains("切换账号") || t.Contains("添加账号")
            || t.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static int Main(string[] args)
    {
        // label -> process name prefixes (mirror of the dock Alias table)
        var cases = new Dictionary<string, string[]> {
            { "QQ", new[] { "QQ" } },
            { "微信", new[] { "Weixin", "WeChat", "WeChatAppEx" } },
            { "网易云音乐", new[] { "cloudmusic" } }
        };
        var labels = args.Length > 0 ? new List<string>(args) : new List<string>(cases.Keys);
        int fails = 0;
        foreach (string label in labels)
        {
            Console.WriteLine("=== " + label + " ===");
            var pids = new List<uint>();
            foreach (string n in cases[label])
                foreach (Process p in Process.GetProcessesByName(n)) pids.Add((uint)p.Id);
            if (pids.Count == 0) { Console.WriteLine("  not running, skip"); continue; }

            var wins = new List<IntPtr>();
            RECT rr = new RECT();
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint wp; GetWindowThreadProcessId(h, out wp);
                if (pids.Contains(wp) && GetWindowRect(h, out rr) && rr.R - rr.L > 0 && rr.B - rr.T > 0) wins.Add(h);
                return true;
            }, IntPtr.Zero);

            // dock ranking: titleMatch > firstPid > topLevel > titled > area, with penalties
            IntPtr best = IntPtr.Zero; long bestScore = long.MinValue;
            foreach (IntPtr h in wins)
            {
                string t = Title(h);
                bool vis = IsWindowVisible(h);
                long w = 0, ht = 0;
                GetWindowRect(h, out rr); w = rr.R - rr.L; ht = rr.B - rr.T;
                bool titleMatch = t.Length > 0 && t.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0;
                long score = (titleMatch ? 3500000L : 0) + (vis ? 150000L : 0)
                           + (GetWindowTextLength(h) > 0 ? 200000L : 0) + Math.Min(w * ht, 4000000L);
                if (Junk(t)) score -= 1850000000L;
                if (LoginLike(t)) score -= 1900000000L;
                if (!vis && !titleMatch) score -= 900000L;
                Console.WriteLine(string.Format("  cand {0} vis={1} '{2}' score={3}", h, vis, t.Length > 0 ? t : "(untitled)", score));
                if (score > bestScore) { bestScore = score; best = h; }
            }
            if (best == IntPtr.Zero || bestScore < 0) { Console.WriteLine("  NO usable candidate (would give up, no Process.Start)"); fails++; continue; }
            Console.WriteLine(string.Format("  -> activating {0} '{1}'", best, Title(best)));

            // dock activation sequence (no cursor moves)
            if (!IsWindowVisible(best)) { ShowWindow(best, 9); Thread.Sleep(150); }
            else if (IsIconic(best)) { ShowWindow(best, 9); Thread.Sleep(150); }
            SetWindowPos(best, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            SetWindowPos(best, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            uint tgt; GetWindowThreadProcessId(best, out tgt);
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            SetForegroundWindow(best);
            keybd_event(0x12, 0, 2, UIntPtr.Zero);
            Thread.Sleep(250);
            if (!(FgIs(tgt) && IsWindowVisible(best)))
            {
                keybd_event(0x12, 0, 0, UIntPtr.Zero);
                SetForegroundWindow(best);
                keybd_event(0x12, 0, 2, UIntPtr.Zero);
                Thread.Sleep(200);
            }
            IntPtr fg = GetForegroundWindow();
            uint fgPid = 0; GetWindowThreadProcessId(fg, out fgPid);
            bool ok = FgIs(tgt) && IsWindowVisible(best);
            Console.WriteLine(string.Format("  foreground='{0}' (pid {1}) targetPid={2} -> {3}",
                Title(fg), fgPid, tgt, ok ? "SUCCESS" : "FAIL"));
            if (!ok) fails++;
            Thread.Sleep(700);
        }
        Console.WriteLine(fails == 0 ? "ALL OK" : fails + " FAILURES");
        return fails;
    }

    static bool FgIs(uint pid)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        uint fp; GetWindowThreadProcessId(fg, out fp);
        return fp == pid;
    }
}
