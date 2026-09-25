// list ALL windows (top-level) of a pid: hwnd, rect, visibility, styles
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class ZWins
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    static uint target;
    static void Main(string[] args)
    {
        target = uint.Parse(args[0]);
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if (p == target)
            {
                RECT r; GetWindowRect(h, out r);
                StringBuilder cn = new StringBuilder(256), tt = new StringBuilder(256);
                GetClassName(h, cn, 256); GetWindowText(h, tt, 256);
                Console.WriteLine(string.Format("hwnd={0} rect=({1},{2})-({3},{4}) vis={5} cls='{6}' title='{7}'",
                    h, r.L, r.T, r.R, r.B, IsWindowVisible(h), cn, tt));
            }
            return true;
        }, IntPtr.Zero);
    }
}
