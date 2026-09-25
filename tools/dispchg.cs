using System;
using System.Runtime.InteropServices;
class DispChg
{
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    static void Main()
    {
        IntPtr lp = (IntPtr)((1080 << 16) | 1920);
        bool ok = PostMessage((IntPtr)0xFFFF, 0x007E, IntPtr.Zero, lp);   // HWND_BROADCAST WM_DISPLAYCHANGE
        Console.WriteLine("broadcast WM_DISPLAYCHANGE -> " + ok);
    }
}
