// traypost2.exe <namePart> <mode> - mode: post=mousemove+down+up, real=cursor click
using System;
using System.Runtime.InteropServices;
using System.Threading;
class TrayPost2
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr s, uint t, uint pr);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr b, byte[] buf, UIntPtr s, out IntPtr r);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool i, uint pid);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static void Main(string[] a)
    {
        string part = a[0]; string mode = a[1];
        IntPtr tbw = FindWindow("Shell_TrayWnd", null);
        if (tbw != IntPtr.Zero) { ShowWindow(tbw, 5); Thread.Sleep(700); }
        IntPtr ov = FindWindow("NotifyIconOverflowWindow", null);
        IntPtr tb = FindWindowEx(ov, IntPtr.Zero, "ToolbarWindow32", null);
        if (ov != IntPtr.Zero) { ShowWindow(ov, 8); Thread.Sleep(600); }
        uint pid; GetWindowThreadProcessId(tb, out pid);
        IntPtr proc = OpenProcess(0x1F0FFF, false, pid);
        IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x1000, 0x40);
        byte[] buf = new byte[64]; IntPtr dummy;
        int count = (int)SendMessage(tb, 0x400 + 24, IntPtr.Zero, IntPtr.Zero);
        for (int i = 0; i < count; i++)
        {
            if (SendMessage(tb, 0x400 + 23, (IntPtr)i, remote) == IntPtr.Zero) continue;
            ReadProcessMemory(proc, remote, buf, (UIntPtr)32, out dummy);
            IntPtr dwData = (IntPtr)BitConverter.ToInt64(buf, 16);
            byte[] dbuf = new byte[64];
            ReadProcessMemory(proc, dwData, dbuf, (UIntPtr)64, out dummy);
            IntPtr cb = (IntPtr)BitConverter.ToInt64(dbuf, 0);
            uint cp; GetWindowThreadProcessId(cb, out cp);
            string pn = "?"; try { pn = System.Diagnostics.Process.GetProcessById((int)cp).ProcessName; } catch { }
            if (!pn.Equals(part, StringComparison.OrdinalIgnoreCase)) continue;
            if (SendMessage(tb, 0x400 + 29, (IntPtr)i, remote) == IntPtr.Zero) continue;
            ReadProcessMemory(proc, remote, buf, (UIntPtr)16, out dummy);
            int rx = (BitConverter.ToInt32(buf, 0) + BitConverter.ToInt32(buf, 8)) / 2;
            int ry = (BitConverter.ToInt32(buf, 4) + BitConverter.ToInt32(buf, 12)) / 2;
            Console.WriteLine("found " + pn + " btn#" + i + " client(" + rx + "," + ry + ") mode=" + mode);
            if (mode == "post")
            {
                IntPtr lp = (IntPtr)((ry << 16) | (rx & 0xFFFF));
                PostMessage(tb, 0x0200, IntPtr.Zero, lp); Thread.Sleep(150);   // WM_MOUSEMOVE first
                PostMessage(tb, 0x0201, (IntPtr)0x0001, lp); Thread.Sleep(80);
                PostMessage(tb, 0x0202, IntPtr.Zero, lp);
            }
            else
            {
                POINT c = new POINT(); c.X = rx; c.Y = ry; ClientToScreen(tb, ref c);
                POINT o; GetCursorPos(out o);
                SetCursorPos(c.X, c.Y); Thread.Sleep(250);
                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60);
                mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(400);
                SetCursorPos(o.X, o.Y);
                Console.WriteLine("real clicked at (" + c.X + "," + c.Y + ")");
            }
            Thread.Sleep(1500);
            ShowWindow(ov, 0); ShowWindow(tbw, 0);
            return;
        }
        Console.WriteLine("not found");
        ShowWindow(ov, 0); ShowWindow(tbw, 0);
    }
}
