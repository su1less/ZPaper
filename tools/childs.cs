using System;
using System.Runtime.InteropServices;
using System.Text;
class Childs
{
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static void Main(string[] a)
    {
        IntPtr p = (IntPtr)int.Parse(a[0]);
        RECT r; GetWindowRect(p, out r);
        Console.WriteLine("parent rect (" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ")");
        EnumChildWindows(p, delegate(IntPtr h, IntPtr l)
        {
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            GetWindowRect(h, out r);
            Console.WriteLine("child " + h + " vis=" + IsWindowVisible(h) + " class='" + c + "' rect=(" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ")");
            return true;
        }, IntPtr.Zero);
    }
}
