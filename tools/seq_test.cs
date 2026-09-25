// Run a click sequence saving screenshots at each step for offline analysis
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class SeqTest
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);

    static void Save(string path)
    {
        using (var b = new Bitmap(1920, 1080))
        {
            using (var g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, b.Size);
            b.Save(path);
        }
    }

    static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(1000);
    }

    static void Dbl(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(80);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(1000);
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
        Save(dir + "seq0_initial.png");
        Click(395, 112);                                    // single click row area 1
        Save(dir + "seq1_after_single_r1.png");
        Click(395, 177);                                    // single click row area 2
        Save(dir + "seq2_after_single_r2.png");
        Dbl(395, 177);                                      // double click row area 2
        Save(dir + "seq3_after_dbl_r2.png");
        Console.WriteLine("done");
        SetCursorPos(960, 300);
        return 0;
    }
}
