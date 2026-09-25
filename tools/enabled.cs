using System;
using System.Runtime.InteropServices;
using System.Text;
class Enabled
{
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnableWindow(IntPtr h, bool en);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        Console.WriteLine("window " + h + " enabled=" + IsWindowEnabled(h));
        EnumChildWindows(h, delegate(IntPtr c, IntPtr l)
        {
            StringBuilder sb = new StringBuilder(128); GetClassName(c, sb, 128);
            Console.WriteLine("  child " + c + " '" + sb + "' enabled=" + IsWindowEnabled(c));
            return true;
        }, IntPtr.Zero);
        if (a.Length > 1 && a[1] == "fix")
        {
            EnableWindow(h, true);
            Console.WriteLine("EnableWindow(true) applied -> enabled=" + IsWindowEnabled(h));
        }
    }
}
