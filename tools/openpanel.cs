using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
class OpenPanel
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }
    static uint wantPid;
    static IntPtr dock, panel; static int panelArea;

    static void Main(string[] a)
    {
        wantPid = uint.Parse(a[0]);
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != wantPid || !IsWindowVisible(h)) return true;
            R r; GetWindowRect(h, out r);
            int w = r.Rt - r.L, hh = r.B - r.T;
            if (w > 800 && w < 1200 && hh > 100 && hh < 200) dock = h;
            if (w > 1700 && hh > 900) { int area = w * hh; if (area > panelArea) { panelArea = area; panel = h; } }
            return true;
        }, IntPtr.Zero);
        Console.WriteLine("dock=" + dock + " existingPanel=" + panel);
        if (dock == IntPtr.Zero) return;
        IntPtr L = (IntPtr)((100 << 16) | 251);
        PostMessage(dock, 0x0201, (IntPtr)1, L);
        Thread.Sleep(80);
        PostMessage(dock, 0x0202, IntPtr.Zero, L);
        Thread.Sleep(450);
        // find the freshly opened panel and give it foreground so it stays open
        IntPtr p2 = IntPtr.Zero; int best = 0;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != wantPid || !IsWindowVisible(h)) return true;
            R r; GetWindowRect(h, out r);
            int w = r.Rt - r.L, hh = r.B - r.T;
            if (w > 1700 && hh > 900) { int area = w * hh; if (area > best) { best = area; p2 = h; } }
            return true;
        }, IntPtr.Zero);
        Console.WriteLine("panel=" + p2);
        if (p2 != IntPtr.Zero)
        {
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            SetForegroundWindow(p2);
            keybd_event(0x12, 0, 2, UIntPtr.Zero);
            Console.WriteLine("foregrounded");
        }
    }
}
