using System;
using System.Runtime.InteropServices;
using System.Threading;
class MinRestore
{
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        Console.WriteLine("start: vis=" + IsWindowVisible(h) + " iconic=" + IsIconic(h));
        ShowWindow(h, 6); Thread.Sleep(400);            // SW_MINIMIZE (external)
        Console.WriteLine("after SW_MINIMIZE: vis=" + IsWindowVisible(h) + " iconic=" + IsIconic(h));
        IntPtr r = SendMessage(h, 0x0112, (IntPtr)0xF120, IntPtr.Zero);   // SC_RESTORE via app
        Thread.Sleep(700);
        Console.WriteLine("after SC_RESTORE: vis=" + IsWindowVisible(h) + " iconic=" + IsIconic(h));
    }
}
