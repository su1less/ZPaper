using System;
using System.Runtime.InteropServices;
using System.Text;
class OvWalk
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    static void Dump(IntPtr h, string ind, int depth)
    {
        if (h == IntPtr.Zero || depth > 6) return;
        StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
        Console.WriteLine(ind + h + " [" + c + "]");
        IntPtr ch = FindWindowEx(h, IntPtr.Zero, null, null);
        while (ch != IntPtr.Zero) { Dump(ch, ind + "  ", depth + 1); ch = FindWindowEx(h, ch, null, null); }
    }
    static void Main()
    {
        IntPtr ov = FindWindow("NotifyIconOverflowWindow", null);
        Console.WriteLine("overflow: " + ov);
        if (ov != IntPtr.Zero) { ShowWindow(ov, 8); Dump(ov, "", 0); ShowWindow(ov, 0); }
    }
}
