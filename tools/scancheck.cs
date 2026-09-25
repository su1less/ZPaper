using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class ScanCheck
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool inh, uint pid);
    [DllImport("kernel32.dll")] static extern bool QueryFullProcessImageName(IntPtr h, uint f, StringBuilder s, ref int n);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    static void Main()
    {
        string[] want = { "zcode", "firefox", "chrome", "todesk" };
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid == 0) return true;
            string pn = "?"; try { pn = Process.GetProcessById((int)pid).ProcessName.ToLower(); } catch { }
            if (Array.IndexOf(want, pn) < 0) return true;
            bool vis = IsWindowVisible(h);
            int tl = GetWindowTextLength(h);
            long ex = (long)GetWindowLongPtr(h, -20);
            bool tool = (ex & 0x80) != 0;
            bool noact = (ex & 0x08000000) != 0 && (ex & 0x00010000) == 0;
            int cloak = -1; try { DwmGetWindowAttribute(h, 14, out cloak, 4); } catch { }
            // exe path
            string exe = "";
            IntPtr ph = OpenProcess(0x1000, false, pid);
            if (ph != IntPtr.Zero)
            {
                StringBuilder sb = new StringBuilder(1024); int n = 1024;
                if (QueryFullProcessImageName(ph, 0, sb, ref n)) exe = sb.ToString(0, n);
                CloseHandle(ph);
            }
            bool pass = vis && tl > 0 && !tool && !noact && cloak == 0 && exe.Length > 0;
            Console.WriteLine(string.Format("{0,-9} hwnd={1} vis={2} title={3} tool={4} noact={5} cloak={6} exe=[{7}] -> {8}",
                pn, h, vis, tl, tool, noact, cloak, exe, pass ? "PASS" : "FILTERED"));
            return true;
        }, IntPtr.Zero);
    }
}
