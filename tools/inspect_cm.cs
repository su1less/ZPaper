using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class InspectCm
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static void Main()
    {
        uint self = (uint)Process.GetCurrentProcess().Id;
        RECT r = new RECT();
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint wp; GetWindowThreadProcessId(h, out wp);
            string exe = "";
            try { foreach (Process p in Process.GetProcessesByName("cloudmusic")) if (p.Id == wp) exe = "cloudmusic"; } catch {}
            if (exe == "") return true;
            StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256);
            StringBuilder c = new StringBuilder(256); GetClassName(h, c, 256);
            GetWindowRect(h, out r);
            Console.WriteLine(string.Format("hwnd={0} vis={1} iconic={2} rect=({3},{4})-({5},{6}) class='{7}' title='{8}'",
                h, IsWindowVisible(h), IsIconic(h), r.L, r.T, r.R, r.B, c, t));
            return true;
        }, IntPtr.Zero);
    }
}
