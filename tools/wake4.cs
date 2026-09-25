// wake4.exe <hwnd> - hover liveness probe: real cursor move to left sidebar, pixel-diff the window
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
using System.Text;

class Wake4
{
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    static long Diff(Bitmap a, Bitmap b)
    {
        long sum = 0; int n = 0;
        for (int y = 0; y < a.Height; y += 3)
            for (int x = 0; x < a.Width; x += 3)
            {
                Color ca = a.GetPixel(x, y), cb = b.GetPixel(x, y);
                sum += Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B);
                n++;
            }
        return n == 0 ? 0 : sum / n;
    }

    static Bitmap Shot(RECT r)
    {
        Bitmap b = new Bitmap(r.R - r.L, r.B - r.T);
        using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.L, r.T, 0, 0, b.Size);
        return b;
    }

    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256);
        RECT r; GetWindowRect(h, out r);
        Console.WriteLine("== hover probe hwnd " + h + " '" + t + "' rect(" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ")");
        POINT orig; GetCursorPos(out orig);
        Bitmap before = Shot(r);
        // left sidebar: ~x+28, mid height (icon rail in QQ/WeChat dark UIs)
        SetCursorPos(r.L + 28, r.T + (r.B - r.T) / 2);
        Thread.Sleep(600);
        Bitmap during = Shot(r);
        SetCursorPos(orig.X, orig.Y);
        Thread.Sleep(400);
        Bitmap after = Shot(r);
        Console.WriteLine("  diff(before,during)=" + Diff(before, during) + "  diff(during,after)=" + Diff(during, after) + "  diff(before,after)=" + Diff(before, after));
        Console.WriteLine("  (hover-reactive if before/during diff > 1.0)");
        before.Dispose(); during.Dispose(); after.Dispose();
    }
}
