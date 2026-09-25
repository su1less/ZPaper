// click an item in the popup menu (#32768): native menus only select an item
// once it has been hover-highlighted, so a WM_MOUSEMOVE leads the DOWN/UP
using System;
using System.Runtime.InteropServices;
using System.Threading;
class MenuClick
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowW(string c, string t);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    static IntPtr MkL(int lo, int hi) { return (IntPtr)((hi << 16) | (lo & 0xFFFF)); }
    static void Main(string[] a)
    {
        IntPtr h = a.Length > 2 ? (IntPtr)int.Parse(a[0]) : FindWindowW("#32768", null);
        if (h == IntPtr.Zero) { Console.WriteLine("no menu window"); return; }
        int x = int.Parse(a[a.Length > 2 ? 1 : 0]), y = int.Parse(a[a.Length > 2 ? 2 : 1]);
        PostMessage(h, 0x0200, IntPtr.Zero, MkL(x, y));   // WM_MOUSEMOVE (highlight)
        Thread.Sleep(150);
        PostMessage(h, 0x0200, IntPtr.Zero, MkL(x, y));   // again: menus track hover statefully
        Thread.Sleep(120);
        PostMessage(h, 0x0201, (IntPtr)1, MkL(x, y));     // DOWN
        Thread.Sleep(80);
        PostMessage(h, 0x0202, IntPtr.Zero, MkL(x, y));   // UP
        Console.WriteLine("menuclick " + x + "," + y + " -> " + h);
    }
}
