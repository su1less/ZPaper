using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
class Nudge
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int ht, uint f);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }

    static void Main(string[] a)
    {
        uint wantPid = uint.Parse(a[0]);
        IntPtr wall = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            if (!IsWindowVisible(h)) return true;
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid != wantPid) return true;
            if (GetParent(h) == IntPtr.Zero) return true;   // hosted = has a parent (Progman)
            R r; GetWindowRect(h, out r);
            if (r.Rt - r.L > 1500 && r.B - r.T > 800) { wall = h; return false; }
            return true;
        }, IntPtr.Zero);
        Console.WriteLine("wallpaper hwnd=" + wall);
        if (wall == IntPtr.Zero) return;
        string mode = a.Length > 1 ? a[1] : "1";
        R rr; GetWindowRect(wall, out rr);
        if (mode == "1")
        {
            // 1px resize round-trip: forces full re-layout + repaint
            SetWindowPos(wall, IntPtr.Zero, rr.L, rr.T, rr.Rt - rr.L + 1, rr.B - rr.T, 0x0002 | 0x0004 | 0x0010);
            Thread.Sleep(150);
            SetWindowPos(wall, IntPtr.Zero, rr.L, rr.T, rr.Rt - rr.L, rr.B - rr.T, 0x0002 | 0x0004 | 0x0010);
            Console.WriteLine("nudged resize");
        }
        else if (mode == "2")
        {
            ShowWindow(wall, 0); Thread.Sleep(300); ShowWindow(wall, 8);
            Console.WriteLine("hide+showna");
        }
        else if (mode == "3")
        {
            // z flip: to top of siblings then back below
            SetWindowPos(wall, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            Thread.Sleep(120);
            SetWindowPos(wall, (IntPtr)1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);   // HWND_BOTTOM
            Console.WriteLine("z-flipped");
        }
    }
}
