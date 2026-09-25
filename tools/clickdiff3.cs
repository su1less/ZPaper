// clickdiff3.exe <hwnd> <lx> <ly> - foreground, report WindowFromPoint at click spot,
// real click, window diff AND full-screen diff
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
class ClickDiff3
{
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    static Bitmap Shot(int x, int y, int w, int h)
    {
        Bitmap b = new Bitmap(w, h);
        using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(x, y, 0, 0, b.Size);
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
        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        SetForegroundWindow(h);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        Thread.Sleep(500);
        IntPtr fg = GetForegroundWindow();
        uint fgp = 0, tp = 0; GetWindowThreadProcessId(fg, out fgp); GetWindowThreadProcessId(h, out tp);
        RECT r; GetWindowRect(h, out r);
        int sx = r.L + lx, sy = r.T + ly;
        POINT cp = new POINT(); cp.X = sx; cp.Y = sy;
        IntPtr under = WindowFromPoint(cp);
        uint up = 0; GetWindowThreadProcessId(under, out up);
        Console.WriteLine("fgMatch=" + (fgp == tp) + "  WindowFromPoint(" + sx + "," + sy + ") -> " + under + " pid " + up + (up == tp ? " (target proc)" : " (OTHER PROC!)"));
        POINT orig; GetCursorPos(out orig);
        Bitmap wBefore = Shot(r.L, r.T, r.R - r.L, r.B - r.T);
        Bitmap fBefore = Shot(0, 0, 1920, 1080);
        SetCursorPos(sx, sy); Thread.Sleep(250);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(700);
        Bitmap wAfter = Shot(r.L, r.T, r.R - r.L, r.B - r.T);
        Bitmap fAfter = Shot(0, 0, 1920, 1080);
        SetCursorPos(orig.X, orig.Y);
        Console.WriteLine("window diff=" + Diff(wBefore, wAfter) + "  fullscreen diff=" + Diff(fBefore, fAfter));
        wBefore.Dispose(); wAfter.Dispose();
        fBefore.Dispose(); fAfter.Dispose();
    }
}
