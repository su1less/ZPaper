using System;
using System.Runtime.InteropServices;
using System.Text;
class FgCheck
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static void Main()
    {
        IntPtr fg = GetForegroundWindow();
        StringBuilder c = new StringBuilder(64); GetClassName(fg, c, 64);
        StringBuilder t = new StringBuilder(256); GetWindowText(fg, t, 256);
        RECT r; GetWindowRect(fg, out r);
        Console.WriteLine("fg=" + fg + " class=[" + c + "] title=[" + t + "] rect=" + r.L + "," + r.T + "," + r.R + "," + r.B + " zoomed=" + IsZoomed(fg));
        bool fs = r.L <= 0 && r.T <= 0 && r.R >= 1920 && r.B >= 1080;
        Console.WriteLine("judged fullscreen by dock logic: " + fs);
    }
}
