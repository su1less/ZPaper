using System;
using System.Runtime.InteropServices;
using System.Text;
class ZOrder
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    static int n = 0;
    static void Main()
    {
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            string cs = c.ToString();
            if (cs == "Progman" || cs == "WorkerW" || cs == "SHELLDLL_DefView")
                Console.WriteLine("z#" + (n++) + " " + cs + " hwnd=" + h + " vis=" + IsWindowVisible(h));
            n++;
            return true;
        }, IntPtr.Zero);
        Console.WriteLine("(lower z# = closer to top)");
    }
}
