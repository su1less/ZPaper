// wheeldiff.exe <hwnd> <lx> <ly> - foreground, real wheel scroll at window point, diff
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
class WheelDiff
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
        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        SetForegroundWindow(h);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        Thread.Sleep(500);
        RECT r; GetWindowRect(h, out r);
        POINT o; GetCursorPos(out o);
        Bitmap before = Shot(r);
        SetCursorPos(r.L + lx, r.T + ly); Thread.Sleep(250);
        for (int i = 0; i < 3; i++) { mouse_event(0x0800, 0, 0, (uint)120, UIntPtr.Zero); Thread.Sleep(150); }
        Thread.Sleep(500);
        Bitmap after = Shot(r);
        SetCursorPos(o.X, o.Y);
        Console.WriteLine("wheel diff=" + Diff(before, after) + " -> " + "check");
        before.Dispose(); after.Dispose();
    }
}
