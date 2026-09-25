using System;
using System.Runtime.InteropServices;
using System.Threading;
class ClickXY
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    static void Main(string[] a)
    {
        int x = int.Parse(a[0]), y = int.Parse(a[1]);
        POINT o; GetCursorPos(out o);
        SetCursorPos(x, y); Thread.Sleep(300);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(500);
        SetCursorPos(o.X, o.Y);
        Console.WriteLine("clicked " + x + "," + y);
    }
}
