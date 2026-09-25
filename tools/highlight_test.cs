// Test: does single-click on a row move the SELECTION HIGHLIGHT?
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class HighlightTest
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
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

    static double Diff(Bitmap a, Bitmap b, int x0, int y0, int w, int h)
    {
        long sum = 0; int n = 0;
        for (int y = y0; y < y0 + h; y += 3)
            for (int x = x0; x < x0 + w; x += 3)
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
        Thread.Sleep(900);
    }

    static int Main()
    {
        Bitmap baseShot = Shot();

        Click(395, 112);            // row 1
        Bitmap s1 = Shot();
        Console.WriteLine("after click row1: list diff = " + Diff(baseShot, s1, 280, 80, 230, 900).ToString("F1")
            + ", pane diff = " + Diff(baseShot, s1, 940, 60, 400, 900).ToString("F1"));

        Click(395, 177);            // row 2
        Bitmap s2 = Shot();
        Console.WriteLine("after click row2: list diff = " + Diff(s1, s2, 280, 80, 230, 900).ToString("F1")
            + ", pane diff = " + Diff(s1, s2, 940, 60, 400, 900).ToString("F1"));

        // double-click row 2 and watch the pane
        SetCursorPos(395, 177);
        Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(80);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(1000);
        Bitmap s3 = Shot();
        Console.WriteLine("after double-click row2: pane diff = " + Diff(s2, s3, 940, 60, 400, 900).ToString("F1")
            + ", list diff = " + Diff(s2, s3, 280, 80, 230, 900).ToString("F1"));
        SetCursorPos(960, 300);
        return 0;
    }
}
