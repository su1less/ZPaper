using System;
using System.Runtime.InteropServices;
using System.Text;
class WallFind
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    static IntPtr foundWall = IntPtr.Zero;
    static bool Cb(IntPtr h, IntPtr l)
    {
        IntPtr def = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (def != IntPtr.Zero)
        {
            Console.WriteLine("DefView parent hwnd=" + h);
            IntPtr wall = FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
            Console.WriteLine("  WorkerW after it: " + wall);
            if (wall != IntPtr.Zero) { foundWall = wall; return false; }
        }
        return true;
    }
    static void Main()
    {
        IntPtr progman = FindWindow("Progman", null);
        Console.WriteLine("Progman=" + progman);
        IntPtr r; SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 2, 1000, out r);
        Console.WriteLine("sent 0x052C");
        EnumWindows(Cb, IntPtr.Zero);
        Console.WriteLine("foundWall=" + foundWall);
        IntPtr w2 = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        Console.WriteLine("WorkerW under Progman=" + w2);
        if (w2 != IntPtr.Zero) { Console.WriteLine("vis=" + IsWindowVisible(w2)); }
    }
}
