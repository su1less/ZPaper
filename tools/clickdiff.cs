// clickdiff.exe <hwnd> <localX> <localY> - real click at window-local point, pixel-diff before/after
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
class ClickDiff
{
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    static Bitmap Shot(RECT r)
    {
        Bitmap b = new Bitmap(r.R - r.L, r.B - r.T);
        using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.L, r.T, 0, 0, b.Size);
        return b;
    }
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
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        int lx = int.Parse(a[1]), ly = int.Parse(a[2]);
        RECT r; GetWindowRect(h, out r);
        POINT orig; GetCursorPos(out orig);
        Bitmap before = Shot(r);
        SetCursorPos(r.L + lx, r.T + ly); Thread.Sleep(200);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(600);
        Bitmap after = Shot(r);
        SetCursorPos(orig.X, orig.Y);
        long d = Diff(before, after);
        Console.WriteLine("click(" + lx + "," + ly + ") diff=" + d + " -> " + (d > 1.0 ? "REACTED (input LIVE)" : "no change"));
        before.Dispose(); after.Dispose();
    }
}
