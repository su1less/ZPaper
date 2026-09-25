using System;
using System.Runtime.InteropServices;
using System.Threading;
class PostClick
{
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    static IntPtr MkL(int lo, int hi) { return (IntPtr)((hi << 16) | (lo & 0xFFFF)); }
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        int x = int.Parse(a[1]), y = int.Parse(a[2]);
        PostMessage(h, 0x0201, (IntPtr)1, MkL(x, y));   // WM_LBUTTONDOWN, MK_LBUTTON
        Thread.Sleep(80);
        PostMessage(h, 0x0202, IntPtr.Zero, MkL(x, y)); // WM_LBUTTONUP
        Console.WriteLine("posted click " + x + "," + y + " to " + h);
    }
}
