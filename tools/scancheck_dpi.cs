using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class ScanCheckDpi
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
    static void Main()
    {
        SetProcessDPIAware();
        string[] want = { "zcode", "firefox", "chrome", "todesk" };
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid == 0) return true;
            string pn = "?"; try { pn = Process.GetProcessById((int)pid).ProcessName.ToLower(); } catch { }
            if (Array.IndexOf(want, pn) < 0) return true;
            if (!IsWindowVisible(h) || GetWindowTextLength(h) == 0) return true;
            long ex = (long)GetWindowLongPtr(h, -20);
            if ((ex & 0x80) != 0) return true;
            int cloak = -1; int hr = DwmGetWindowAttribute(h, 14, out cloak, 4);
            Console.WriteLine(pn + " hwnd=" + h + " hr=0x" + hr.ToString("X") + " cloak=" + cloak);
            return true;
        }, IntPtr.Zero);
    }
}
