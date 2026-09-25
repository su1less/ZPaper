using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class ChildScan
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    static void Main()
    {
        uint me = (uint)Process.GetProcessesByName("TechRainWallpaper")[0].Id;
        IntPtr progman = FindWindow("Progman", null);
        Console.WriteLine("scanning Progman " + progman + " children for pid " + me);
        EnumChildWindows(progman, delegate(IntPtr h, IntPtr l)
        {
            uint wp; GetWindowThreadProcessId(h, out wp);
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            if (wp == me) Console.WriteLine("OURS: " + h + " [" + c + "] vis=" + IsWindowVisible(h));
            else if (c.ToString().Contains("DefView") || c.ToString().Contains("WorkerW"))
                Console.WriteLine("explorer child: " + h + " [" + c + "] vis=" + IsWindowVisible(h));
            return true;
        }, IntPtr.Zero);
    }
}
