using System;
using System.Runtime.InteropServices;
using System.Threading;
class ScrollBy
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    static void Main(string[] a)
    {
        int x = int.Parse(a[0]), y = int.Parse(a[1]), steps = int.Parse(a[2]);
        SetCursorPos(x, y); Thread.Sleep(250);
        int dir = steps > 0 ? -120 : 120;
        for (int i = 0; i < Math.Abs(steps); i++) { mouse_event(0x0800, 0, 0, (uint)dir, UIntPtr.Zero); Thread.Sleep(120); }
        Console.WriteLine("scrolled " + steps + " at " + x + "," + y);
    }
}
