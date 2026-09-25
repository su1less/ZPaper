// wake2.exe <hwnd> <mode> <tag> - try one restore variant on a window, then report
// visibility, hit-test mapping (WindowFromPoint at several points of the shown rect)
// and save a screenshot of the rect for visual inspection.
// modes: A=SW_RESTORE only, B=A+size nudge, C=minimize->restore cycle, D=WM_SYSCOMMAND SC_RESTORE
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

class Wake2
{
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    static string Tit(IntPtr h) { StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256); return t.ToString(); }

    static void Hit(string name, IntPtr target, int x, int y)
    {
        POINT p = new POINT(); p.X = x; p.Y = y;
        IntPtr w = WindowFromPoint(p);
        uint wp = 0, tp = 0;
        if (w != IntPtr.Zero) GetWindowThreadProcessId(w, out wp);
        GetWindowThreadProcessId(target, out tp);
        Console.WriteLine(string.Format("  hit[{0} @({1},{2})] -> hwnd {3} pid {4} samePid={5} isTarget={6}",
            name, x, y, w, wp, wp == tp, w == target));
    }

    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        string mode = a[1].ToUpper();
        string tag = a[2];
        RECT r = new RECT();
        GetWindowRect(h, out r);
        Console.WriteLine(string.Format("== {0} mode {1} hwnd {2} '{3}' before: vis={4} iconic={5} rect=({6},{7})-({8},{9})",
            tag, mode, h, Tit(h), IsWindowVisible(h), IsIconic(h), r.L, r.T, r.R, r.B));

        if (mode == "H") { ShowWindow(h, 0); Thread.Sleep(400); return; }
        if (mode == "A" || mode == "B" || mode == "C")
        {
            if (!IsWindowVisible(h) || IsIconic(h)) { ShowWindow(h, 9); Thread.Sleep(200); }
        }
        if (mode == "B")
        {
            GetWindowRect(h, out r);
            int w = r.R - r.L, ht = r.B - r.T;
            SetWindowPos(h, IntPtr.Zero, r.L, r.T, w + 1, ht, 0x0002 | 0x0010 | 0x0004); // NOMOVE|NOACTIVATE|NOZORDER
            Thread.Sleep(120);
            SetWindowPos(h, IntPtr.Zero, r.L, r.T, w, ht, 0x0002 | 0x0010 | 0x0004);
            Thread.Sleep(300);
        }
        if (mode == "C")
        {
            ShowWindow(h, 6); Thread.Sleep(250);          // SW_MINIMIZE
            ShowWindow(h, 9); Thread.Sleep(450);          // SW_RESTORE
        }
        if (mode == "D")
        {
            if (!IsWindowVisible(h)) ShowWindow(h, 5);    // SW_SHOW
            Thread.Sleep(120);
            SendMessage(h, 0x0112, (IntPtr)0xF120, IntPtr.Zero);  // WM_SYSCOMMAND SC_RESTORE
            Thread.Sleep(450);
        }
        // foreground (same as dock)
        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        SetForegroundWindow(h);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        Thread.Sleep(900);

        GetWindowRect(h, out r);
        bool vis = IsWindowVisible(h);
        IntPtr fg = GetForegroundWindow();
        uint fgp = 0, tp2 = 0; GetWindowThreadProcessId(fg, out fgp); GetWindowThreadProcessId(h, out tp2);
        Console.WriteLine(string.Format("  after: vis={0} iconic={1} rect=({2},{3})-({4},{5}) fg='{6}' fgSamePid={7}",
            vis, IsIconic(h), r.L, r.T, r.R, r.B, Tit(fg), fgp == tp2));

        int w2 = r.R - r.L, h2 = r.B - r.T;
        if (vis && w2 > 100 && h2 > 100 && r.L > -10000)
        {
            Hit("center", h, r.L + w2 / 2, r.T + h2 / 2);
            Hit("tl", h, r.L + w2 / 4, r.T + h2 / 4);
            Hit("br", h, r.L + 3 * w2 / 4, r.T + 3 * h2 / 4);
            try
            {
                using (Bitmap b = new Bitmap(w2, h2))
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.CopyFromScreen(r.L, r.T, 0, 0, b.Size);
                    b.Save("out_" + tag + "_" + mode + ".png", ImageFormat.Png);
                }
                Console.WriteLine("  shot saved out_" + tag + "_" + mode + ".png");
            }
            catch (Exception ex) { Console.WriteLine("  shot FAILED " + ex.Message); }
        }
    }
}
