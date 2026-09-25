// wake5.exe <hwnd> - deep liveness: WM_NCHITTEST probe (is the message loop pumping?)
// + multi-height real double-click maximize tests
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

class Wake5
{
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr wp, IntPtr lp, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    static IntPtr Htt(int x, int y)
    {
        IntPtr res;
        IntPtr hr = SendMessageTimeout(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, 0, 0, out res); // placeholder
        return hr;
    }

    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256);
        RECT r; GetWindowRect(h, out r);
        Console.WriteLine("== wake5 '" + t + "' rect(" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ") zoom=" + IsZoomed(h));
        int W = r.R - r.L, H = r.B - r.T;
        // NCHITTEST probe at grid points (screen coords)
        int[] xs = { r.L + 30, r.L + W / 2, r.L + W - 30 };
        int[] ys = { r.T + 5, r.T + 14, r.T + 25, r.T + 40, r.T + 60, r.T + H / 2 };
        foreach (int y in ys)
        {
            string line = "y=" + y + ": ";
            foreach (int x in xs)
            {
                IntPtr res;
                IntPtr ok = SendMessageTimeout(h, 0x0084, IntPtr.Zero, (IntPtr)((y << 16) | (x & 0xFFFF)), 2 /*ABORTIFHUNG*/, 1200, out res);
                line += (ok == IntPtr.Zero ? "TIMEOUT " : ("HT" + (int)res + " "));
            }
            Console.WriteLine(line);
        }
        // HT codes: 0 nowhere, 1 client, 2 caption, 4 left, 8 right, 12 top, ...
        // real double-clicks at several heights on x=30% (maximize toggle test)
        POINT orig; GetCursorPos(out orig);
        int dx = r.L + W * 3 / 10;
        foreach (int yOff in new int[] { 25, 40, 60 })
        {
            bool z0 = IsZoomed(h);
            SetCursorPos(dx, r.T + yOff); Thread.Sleep(150);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(45); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(90);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(45); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(450);
            bool z1 = IsZoomed(h);
            Console.WriteLine("dblclick y+" + yOff + ": zoom " + z0 + " -> " + z1 + (z1 != z0 ? "  REACTED" : "  no reaction"));
            if (z1 != z0)
            {   // restore
                SetCursorPos(dx, r.T + yOff); Thread.Sleep(150);
                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(45); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(90);
                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(45); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(400);
                Console.WriteLine("  restored: zoom=" + IsZoomed(h));
            }
        }
        SetCursorPos(orig.X, orig.Y);
    }
}
