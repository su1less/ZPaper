using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
class SweepWW
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);

    static void Main(string[] a)
    {
        uint appPid = uint.Parse(a[0]);
        IntPtr wall = FindWall(appPid);
        if (wall == IntPtr.Zero) { Console.WriteLine("wall not found"); return; }
        // WorkerWs in z-order (top first)
        IntPtr[] wws = new IntPtr[32]; int n = 0;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            StringBuilder sb = new StringBuilder(64);
            GetClassName(h, sb, 64);
            if (sb.ToString() == "WorkerW" && IsWindowVisible(h) && n < 32) wws[n++] = h;
            return true;
        }, IntPtr.Zero);
        Console.WriteLine("candidates=" + n);
        for (int i = 0; i < n && i < 8; i++)
        {
            SetParent(wall, wws[i]);
            MoveWindow(wall, 0, 0, 1920, 1080, true);
            Thread.Sleep(1000);
            double d = SliverDiff();
            Console.WriteLine("host#" + i + " " + wws[i] + " -> sliverDiff=" + d.ToString("0.000"));
            if (d > 0.4 || BitmapPair.LastMax > 150) { Console.WriteLine("ANIMATING on " + wws[i] + " max=" + BitmapPair.LastMax); return; }
        }
        Console.WriteLine("none animated");
    }

    static IntPtr FindWall(uint pid)
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr h = IntPtr.Zero;
        while (true)
        {
            h = FindWindowEx(progman, h, null, null);
            if (h == IntPtr.Zero) break;
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid) return h;
        }
        // maybe already in a WorkerW from a previous sweep
        IntPtr w = IntPtr.Zero; IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h2, IntPtr l)
        {
            StringBuilder sb = new StringBuilder(64);
            GetClassName(h2, sb, 64);
            if (sb.ToString() != "WorkerW") return true;
            IntPtr c = IntPtr.Zero;
            while (true)
            {
                c = FindWindowEx(h2, c, null, null);
                if (c == IntPtr.Zero) break;
                uint p; GetWindowThreadProcessId(c, out p);
                if (p == pid) { found = c; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    static double SliverDiff()
    {
        // sample the visible wallpaper sliver left of the chat window
        BitmapPair bp = new BitmapPair();
        double d = bp.Capture();
        return d;
    }
}

// separate class holds GDI+ stuff (System.Drawing referenced at compile time)
class BitmapPair
{
    public double Capture()
    {
        using (System.Drawing.Bitmap b1 = new System.Drawing.Bitmap(1920, 1080))
        using (System.Drawing.Graphics g1 = System.Drawing.Graphics.FromImage(b1))
        {
            g1.CopyFromScreen(0, 0, 0, 0, b1.Size);
            System.Threading.Thread.Sleep(1800);
            using (System.Drawing.Bitmap b2 = new System.Drawing.Bitmap(1920, 1080))
            using (System.Drawing.Graphics g2 = System.Drawing.Graphics.FromImage(b2))
            {
                g2.CopyFromScreen(0, 0, 0, 0, b2.Size);
                double d = 0; int n = 0, mx = 0;
                for (int y = 120; y < 900; y += 10)
                    for (int x = 20; x < 380; x += 10)
                    {
                        System.Drawing.Color c1 = b1.GetPixel(x, y), c2 = b2.GetPixel(x, y);
                        int dd = Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
                        d += dd; n++;
                        if (dd > mx) mx = dd;
                    }
                LastMax = mx;
                return d / n;
            }
        }
    }
    public static int LastMax;
}
