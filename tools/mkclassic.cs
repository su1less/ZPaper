using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
class MkClassic
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int ht, uint f);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);

    static void Main(string[] a)
    {
        string mode = a.Length > 0 ? a[0] : "build";
        IntPtr progman = FindWindow("Progman", null);
        IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (mode == "revert")
        {
            // move DefView back under Progman
            IntPtr w = IntPtr.Zero; IntPtr defHost = IntPtr.Zero; IntPtr dv = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                StringBuilder sb = new StringBuilder(64);
                GetClassName(h, sb, 64);
                if (sb.ToString() != "WorkerW") return true;
                IntPtr d = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (d != IntPtr.Zero) { defHost = h; dv = d; return false; }
                return true;
            }, IntPtr.Zero);
            if (dv != IntPtr.Zero)
            {
                SetParent(dv, progman);
                SetWindowPos(dv, (IntPtr)0, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                Console.WriteLine("reverted DefView " + dv + " -> Progman");
            }
            else Console.WriteLine("no DefView in WorkerW found");
            return;
        }
        if (defView == IntPtr.Zero)
        {
            Console.WriteLine("DefView not under Progman (already classic?)");
            return;
        }
        // find a visible fullscreen WorkerW to host the icon layer
        IntPtr target = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            StringBuilder sb = new StringBuilder(64);
            GetClassName(h, sb, 64);
            if (sb.ToString() != "WorkerW" || !IsWindowVisible(h)) return true;
            target = h;
            return true;   // keep last = bottom-most in enum order (enum is top-down)
        }, IntPtr.Zero);
        if (target == IntPtr.Zero) { Console.WriteLine("no WorkerW candidate"); return; }
        SetParent(defView, target);
        SetWindowPos(defView, (IntPtr)0, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);   // top within host
        MoveWindow(defView, 0, 0, 1920, 1080, true);
        Console.WriteLine("DefView " + defView + " -> WorkerW " + target);
    }
}
