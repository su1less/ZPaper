using System;
using System.Runtime.InteropServices;
class WhoPt
{
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(P p);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [StructLayout(LayoutKind.Sequential)] struct P { public int x, y; public P(int a, int b) { x = a; y = b; } }
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }
    [STAThread]
    static void Main(string[] a)
    {
        foreach (string arg in a)
        {
            string[] parts = arg.Split(',');
            P pt = new P(int.Parse(parts[0]), int.Parse(parts[1]));
            IntPtr w = WindowFromPoint(pt);
            uint pid; GetWindowThreadProcessId(w, out pid);
            System.Text.StringBuilder t = new System.Text.StringBuilder(256); GetWindowText(w, t, 256);
            System.Text.StringBuilder c = new System.Text.StringBuilder(256); GetClassName(w, c, 256);
            R r; GetWindowRect(w, out r);
            Console.WriteLine(pt.x + "," + pt.y + " -> hwnd=" + w + " pid=" + pid + " cls='" + c + "' title='" + t + "' rect(" + r.L + "," + r.T + ")-(" + r.Rt + "," + r.B + ")");
        }
    }
}
