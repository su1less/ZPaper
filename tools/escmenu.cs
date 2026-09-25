// post ESC to every #32768 popup-menu window (dismiss open menus)
using System;
using System.Runtime.InteropServices;
using System.Text;
class EscMenu
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    static int hit;
    static void Main()
    {
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            StringBuilder cn = new StringBuilder(64);
            GetClassName(h, cn, 64);
            if (cn.ToString() == "#32768")
            {
                PostMessage(h, 0x0100, (IntPtr)0x1B, IntPtr.Zero);  // ESC down
                PostMessage(h, 0x0101, (IntPtr)0x1B, IntPtr.Zero);  // ESC up
                hit++;
            }
            return true;
        }, IntPtr.Zero);
        Console.WriteLine("esc sent to " + hit + " menu window(s)");
    }
}
