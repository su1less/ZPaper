using System;
using System.Runtime.InteropServices;
using System.Text;
class DockVis
{
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static uint me;
    static void Main(string[] a)
    {
        me = (uint)System.Diagnostics.Process.GetProcessesByName("ZPapaer")[0].Id;
        Dump("current");
        if (a.Length > 0)
        {
            IntPtr t = (IntPtr)int.Parse(a[0]);
            SetForegroundWindow(t);
            System.Threading.Thread.Sleep(700);
            Dump("after fg normal window");
        }
    }
    static void Dump(string label)
    {
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == me)
            {
                RECT r; GetWindowRect(h, out r);
                long w = r.R - r.L, ht = r.B - r.T;
                if (r.T > 800 && ht < 200 && w > 100)
                    Console.WriteLine(label + ": bottomstrip hwnd=" + h + " vis=" + IsWindowVisible(h) + " rect=" + r.L + "," + r.T + "," + r.R + "," + r.B);
            }
            return true;
        }, IntPtr.Zero);
    }
}
