using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class ChildScan2
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static uint me;
    static bool Report(IntPtr h, string host)
    {
        uint wp; GetWindowThreadProcessId(h, out wp);
        if (wp == me)
        {
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            RECT r; GetWindowRect(h, out r);
            Console.WriteLine("OURS in " + host + ": " + h + " [" + c + "] vis=" + IsWindowVisible(h) + " rect=" + r.L + "," + r.T + "," + r.R + "," + r.B);
        }
        return true;
    }
    static void Main()
    {
        me = (uint)Process.GetProcessesByName("TechRainWallpaper")[0].Id;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            string cs = c.ToString();
            if (cs == "WorkerW" || cs == "Progman")
            {
                RECT r; GetWindowRect(h, out r);
                Console.WriteLine("host " + cs + " " + h + " vis=" + IsWindowVisible(h) + " rect=" + r.L + "," + r.T + "," + r.R + "," + r.B);
                EnumChildWindows(h, delegate(IntPtr ch, IntPtr ll) { Report(ch, cs); return true; }, IntPtr.Zero);
            }
            return true;
        }, IntPtr.Zero);
    }
}
