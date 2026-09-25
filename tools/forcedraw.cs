using System;
using System.Runtime.InteropServices;
using System.Threading;
class ForceDraw
{
    [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr rc, IntPtr rgn, uint flags);
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)long.Parse(a[0]);
        int ms = int.Parse(a[1]);
        uint f = 0x0100 | 0x0080 | 0x0004 | 0x0001;   // UPDATENOW|FRAME|ALLCHILDREN|INVALIDATE
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int n = 0;
        while (sw.ElapsedMilliseconds < ms) { RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, f); n++; Thread.Sleep(33); }
        Console.WriteLine("forced " + n + " redraws over " + ms + "ms");
    }
}
