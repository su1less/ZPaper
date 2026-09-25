using System;
using System.Runtime.InteropServices;
using System.Text;
class ZOrder2
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static int n = 0;
    static void Main()
    {
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            string cs = c.ToString();
            if (cs == "Progman" || cs == "WorkerW")
            {
                RECT r; GetWindowRect(h, out r);
                long area = (long)(r.R - r.L) * (r.B - r.T);
                if (cs == "Progman" || area > 1920 * 1080 / 2)
                {
                    uint p; GetWindowThreadProcessId(h, out p);
                    Console.WriteLine("z#" + (n) + " " + cs + " " + h + " vis=" + IsWindowVisible(h) + " rect=" + r.L + "," + r.T + "," + r.R + "," + r.B + " pid=" + p);
                }
            }
            n++;
            return true;
        }, IntPtr.Zero);
        Console.WriteLine("(lower z# = more on top)");
    }
}
