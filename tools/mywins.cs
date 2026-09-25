using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class MyWins
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int idx);
    static void Main()
    {
        uint me = (uint)Process.GetProcessesByName("TechRainWallpaper")[0].Id;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint wp; GetWindowThreadProcessId(h, out wp);
            if (wp != me) return true;
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            IntPtr par = GetParent(h);
            string pcs = "";
            if (par != IntPtr.Zero)
            {
                StringBuilder pc = new StringBuilder(128); GetClassName(par, pc, 128);
                uint pp; GetWindowThreadProcessId(par, out pp);
                pcs = " parent=" + pc + "(pid " + pp + (pp == me ? " SELF" : "") + ")";
            }
            long ex = (long)GetWindowLongPtr(h, -20);
            Console.WriteLine("hwnd=" + h + " class=[" + c + "] vis=" + IsWindowVisible(h) + " child=" + (par != IntPtr.Zero) + pcs + " ex=0x" + ex.ToString("X8"));
            return true;
        }, IntPtr.Zero);
    }
}
