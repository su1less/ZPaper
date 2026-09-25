using System;
using System.Runtime.InteropServices;
using System.Text;
class FastHideTest
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static uint me;
    static IntPtr dockHwnd;
    static bool DockVisible() { return dockHwnd != IntPtr.Zero && IsWindowVisible(dockHwnd); }
    static void Main(string[] a)
    {
        me = (uint)System.Diagnostics.Process.GetProcessesByName("TechRainWallpaper")[0].Id;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == me)
            {
                RECT r; GetWindowRect(h, out r);
                long w = r.R - r.L, ht = r.B - r.T;
                long ex = (long)GetWindowLongPtr(h, -20);
                if (w > 1500 && ht < 200 && (ex & 0x80000) != 0) { dockHwnd = h; Console.WriteLine("dock hwnd=" + h + " rect=" + r.L + "," + r.T + "," + r.R + "," + r.B); }
            }
            return true;
        }, IntPtr.Zero);
        IntPtr target = (IntPtr)int.Parse(a[0]);
        Console.WriteLine("before: dockVisible=" + DockVisible() + " targetZoomed=" + IsZoomed(target));
        SendMessage(target, 0x0112, (IntPtr)0xF030, IntPtr.Zero);   // SC_MAXIMIZE
        System.Threading.Thread.Sleep(800);
        Console.WriteLine("max+800ms: dockVisible=" + DockVisible() + " targetZoomed=" + IsZoomed(target));
        System.Threading.Thread.Sleep(1500);
        Console.WriteLine("max+2300ms: dockVisible=" + DockVisible());
        SendMessage(target, 0x0112, (IntPtr)0xF120, IntPtr.Zero);   // SC_RESTORE
        System.Threading.Thread.Sleep(800);
        Console.WriteLine("restored+800ms: dockVisible=" + DockVisible() + " zoomed=" + IsZoomed(target));
    }
}
