// Test: does minimize+restore force WeChat's main window to fully repaint?
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class MinRestore
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);

    static void Save(string path)
    {
        using (var b = new Bitmap(1920, 1080))
        {
            using (var g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, b.Size);
            b.Save(path);
        }
    }

    static int Main()
    {
        IntPtr win = IntPtr.Zero;
        var pids = new System.Collections.Generic.List<uint>();
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Weixin")) pids.Add((uint)p.Id);
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (!pids.Contains(pid)) return true;
            StringBuilder sb = new StringBuilder(200); GetWindowText(h, sb, 200);
            if (sb.ToString() == "Jett Su") { win = h; return false; }
            return true;
        }, IntPtr.Zero);
        if (win == IntPtr.Zero) { Console.WriteLine("window not found"); return 1; }

        string dir = @"C:\Users\ASUS\ZCodeProject\desktop_beautify\";
        Save(dir + "mr0_before.png");
        ShowWindow(win, 6);   // SW_MINIMIZE
        Thread.Sleep(800);
        ShowWindow(win, 9);   // SW_RESTORE
        Thread.Sleep(1500);
        Save(dir + "mr1_after_min_restore.png");

        // also click once inside to give the app a real event
        SetCursorPos(940, 547);
        Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(1200);
        Save(dir + "mr2_after_click.png");
        Console.WriteLine("done, iconic=" + IsIconic(win));
        SetCursorPos(960, 300);
        return 0;
    }
}
