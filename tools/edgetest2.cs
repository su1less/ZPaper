using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
class FGHelper { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e); }
class EdgeTest2
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out P p);
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }
    [StructLayout(LayoutKind.Sequential)] struct P { public int X, Y; }
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

    static void Pump(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(40); }
    }

    static void Main()
    {
        foreach (Process p in Process.GetProcessesByName("ZPapaer")) zpPid = (uint)p.Id;
        if (zpPid == 0) { Console.WriteLine("ZPapaer not running"); return; }
        P orig; GetCursorPos(out orig);
        Form f = new Form();
        f.FormBorderStyle = FormBorderStyle.None;
        f.StartPosition = FormStartPosition.Manual;
        f.Bounds = Screen.PrimaryScreen.Bounds;
        f.TopMost = true;
        f.Show();
        FGHelper.keybd_event(0x12, 0, 0, UIntPtr.Zero);
        FGHelper.SetForegroundWindow(f.Handle);
        FGHelper.keybd_event(0x12, 0, 2, UIntPtr.Zero);
        f.Activate();
        Pump(1200);
        bool hiddenInFs = !DockVisible();
        Console.WriteLine("step1 fullscreen -> dock hidden: " + hiddenInFs);
        SetCursorPos(960, 1079);
        Pump(800);
        bool summoned = DockVisible();
        Console.WriteLine("step2 edge-hug 1s -> dock summoned: " + summoned);
        SetCursorPos(960, 400);
        Pump(900);
        bool hiddenAgain = !DockVisible();
        Console.WriteLine("step3 mouse away -> dock hidden: " + hiddenAgain);
        SetCursorPos(orig.X, orig.Y);
        f.Close();
        Console.WriteLine("RESULT " + (hiddenInFs && summoned && hiddenAgain ? "PASS" : "FAIL"));
    }
}
