using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class ToDeskProbe
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);

    static void Main()
    {
        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                if (p.ProcessName.ToLower().IndexOf("todesk") < 0) continue;
                Console.WriteLine("proc pid=" + p.Id + " name=" + p.ProcessName + " mainWin=" + p.MainWindowHandle);
            }
            catch { }
        }
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            string nm = null;
            try { nm = Process.GetProcessById((int)pid).ProcessName; } catch { }
            if (nm == null || nm.ToLower().IndexOf("todesk") < 0) return true;
            StringBuilder cn = new StringBuilder(128);
            GetClassName(h, cn, 128);
            StringBuilder tt = new StringBuilder(128);
            GetWindowText(h, tt, 128);
            Console.WriteLine("hwnd=" + h + " pid=" + pid + " cls=" + cn + " vis=" + IsWindowVisible(h)
                + " parent=" + GetParent(h) + " title='" + tt + "'");
            return true;
        }, IntPtr.Zero);
    }
}
