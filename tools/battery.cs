// Battery of wake patterns: find what makes the conversation row click work
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class Battery
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte sc, uint f, UIntPtr e);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);

    static Bitmap Shot()
    {
        var b = new Bitmap(1920, 1080);
        using (var g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, b.Size);
        return b;
    }

    static double Diff(Bitmap a, Bitmap b)
    {
        long sum = 0; int n = 0;
        for (int y = 60; y < 1000; y += 4)
            for (int x = 530; x < 1370; x += 4)
            {
                Color ca = a.GetPixel(x, y), cb = b.GetPixel(x, y);
                sum += Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B);
                n++;
            }
        return n == 0 ? 0 : sum / (double)(n * 3);
    }

    static void Click(int x, int y, int dwell = 150)
    {
        SetCursorPos(x, y);
        Thread.Sleep(dwell);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(900);
    }

    static void DblClick(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(80);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(900);
    }

    static double Try(string name, Action act)
    {
        var a = Shot();
        act();
        var b = Shot();
        double d = Diff(a, b);
        Console.WriteLine(name + " -> whole-pane diff = " + d.ToString("F1"));
        return d;
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

        // baseline: click row 1 directly
        Try("1. row1 single click", delegate { Click(395, 112); });
        // double-click row
        Try("2. row1 double click", delegate { DblClick(395, 112); });
        // click row 2
        Try("3. row2 (files) click", delegate { Click(395, 177); });
        // click chat pane center then row
        Try("4. pane click + row1", delegate { Click(940, 547); Click(395, 112); });
        // click pane center twice then row
        Try("5. pane x2 + row1", delegate { Click(940, 547); Click(940, 400); Click(395, 112); });
        // click search box, type, then click search RESULT row
        Try("6. search 'a' + click result", delegate
        {
            Click(370, 56);
            keybd_event(0x41, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            keybd_event(0x41, 0, 2, UIntPtr.Zero);
            Thread.Sleep(900);
            Click(395, 150);
            keybd_event(0x1B, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            keybd_event(0x1B, 0, 2, UIntPtr.Zero);
        });
        SetCursorPos(960, 300);
        return 0;
    }
}
