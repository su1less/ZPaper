using System;
using System.Runtime.InteropServices;
using System.Threading;
class ScrollPost
{
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    static IntPtr MkL(int lo, int hi) { return (IntPtr)((hi << 16) | (lo & 0xFFFF)); }
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        int steps = int.Parse(a[1]);   // positive = scroll down
        int delta = steps > 0 ? -120 : 120;
        IntPtr wp = (IntPtr)((delta & 0xFFFF) << 16);
        for (int i = 0; i < Math.Abs(steps); i++)
        {
            PostMessage(h, 0x020A, wp, MkL(700, 400));   // WM_MOUSEWHEEL
            Thread.Sleep(150);
        }
        Console.WriteLine("posted " + steps + " wheel steps to " + h);
    }
}
