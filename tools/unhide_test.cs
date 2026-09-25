using System;
using System.Runtime.InteropServices;
class UnhideTest
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static uint me; static IntPtr dockHwnd;
    static bool DockVisible() { return dockHwnd != IntPtr.Zero && IsWindowVisible(dockHwnd); }
    static void FindDock()
    {
        dockHwnd = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == me)
            {
                RECT r; GetWindowRect(h, out r);
                long w = r.R - r.L, ht = r.B - r.T;
                long ex = (long)GetWindowLongPtr(h, -20);
                if (w > 1500 && ht < 200 && (ex & 0x80000) != 0) dockHwnd = h;
            }
            return true;
        }, IntPtr.Zero);
    }
    static void Main(string[] a)
    {
        me = (uint)System.Diagnostics.Process.GetProcessesByName("TechRainWallpaper")[0].Id;
        IntPtr target = (IntPtr)int.Parse(a[0]);
        FindDock();
        Console.WriteLine("before: dockVisible=" + DockVisible());
        SetForegroundWindow(target);
        System.Threading.Thread.Sleep(700);
        FindDock();
        Console.WriteLine("fg-normal+700ms: dockVisible=" + DockVisible());
    }
}
