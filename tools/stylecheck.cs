using System;
using System.Runtime.InteropServices;
using System.Text;
class StyleCheck
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static void Main()
    {
        IntPtr fg = GetForegroundWindow();
        StringBuilder c = new StringBuilder(64); GetClassName(fg, c, 64);
        StringBuilder t = new StringBuilder(256); GetWindowText(fg, t, 256);
        long st = (long)GetWindowLongPtr(fg, -16);
        long ex = (long)GetWindowLongPtr(fg, -20);
        RECT r; GetWindowRect(fg, out r);
        Console.WriteLine("fg [" + c + "] [" + t + "] rect=" + r.L + "," + r.T + "," + r.R + "," + r.B + " zoom=" + IsZoomed(fg));
        Console.WriteLine("style=0x" + st.ToString("X8") + "  WS_CAPTION=" + ((st & 0xC00000) != 0) + "  WS_THICKFRAME=" + ((st & 0x40000) != 0) + "  WS_POPUP=" + ((st & 0x80000000L) != 0) + "  WS_SIZEBOX...");
        Console.WriteLine("exstyle=0x" + ex.ToString("X8") + "  WS_EX_TOOLWINDOW=" + ((ex & 0x80) != 0) + "  WS_EX_APPWINDOW=" + ((ex & 0x40000) != 0));
    }
}
