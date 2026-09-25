// zstack.exe <x> <y> - every window whose rect contains the point, in z-order (top first)
using System;
using System.Runtime.InteropServices;
using System.Text;
class ZStack
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    static void Main(string[] a)
    {
        int x = int.Parse(a[0]), y = int.Parse(a[1]);
        Console.WriteLine("z-stack at (" + x + "," + y + "):");
        int rank = 0;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            RECT r; if (!GetWindowRect(h, out r)) return true;
            if (x < r.L || x > r.R || y < r.T || y > r.B) return true;
            long ex = (long)GetWindowLongPtr(h, -20);
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256);
            uint pid; GetWindowThreadProcessId(h, out pid);
            string proc = "?"; try { System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById((int)pid); proc = p.ProcessName; } catch {}
            Console.WriteLine(string.Format("#{0} hwnd={1} proc={2} vis={3} layered={4} transparent={5} toolwin={6} class='{7}' title='{8}'",
                rank++, h, proc, IsWindowVisible(h), (ex & 0x80000) != 0, (ex & 0x20) != 0, (ex & 0x80) != 0, c, t));
            return true;
        }, IntPtr.Zero);
        POINT p2 = new POINT(); p2.X = x; p2.Y = y;
        IntPtr w = WindowFromPoint(p2);
        Console.WriteLine("WindowFromPoint -> " + w);
    }
}
