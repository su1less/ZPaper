using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
class EdgeTest
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int ht, uint f);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out P p);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }
    [StructLayout(LayoutKind.Sequential)] struct P { public int X, Y; }
    static uint zpPid;

    static void Main()
    {
        // locate ZPapaer pid + dock window visibility checker
        foreach (Process p in Process.GetProcessesByName("ZPapaer")) zpPid = (uint)p.Id;
        if (zpPid == 0) { Console.WriteLine("ZPapaer not running"); return; }
        P orig; GetCursorPos(out orig);

        // 1. notepad fullscreen = fullscreen foreground
        Process np = Process.Start("notepad.exe");
        Thread.Sleep(1500);
        np.Refresh();
        IntPtr nh = np.MainWindowHandle;
        SetWindowPos(nh, (IntPtr)(-1), 0, 0, 1920, 1080, 0x0001 | 0x0002 | 0x0040);
        SetForegroundWindow(nh);
        Thread.Sleep(900);
        bool hiddenInFs = !DockVisible();
        Console.WriteLine("step1 fullscreen -> dock hidden: " + hiddenInFs);

        // 2. hug the bottom edge 1s -> dock summoned
        SetCursorPos(960, 1079);
        Thread.Sleep(1600);
        bool summoned = DockVisible();
        Console.WriteLine("step2 edge-hug 1s -> dock summoned: " + summoned);

        // 3. move away -> dock hides within 1s
        SetCursorPos(960, 400);
        Thread.Sleep(800);
        bool hiddenAgain = !DockVisible();
        Console.WriteLine("step3 mouse away -> dock hidden: " + hiddenAgain);

        // cleanup
        SetCursorPos(orig.X, orig.Y);
        PostMessage(nh, 0x0010, IntPtr.Zero, IntPtr.Zero);   // WM_CLOSE notepad
        Console.WriteLine("RESULT " + (hiddenInFs && summoned && hiddenAgain ? "PASS" : "FAIL"));
    }

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
}
