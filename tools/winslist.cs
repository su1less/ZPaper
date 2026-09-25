using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class WinsList
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }
    static string want;

    static void Main(string[] a)
    {
        want = a.Length > 0 ? a[0].ToLower() : null;
        IntPtr fg = GetForegroundWindow();
        StringBuilder fgb = new StringBuilder(256);
        GetWindowText(fg, fgb, 256);
        Console.WriteLine("FOREGROUND: hwnd=" + fg + " title='" + fgb + "'");
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            if (!IsWindowVisible(h) || GetWindowTextLength(h) == 0) return true;
            uint pid; GetWindowThreadProcessId(h, out pid);
            string name = null;
            try { name = Process.GetProcessById((int)pid).ProcessName; } catch { }
            if (name == null || (want != null && name.ToLower() != want)) return true;
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(h, sb, 256);
            R r; GetWindowRect(h, out r);
            Console.WriteLine("hwnd=" + h + " pid=" + pid + " [" + name + "] rect(" + r.L + "," + r.T + ")-(" + r.Rt + "," + r.B + ") title='" + sb + "'");
            return true;
        }, IntPtr.Zero);
    }
}
