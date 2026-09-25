using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class InspectWins
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    static void Main(string[] args)
    {
        string[] names = args.Length > 0 ? args : new[] { "QQ", "Weixin", "WeChatAppEx", "cloudmusic" };
        var targets = new Dictionary<uint, string>();
        foreach (var n in names)
            try { foreach (var p in Process.GetProcessesByName(n)) targets[(uint)p.Id] = n; } catch { }
        foreach (var kv in targets) Console.WriteLine("PID " + kv.Key + " = " + kv.Value);
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            string proc;
            if (!targets.TryGetValue(pid, out proc)) return true;
            RECT r;
            if (!GetWindowRect(h, out r)) return true;
            int w = r.R - r.L, ht = r.B - r.T;
            if (w < 300 || ht < 200) return true;
            StringBuilder t = new StringBuilder(150); GetWindowText(h, t, 150);
            StringBuilder c = new StringBuilder(100); GetClassName(h, c, 100);
            long ex = (long)GetWindowLongPtr(h, -20);
            IntPtr owner = GetWindow(h, 4);
            Console.WriteLine(string.Format("{0} hwnd={1} {2}x{3} pos({4},{5}) vis={6} APPWIN={7} LAYERED={8} TRANSP={9} TOOLW={10} NOACT={11} owner0={12} cls='{13}' title='{14}'",
                proc, h, w, ht, r.L, r.T, IsWindowVisible(h),
                (ex & 0x40000) != 0, (ex & 0x80000) != 0, (ex & 0x20) != 0,
                (ex & 0x80) != 0, (ex & 0x08000000) != 0,
                owner == IntPtr.Zero, c.ToString(), t.ToString()));
            return true;
        }, IntPtr.Zero);
    }
}
