// wake3.exe <hwnd> <mode> - input-liveness tester.
// T  : real double-click on caption, report zoom/rect change (input works?), toggle back
// MT : PostMessage WM_MOUSEACTIVATE(HTCAPTION) first, then T
// C2T: minimize->restore cycle, then T
// RT : one real caption click, then T
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

class Wake3
{
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    static string Tit(IntPtr h) { StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256); return t.ToString(); }
    static string RectS(IntPtr h) { RECT r; GetWindowRect(h, out r); return string.Format("({0},{1})-({2},{3}) zoom={4}", r.L, r.T, r.R, r.B, IsZoomed(h)); }

    // real double-click at caption x=30%; returns true if window state visibly reacted (zoom toggled)
    static bool DblClickCaption(IntPtr h)
    {
        RECT r; if (!GetWindowRect(h, out r)) return false;
        POINT orig; if (!GetCursorPos(out orig)) return false;
        string before = RectS(h);
        int x = r.L + (r.R - r.L) * 3 / 10, y = r.T + 14;
        SetCursorPos(x, y); Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(80);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(500);
        string after = RectS(h);
        bool reacted = after != before;
        Console.WriteLine("    dblclick: before[" + before + "] after[" + after + "] -> INPUT " + (reacted ? "LIVE" : "DEAD"));
        if (reacted)
        {
            // toggle back
            SetCursorPos(x, y); Thread.Sleep(120);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(80);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(400);
            Console.WriteLine("    toggled back: " + RectS(h));
        }
        SetCursorPos(orig.X, orig.Y);
        return reacted;
    }

    static void Fg(IntPtr h)
    {
        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        SetForegroundWindow(h);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        Thread.Sleep(400);
    }

    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        string mode = a[1].ToUpper();
        Console.WriteLine("== wake3 " + mode + " hwnd " + h + " '" + Tit(h) + "': " + RectS(h));
        RECT r = new RECT();
        if (mode == "T") { Fg(h); DblClickCaption(h); return; }
        if (mode == "MT")
        {
            Fg(h);
            PostMessage(h, 0x0021, h, (IntPtr)((2) | (0x0201 << 16)));  // WM_MOUSEACTIVATE, HTCAPTION+WM_LBUTTONDOWN
            Thread.Sleep(400);
            DblClickCaption(h); return;
        }
        if (mode == "C2T")
        {
            ShowWindow(h, 6); Thread.Sleep(300);
            ShowWindow(h, 9); Thread.Sleep(600);
            Fg(h); DblClickCaption(h); return;
        }
        if (mode == "RT")
        {
            Fg(h);
            GetWindowRect(h, out r);
            POINT orig; GetCursorPos(out orig);
            SetCursorPos(r.L + (r.R - r.L) * 3 / 10, r.T + 14); Thread.Sleep(120);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(50); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(300);
            SetCursorPos(orig.X, orig.Y);
            DblClickCaption(h); return;
        }
        Console.WriteLine("unknown mode");
    }
}
