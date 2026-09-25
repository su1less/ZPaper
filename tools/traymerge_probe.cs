// traymerge_probe - read-only probe: enumerate notification-area toolbar
// buttons and print owner-pid/exe attribution from the dwData record (offset 0
// = callback HWND, per the dock's proven convention). Verifies tray-only apps
// (ToDesk/ZCode) are attributable BEFORE wiring them into the dock scan.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

static class Probe
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowW(string c, string t);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowExW(IntPtr p, IntPtr a, string c, string t);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool inh, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p, IntPtr addr, UIntPtr size, uint type);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr addr, byte[] buf, UIntPtr size, out IntPtr read);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(IntPtr h, uint f, StringBuilder s, ref int size);
    delegate bool EnumProc(IntPtr h, IntPtr l);

    static string ExeOfPid(uint pid)
    {
        IntPtr h = OpenProcess(0x1000, false, pid);   // QUERY_LIMITED_INFORMATION
        if (h == IntPtr.Zero) return null;
        try
        {
            StringBuilder sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (QueryFullProcessImageNameW(h, 0, sb, ref size)) return sb.ToString(0, size);
            return null;
        }
        finally { CloseHandle(h); }
    }

    static List<IntPtr> Toolbars()
    {
        List<IntPtr> tbs = new List<IntPtr>();
        IntPtr ov = FindWindowW("NotifyIconOverflowWindow", null);
        if (ov != IntPtr.Zero)
        {
            IntPtr t = FindWindowExW(ov, IntPtr.Zero, "ToolbarWindow32", null);
            if (t == IntPtr.Zero)
            {
                IntPtr pg = FindWindowExW(ov, IntPtr.Zero, "SysPager", null);
                if (pg != IntPtr.Zero) t = FindWindowExW(pg, IntPtr.Zero, "ToolbarWindow32", null);
            }
            if (t != IntPtr.Zero) tbs.Add(t);
        }
        IntPtr tbw = FindWindowW("Shell_TrayWnd", null);
        if (tbw != IntPtr.Zero)
        {
            IntPtr tn = FindWindowExW(tbw, IntPtr.Zero, "TrayNotifyWnd", null);
            if (tn != IntPtr.Zero)
            {
                IntPtr pg = FindWindowExW(tn, IntPtr.Zero, "SysPager", null);
                IntPtr t = pg != IntPtr.Zero ? FindWindowExW(pg, IntPtr.Zero, "ToolbarWindow32", null) : IntPtr.Zero;
                if (t == IntPtr.Zero) t = FindWindowExW(tn, IntPtr.Zero, "ToolbarWindow32", null);
                if (t != IntPtr.Zero) tbs.Add(t);
            }
        }
        return tbs;
    }

    [STAThread]
    static int Main()
    {
        List<IntPtr> tbs = Toolbars();
        Console.WriteLine("toolbars=" + tbs.Count);
        foreach (IntPtr tb in tbs)
        {
            uint expPid;
            GetWindowThreadProcessId(tb, out expPid);
            Console.WriteLine("-- toolbar 0x{0:X} (explorer pid {1}) class '{2}'", tb.ToInt64(), expPid, ClassOf(tb));
            IntPtr proc = OpenProcess(0x1F0FFF, false, expPid);
            if (proc == IntPtr.Zero) { Console.WriteLine("   open explorer FAILED"); continue; }
            try
            {
                IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x1000, 0x40);
                if (remote == IntPtr.Zero) { Console.WriteLine("   alloc FAILED"); continue; }
                try
                {
                    int count = (int)SendMessageW(tb, 0x400 + 24, IntPtr.Zero, IntPtr.Zero);   // TB_BUTTONCOUNT
                    Console.WriteLine("   buttons=" + count);
                    byte[] buf = new byte[64]; IntPtr dummy;
                    for (int i = 0; i < count; i++)
                    {
                        if (SendMessageW(tb, 0x400 + 23, (IntPtr)i, remote) == IntPtr.Zero) continue;   // TB_GETBUTTON
                        if (!ReadProcessMemory(proc, remote, buf, (UIntPtr)32, out dummy)) continue;
                        IntPtr dwData = (IntPtr)BitConverter.ToInt64(buf, 16);
                        if (dwData == IntPtr.Zero) { Console.WriteLine("   #{0} dwData=0", i); continue; }
                        byte[] dbuf = new byte[64];
                        if (!ReadProcessMemory(proc, dwData, dbuf, (UIntPtr)64, out dummy)) { Console.WriteLine("   #{0} record read FAILED", i); continue; }
                        IntPtr cbw = (IntPtr)BitConverter.ToInt64(dbuf, 0);   // offset 0 = callback HWND
                        uint wp = 0;
                        if (cbw != IntPtr.Zero) GetWindowThreadProcessId(cbw, out wp);
                        string exe = wp != 0 ? ExeOfPid(wp) : null;
                        string name = exe == null ? "(denied)" : System.IO.Path.GetFileNameWithoutExtension(exe);
                        Console.WriteLine("   #{0} hwnd=0x{1:X} pid={2} exe='{3}' vis={4} title='{5}'",
                            i, cbw.ToInt64(), wp, name, cbw != IntPtr.Zero && IsWindowVisible(cbw), TitleOf(cbw));
                    }
                }
                finally { VirtualFreeEx(proc, remote, UIntPtr.Zero, 0x8000); }
            }
            finally { CloseHandle(proc); }
        }
        return 0;
    }

    static string ClassOf(IntPtr h) { StringBuilder s = new StringBuilder(96); GetClassNameW(h, s, 96); return s.ToString(); }
    static string TitleOf(IntPtr h) { StringBuilder s = new StringBuilder(128); GetWindowTextW(h, s, 128); return s.ToString(); }
}
