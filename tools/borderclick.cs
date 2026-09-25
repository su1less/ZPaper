// borderclick.exe <hwnd> <n> - n real single clicks on the window's left border edge
using System;
using System.Runtime.InteropServices;
using System.Threading;
class BorderClick
{
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        int n = int.Parse(a[1]);
        RECT r; GetWindowRect(h, out r);
        POINT orig; GetCursorPos(out orig);
        int x = r.L + 1, y = (r.T + r.B) / 2;   // left resize border, mid height
        SetCursorPos(x, y); Thread.Sleep(250);
        for (int i = 0; i < n; i++)
        {
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(50);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(350);
        }
        SetCursorPos(orig.X, orig.Y);
        Console.WriteLine("did " + n + " border clicks at (" + x + "," + y + ")");
    }
}
