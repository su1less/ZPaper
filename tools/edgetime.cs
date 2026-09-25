using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
class EdgeTime
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }
    static uint zpPid;
    static bool DockVisible()
    {
        bool vis = false;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != zpPid || !IsWindowVisible(h)) return true;
            R r; GetWindowRect(h, out r);
            int w = r.Rt - r.L, hh = r.B - r.T;
            if (w > 800 && w < 1200 && hh > 100 && hh < 200) { vis = true; return false; }
            return true;
        }, IntPtr.Zero);
        return vis;
    }
    static void Main()
    {
        foreach (Process p in Process.GetProcessesByName("ZPapaer")) zpPid = (uint)p.Id;
        Form f = new Form();
        f.FormBorderStyle = FormBorderStyle.None;
        f.StartPosition = FormStartPosition.Manual;
        f.Bounds = Screen.PrimaryScreen.Bounds;
        f.TopMost = true;
        f.Show();
        FGHelper.keybd_event(0x12, 0, 0, UIntPtr.Zero);
        FGHelper.SetForegroundWindow(f.Handle);
        FGHelper.keybd_event(0x12, 0, 2, UIntPtr.Zero);
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 1200) { Application.DoEvents(); Thread.Sleep(30); }
        // wait until dock hides (it may already be hidden)
        while (DockVisible() && sw.ElapsedMilliseconds < 4000) { Application.DoEvents(); Thread.Sleep(50); }
        // hug the edge and time first visibility
        SetCursorPos(960, 1079);
        var t0 = Stopwatch.StartNew();
        double first = -1;
        while (t0.ElapsedMilliseconds < 3000)
        {
            Application.DoEvents(); Thread.Sleep(30);
            if (first < 0 && DockVisible()) { first = t0.ElapsedMilliseconds; break; }
        }
        Console.WriteLine("first visible at " + first + " ms after cursor arrival");
        Thread.Sleep(400);
        SetCursorPos(960, 400);
        Thread.Sleep(900);
        Console.WriteLine("hidden after move-away: " + !DockVisible());
        f.Close();
    }
}
class FGHelper { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e); }
