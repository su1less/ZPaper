// Focus diagnosis: who REALLY has input focus in WeChat's thread?
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class FocusTest
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint t, ref GUITHREADINFO g);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct GUITHREADINFO
    {
        public int cbSize; public int flags; public IntPtr hwndActive;
        public IntPtr hwndFocus; public IntPtr hwndCapture; public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize; public IntPtr hwndCaret; public RECT rcCaret;
    }

    static Bitmap Shot()
    {
        var b = new Bitmap(1920, 1080);
        using (var g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, b.Size);
        return b;
    }

    static double Diff(Bitmap a, Bitmap b, int x0, int y0, int w, int h)
    {
        long sum = 0; int n = 0;
        for (int y = y0; y < y0 + h; y += 4)
            for (int x = x0; x < x0 + w; x += 4)
            {
                Color ca = a.GetPixel(x, y), cb = b.GetPixel(x, y);
                sum += Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B);
                n++;
            }
        return n == 0 ? 0 : sum / (double)(n * 3);
    }

    static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    static void ReportFocus(IntPtr win)
    {
        uint tid; GetWindowThreadProcessId(win, out tid);
        uint fgTid; GetWindowThreadProcessId(GetForegroundWindow(), out fgTid);
        var gi = new GUITHREADINFO(); gi.cbSize = Marshal.SizeOf(gi);
        GetGUIThreadInfo(tid, ref gi);
        Console.WriteLine("  foreground thread == win thread: " + (tid == fgTid));
        Console.WriteLine("  hwndFocus == win: " + (gi.hwndFocus == win));
        if (gi.hwndFocus != IntPtr.Zero && gi.hwndFocus != win)
        {
            StringBuilder c = new StringBuilder(100);
            GetClassName(gi.hwndFocus, c, 100);
            Console.WriteLine("  focus is actually on: class '" + c + "' hwnd 0x" + gi.hwndFocus.ToString("X"));
        }
    }

    static int Main()
    {
        IntPtr win = IntPtr.Zero;
        var pids = new System.Collections.Generic.List<uint>();
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Weixin")) pids.Add((uint)p.Id);
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (!pids.Contains(pid)) return true;
            StringBuilder sb = new StringBuilder(200); GetWindowText(h, sb, 200);
            if (sb.ToString() == "Jett Su") { win = h; return false; }
            return true;
        }, IntPtr.Zero);
        if (win == IntPtr.Zero) { Console.WriteLine("window not found"); return 1; }
        Console.WriteLine("state BEFORE any click:");
        ReportFocus(win);

        Bitmap a = Shot();
        Click(395, 112);
        Thread.Sleep(900);
        Bitmap b = Shot();
        Console.WriteLine("after 1st click on row: pane diff = " + Diff(a, b, 940, 60, 400, 900).ToString("F1"));
        ReportFocus(win);

        a = Shot();
        Click(395, 112);
        Thread.Sleep(900);
        b = Shot();
        Console.WriteLine("after 2nd click on row: pane diff = " + Diff(a, b, 940, 60, 400, 900).ToString("F1"));
        ReportFocus(win);
        SetCursorPos(960, 300);
        return 0;
    }
}
