using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class InspectQW
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern int GetDpiForWindow(IntPtr h);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static string Cls(IntPtr h) { StringBuilder c = new StringBuilder(256); GetClassName(h, c, 256); return c.ToString(); }
    static string Tit(IntPtr h) { StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256); return t.ToString(); }
    static void Main(string[] a)
    {
        string want = a[0]; // "QQ" or "Weixin"
        RECT r = new RECT();
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint wp; GetWindowThreadProcessId(h, out wp);
            bool hit = false;
            try { foreach (Process p in Process.GetProcessesByName(want)) if (p.Id == wp) hit = true; } catch {}
            if (!hit) return true;
            GetWindowRect(h, out r);
            int cloak = -1; try { DwmGetWindowAttribute(h, 14, out cloak, 4); } catch {}
            int dpi = -1; try { dpi = GetDpiForWindow(h); } catch {}
            IntPtr owner = GetWindow(h, 4); // GW_OWNER
            Console.WriteLine(string.Format("hwnd={0} vis={1} iconic={2} rect=({3},{4})-({5},{6}) cloak={7} dpi={8} owner={9} class='{10}' title='{11}'",
                h, IsWindowVisible(h), IsIconic(h), r.L, r.T, r.R, r.B, cloak, dpi, owner, Cls(h), Tit(h)));
            return true;
        }, IntPtr.Zero);
    }
}
