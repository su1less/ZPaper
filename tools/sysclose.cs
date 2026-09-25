using System;
using System.Runtime.InteropServices;
using System.Threading;
class SysClose
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        Console.WriteLine("before: vis=" + IsWindowVisible(h));
        SendMessage(h, 0x0112, (IntPtr)0xF060, IntPtr.Zero);  // WM_SYSCOMMAND SC_CLOSE
        Thread.Sleep(800);
        Console.WriteLine("after SC_CLOSE: vis=" + IsWindowVisible(h));
    }
}
