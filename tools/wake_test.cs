// Hypothesis: clicking a focus-grabbing widget (search box) wakes the app's focus.
// Then ESC closes the search and the whole window becomes interactive.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class WakeTest
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte sc, uint f, UIntPtr e);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint t, ref GUITHREADINFO g);
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

    static void FocusReport(IntPtr win)
    {
        uint tid; GetWindowThreadProcessId(win, out tid);
        var gi = new GUITHREADINFO(); gi.cbSize = Marshal.SizeOf(gi);
        GetGUIThreadInfo(tid, ref gi);
        Console.WriteLine("  hwndFocus: 0x" + gi.hwndFocus.ToString("X") + " (win=0x" + win.ToString("X") + ")");
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

        Console.WriteLine("step 1: click the SEARCH BOX (370,56)");
        Click(370, 56);
        Thread.Sleep(600);
        FocusReport(win);

        Console.WriteLine("step 2: press ESC to close any search");
        keybd_event(0x1B, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        keybd_event(0x1B, 0, 2, UIntPtr.Zero);
        Thread.Sleep(600);

        Console.WriteLine("step 3: click conversation row 1 (395,112)");
        Bitmap a = Shot();
        Click(395, 112);
        Thread.Sleep(1200);
        Bitmap b = Shot();
        double d = Diff(a, b, 940, 60, 400, 900);
        Console.WriteLine("chat pane diff = " + d.ToString("F1"));
        Console.WriteLine(d > 6 ? "SUCCESS: row click works after search-box wake" : "row click still dead");
        SetCursorPos(960, 300);
        return 0;
    }
}
