// User-simulation E2E test: clicks the dock icon like a real user, then clicks
// INSIDE the app window and screenshot-diffs to prove the window accepts input.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class E2ETest
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    static void Log(string s) { Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); }

    static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    static Bitmap Shot()
    {
        var b = new Bitmap(1920, 1080);
        using (var g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, b.Size);
        return b;
    }

    static double DiffRegion(Bitmap a, Bitmap b, int x0, int y0, int w, int h)
    {
        long sum = 0; int n = 0;
        for (int y = y0; y < y0 + h; y += 6)
            for (int x = x0; x < x0 + w; x += 6)
            {
                Color ca = a.GetPixel(x, y), cb = b.GetPixel(x, y);
                sum += Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B);
                n++;
            }
        return n == 0 ? 0 : sum / (double)(n * 3);
    }

    // biggest titled window of a process
    static IntPtr BiggestWindow(string[] names)
    {
        IntPtr best = IntPtr.Zero; long bestA = 0;
        var pids = new System.Collections.Generic.List<uint>();
        foreach (var n in names)
            try { foreach (var p in Process.GetProcessesByName(n)) pids.Add((uint)p.Id); } catch { }
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (!pids.Contains(pid)) return true;
            StringBuilder sb = new StringBuilder(200); GetWindowText(h, sb, 200);
            if (sb.Length == 0) return true;
            RECT r; GetWindowRect(h, out r);
            long a = (long)(r.R - r.L) * (r.B - r.T);
            if (a > bestA) { bestA = a; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    static bool AnyVisible(string[] names)
    {
        var pids = new System.Collections.Generic.List<uint>();
        foreach (var n in names)
            try { foreach (var p in Process.GetProcessesByName(n)) pids.Add((uint)p.Id); } catch { }
        bool found = false;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pids.Contains(pid) && IsWindowVisible(h)) { found = true; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    static string ForegroundProc()
    {
        uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        try { return Process.GetProcessById((int)pid).ProcessName; } catch { return "?"; }
    }

    static int FindGreenIcon()
    {
        using (var bmp = Shot())
        {
            // require a CONTIGUOUS green run (icon) rather than scattered wallpaper green
            var counts = new System.Collections.Generic.Dictionary<int, int>();
            for (int y = 1015; y < 1060; y += 2)
                for (int x = 500; x < 1450; x += 2)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.G > 130 && c.G > c.R + 40 && c.G > c.B + 30)
                    {
                        if (!counts.ContainsKey(x)) counts[x] = 0;
                        counts[x]++;
                    }
                }
            int bestX = -1, bestN = 0;
            foreach (var kv in counts)
                if (kv.Value > bestN) { bestN = kv.Value; bestX = kv.Key; }
            return bestX;
        }
    }

    static void MinimizeAllWindows()
    {
        // via shell COM
        var t = Type.GetTypeFromProgID("Shell.Application");
        var sh = Activator.CreateInstance(t);
        t.InvokeMember("MinimizeAll", System.Reflection.BindingFlags.InvokeMethod, null, sh, null);
        Thread.Sleep(1800);
    }

    static int Main(string[] args)
    {
        // args: wechat|qq
        string app = args.Length > 0 ? args[0] : "wechat";
        string[] names = app == "wechat" ? new[] { "Weixin", "WeChatAppEx" } : new[] { "QQ" };
        string wantFg = app == "wechat" ? "Weixin" : "QQ";

        Log("=== E2E test: " + app + " ===");

        // 1. hide the app's window (simulate tray-minimized state)
        IntPtr win = BiggestWindow(names);
        if (win == IntPtr.Zero) { Log("FAIL: no titled window found for " + app); return 1; }
        Log("hiding window 0x" + win.ToString("X"));
        ShowWindow(win, 0);
        Thread.Sleep(800);

        // 2. reveal desktop so the dock is on top
        MinimizeAllWindows();

        // 3. find the dock icon and click it (wechat = green detect; qq = wechat pos - 4 slots)
        int x = -1;
        if (app == "wechat")
        {
            x = FindGreenIcon();
            if (x < 0) { Log("FAIL: green WeChat icon not found"); return 1; }
        }
        else
        {
            int wx = FindGreenIcon();
            if (wx < 0) { Log("FAIL: green WeChat icon not found (needed as anchor)"); return 1; }
            x = wx - 4 * 51;   // QQ sits 4 slots left of WeChat
            Log("WeChat icon at " + wx + ", estimating QQ at " + x);
        }
        // click IMMEDIATELY at the rest position - hovering would magnify the
        // layout and shift the icons before the click lands
        Log("clicking dock icon at (" + x + ", 1032)");
        Click(x, 1032);

        // 4. wait for our app's activation chain (restore + caption click + client click)
        Thread.Sleep(3500);
        bool vis = AnyVisible(names);
        string fg = ForegroundProc();
        Log("window visible: " + vis + ", foreground: " + fg + " (want " + wantFg + ")");
        if (!vis) { Log("FAIL: window did not become visible"); return 1; }

        // 5. USER-SIM: click a conversation item inside the window, diff the chat pane
        IntPtr w2 = BiggestWindow(names);
        RECT r; GetWindowRect(w2, out r);
        int w = r.R - r.L, h = r.B - r.T;
        Log("window rect (" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ")");
        // settle then take pre-click screenshot
        Thread.Sleep(800);
        Bitmap before = Shot();
        // click the FIRST conversation row - row center (judge-measured): list
        // spans x 275-514, row1 center (395,112); x=250px into the window is too
        // close to the list edge (dead zone)
        int cx = r.L + 188, cy = r.T + 112;
        Log("clicking conversation row at (" + cx + "," + cy + ")");
        Click(cx, cy);
        Thread.Sleep(1300);
        Bitmap after = Shot();
        // diff the right chat pane
        int px0 = r.L + (int)(w * 0.62), py0 = r.T + 80, pw = r.R - px0 - 40, ph = r.B - py0 - 60;
        if (px0 + pw > 1919) pw = 1919 - px0;
        double d = DiffRegion(before, after, px0, py0, pw, ph);
        Log("chat pane diff = " + d.ToString("F1") + " (threshold 6)");
        bool pass = d > 6;
        Log(pass ? "PASS: window accepts input and reacts" : "FAIL: window does not react to clicks");
        // move cursor away
        SetCursorPos(960, 300);
        return pass ? 0 : 1;
    }
}
