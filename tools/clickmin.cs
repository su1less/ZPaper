using System;
using System.Runtime.InteropServices;
using System.Threading;
class ClickMin
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        int x = int.Parse(a[1]), y = int.Parse(a[2]);
        POINT o; GetCursorPos(out o);
        SetCursorPos(x, y); Thread.Sleep(250);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(50);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(600);
        bool min = IsIconic(h);
        Console.WriteLine("after clicking minimize btn: iconic=" + min + (min ? "  -> INPUT LIVE" : "  -> dead"));
        if (min) { ShowWindow(h, 9); Thread.Sleep(400); Console.WriteLine("restored: iconic=" + IsIconic(h)); }
        SetCursorPos(o.X, o.Y);
    }
}
