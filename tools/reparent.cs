using System;
using System.Runtime.InteropServices;
class ReParent
{
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]), p = (IntPtr)int.Parse(a[1]);
        IntPtr r = SetParent(h, p);
        MoveWindow(h, 0, 0, 1920, 1080, true);
        Console.WriteLine("reparented " + h + " -> " + p + " (old parent " + r + ")");
    }
}
