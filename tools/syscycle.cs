using System;
using System.Runtime.InteropServices;
using System.Threading;
class SysCycle
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        Console.WriteLine("before: vis=" + IsWindowVisible(h) + " iconic=" + IsIconic(h));
        SendMessage(h, 0x0112, (IntPtr)0xF020, IntPtr.Zero);   // SC_MINIMIZE
        Thread.Sleep(400);
        Console.WriteLine("after min: vis=" + IsWindowVisible(h) + " iconic=" + IsIconic(h));
        SendMessage(h, 0x0112, (IntPtr)0xF120, IntPtr.Zero);   // SC_RESTORE
        Thread.Sleep(700);
        Console.WriteLine("after restore: vis=" + IsWindowVisible(h) + " iconic=" + IsIconic(h));
    }
}
