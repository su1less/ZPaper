using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class ActivateTabTest
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
    class TaskbarList { }
    [ComImport, Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList3
    {
        void HrInit(); void AddTab(IntPtr h); void DeleteTab(IntPtr h); void ActivateTab(IntPtr h);
        void SetActiveAlt(IntPtr h);
    }

    static void Main(string[] args)
    {
        string procName = args.Length > 0 ? args[0] : "QQ";
        // find the biggest titled window of that process
        IntPtr best = IntPtr.Zero; long bestArea = 0;
        var pids = new System.Collections.Generic.List<uint>();
        foreach (var p in Process.GetProcessesByName(procName)) pids.Add((uint)p.Id);
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (!pids.Contains(pid)) return true;
            StringBuilder sb = new StringBuilder(200); GetWindowText(h, sb, 200);
            if (sb.Length == 0) return true;
            RECT r; GetWindowRect(h, out r);
            long a = (long)(r.R - r.L) * (r.B - r.T);
            if (a > bestArea) { bestArea = a; best = h; }
            return true;
        }, IntPtr.Zero);
        if (best == IntPtr.Zero) { Console.WriteLine("no window found for " + procName); return; }
        Console.WriteLine("activating 0x" + best.ToString("X") + " via ITaskbarList.ActivateTab");
        var tlb = (ITaskbarList3)new TaskbarList();
        tlb.HrInit();
        tlb.ActivateTab(best);
        Thread.Sleep(1200);
        IntPtr fg = GetForegroundWindow();
        uint fpid; GetWindowThreadProcessId(fg, out fpid);
        Console.WriteLine("foreground now: " + Process.GetProcessById((int)fpid).ProcessName + " (want " + procName + ")");
    }
}
