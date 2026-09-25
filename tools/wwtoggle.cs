using System;
using System.Runtime.InteropServices;
using System.Text;
class WwToggle
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static int bigCount;
    static IntPtr bigOne;
    static void Scan(string label)
    {
        bigCount = 0; bigOne = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            StringBuilder c = new StringBuilder(64);
            if (GetClassName(h, c, 64) > 0 && c.ToString() == "WorkerW")
            {
                RECT r; GetWindowRect(h, out r);
                long area = (long)(r.R - r.L) * (r.B - r.T);
                if (area > 1920 * 1080 / 2) { bigCount++; bigOne = h; Console.WriteLine(label + " BIG WorkerW " + h + " vis=" + IsWindowVisible(h) + " rect=" + r.L + "," + r.T + "," + r.R + "," + r.B); }
            }
            return true;
        }, IntPtr.Zero);
        Console.WriteLine(label + ": big WorkerWs = " + bigCount);
    }
    static void Main()
    {
        IntPtr progman = FindWindow("Progman", null);
        Scan("before");
        for (int i = 1; i <= 4; i++)
        {
            IntPtr r; SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 2, 1000, out r);
            System.Threading.Thread.Sleep(800);
            Scan("after send #" + i);
        }
    }
}
