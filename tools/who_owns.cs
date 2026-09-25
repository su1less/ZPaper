using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

class WhoOwns
{
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(int x, int y);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    static void Main(string[] args)
    {
        int[][] pts = { new[] { 1061, 480 }, new[] { 960, 500 }, new[] { 700, 400 }, new[] { 960, 700 }, new[] { 500, 900 } };
        foreach (var pt in pts)
        {
            IntPtr h = WindowFromPoint(pt[0], pt[1]);
            uint pid = 0;
            GetWindowThreadProcessId(h, out pid);
            string pn = "?";
            try { pn = Process.GetProcessById((int)pid).ProcessName; } catch { }
            StringBuilder sb = new StringBuilder(150);
            GetWindowText(h, sb, 150);
            RECT r;
            GetWindowRect(h, out r);
            Console.WriteLine(string.Format("({0},{1}) -> {2} pid {3} title '{4}' rect ({5},{6})-({7},{8})",
                pt[0], pt[1], pn, pid, sb, r.L, r.T, r.R, r.B));
        }
        uint fpid;
        GetWindowThreadProcessId(GetForegroundWindow(), out fpid);
        string fn = "?";
        try { fn = Process.GetProcessById((int)fpid).ProcessName; } catch { }
        Console.WriteLine("foreground: " + fn + " (pid " + fpid + ")");
    }
}
