// dockdrag - synthesize a drag on the dock window via posted mouse messages
// (real cursor untouched). args: [startClientX] [steps] [stepPx]
using System;
using System.Runtime.InteropServices;
using System.Threading;
class DockDrag
{
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static IntPtr MkL(int lo, int hi) { return (IntPtr)((hi << 16) | (lo & 0xFFFF)); }

    static void Main(string[] a)
    {
        uint me = (uint)System.Diagnostics.Process.GetProcessesByName("ZPapaer")[0].Id;
        IntPtr dock = IntPtr.Zero; RECT r = new RECT();
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == me && IsWindowVisible(h))
            {
                RECT t; GetWindowRect(h, out t);
                if (t.T > 800 && t.B - t.T < 200 && t.R - t.L > 100) { dock = h; r = t; return false; }
            }
            return true;
        }, IntPtr.Zero);
        if (dock == IntPtr.Zero) { Console.WriteLine("dock window NOT found"); return; }
        int W = r.R - r.L;
        Console.WriteLine("dock hwnd=" + dock + " rect=" + r.L + "," + r.T + "," + r.R + "," + r.B + " W=" + W);

        int x = a.Length > 0 ? int.Parse(a[0]) : 330;
        int steps = a.Length > 1 ? int.Parse(a[1]) : 6;
        int px = a.Length > 2 ? int.Parse(a[2]) : 40;
        int y = 100;
        PostMessage(dock, 0x0200, IntPtr.Zero, MkL(x, y));          // WM_MOUSEMOVE hover
        Thread.Sleep(120);
        PostMessage(dock, 0x0201, (IntPtr)1, MkL(x, y));            // WM_LBUTTONDOWN
        Thread.Sleep(150);
        for (int i = 1; i <= steps; i++)
        {
            PostMessage(dock, 0x0200, (IntPtr)1, MkL(x + i * px, y));   // drag moves
            Thread.Sleep(60);
        }
        Thread.Sleep(200);
        PostMessage(dock, 0x0202, IntPtr.Zero, MkL(x + steps * px, y)); // WM_LBUTTONUP
        Console.WriteLine("drag posted: " + x + " -> " + (x + steps * px));
    }
}
