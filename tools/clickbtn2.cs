// clickbtn2.exe <hwnd> <localX> <localY> - foreground dance, real click at window-local point, state verdict
using System;
using System.Runtime.InteropServices;
using System.Threading;
class ClickBtn2
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    static string St(IntPtr h) { return "vis=" + IsWindowVisible(h) + " iconic=" + IsIconic(h) + " zoom=" + IsZoomed(h); }
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        int lx = int.Parse(a[1]), ly = int.Parse(a[2]);
        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        keybd_event(0x12, 0, 0, UIntPtr.Zero); SetForegroundWindow(h); keybd_event(0x12, 0, 2, UIntPtr.Zero);
        Thread.Sleep(400);
        RECT r; GetWindowRect(h, out r);
        string before = St(h);
        POINT o; GetCursorPos(out o);
        int sx = r.L + lx, sy = r.T + ly;
        SetCursorPos(sx, sy); Thread.Sleep(250);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(50);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(800);
        string after = St(h);
        SetCursorPos(o.X, o.Y);
        Console.WriteLine("click local(" + lx + "," + ly + ") screen(" + sx + "," + sy + ") " + before + " -> " + after + (before != after ? "  ** REACTED (INPUT LIVE)" : "  no change (dead?)"));
    }
}
