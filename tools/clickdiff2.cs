// clickdiff2.exe <hwnd> <lx> <ly> - foreground the window FIRST (same dance as dock),
// then real click + diff, all in one process so no interleaved z-order churn
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
class ClickDiff2
{
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
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
        // dock foreground dance
        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        SetForegroundWindow(h);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        Thread.Sleep(500);
        IntPtr fg = GetForegroundWindow();
        uint fgp = 0, tp = 0; GetWindowThreadProcessId(fg, out fgp); GetWindowThreadProcessId(h, out tp);
        Console.WriteLine("foreground pid match: " + (fgp == tp));
        RECT r; GetWindowRect(h, out r);
        POINT orig; GetCursorPos(out orig);
        Bitmap before = Shot(r);
        SetCursorPos(r.L + lx, r.T + ly); Thread.Sleep(250);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(700);
        Bitmap after = Shot(r);
        SetCursorPos(orig.X, orig.Y);
        long d = Diff(before, after);
        Console.WriteLine("click(" + lx + "," + ly + ") diff=" + d + " -> " + (d > 1.0 ? "REACTED (input LIVE)" : "no change"));
        before.Dispose(); after.Dispose();
    }
}
