using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
class Nudge2
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string cls, string name);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int ht, uint f);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }

    static void Main(string[] a)
    {
        uint wantPid = uint.Parse(a[0]);
        string mode = a.Length > 1 ? a[1] : "list";
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) { Console.WriteLine("no progman"); return; }
        IntPtr h = IntPtr.Zero, wall = IntPtr.Zero;
        while (true)
        {
            h = FindWindowEx(progman, h, null, null);
            if (h == IntPtr.Zero) break;
            uint pid; GetWindowThreadProcessId(h, out pid);
            StringBuilder sb = new StringBuilder(128);
            GetClassName(h, sb, 128);
            R r; GetWindowRect(h, out r);
            Console.WriteLine("child hwnd=" + h + " pid=" + pid + " cls=" + sb + " vis=" + IsWindowVisible(h)
                + " rect(" + r.L + "," + r.T + ")-(" + r.Rt + "," + r.B + ")");
            if (pid == wantPid) wall = h;
        }
        if (mode == "list" || wall == IntPtr.Zero) { Console.WriteLine("wall=" + wall); return; }
        R rr; GetWindowRect(wall, out rr);
        if (mode == "1")
        {
            SetWindowPos(wall, IntPtr.Zero, rr.L, rr.T, rr.Rt - rr.L + 1, rr.B - rr.T, 0x0002 | 0x0004 | 0x0010);
            Thread.Sleep(150);
            SetWindowPos(wall, IntPtr.Zero, rr.L, rr.T, rr.Rt - rr.L, rr.B - rr.T, 0x0002 | 0x0004 | 0x0010);
            Console.WriteLine("nudged resize on " + wall);
        }
        else if (mode == "2")
        {
            ShowWindow(wall, 0); Thread.Sleep(300); ShowWindow(wall, 8);
            Console.WriteLine("hide+show on " + wall);
        }
        else if (mode == "3")
        {
            SetWindowPos(wall, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            Thread.Sleep(120);
            SetWindowPos(wall, (IntPtr)1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            Console.WriteLine("z-flipped " + wall);
        }
    }
}
// mode "4" = TOPMOST and stay: appended via partial-class style switch below
