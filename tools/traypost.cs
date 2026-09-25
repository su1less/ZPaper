// traypost.exe <namePart> - like trayclick but PostMessage instead of real cursor
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
class TrayPost
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p, IntPtr a, UIntPtr size, uint type);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr b, byte[] buf, UIntPtr size, out IntPtr read);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct TBBUTTON
    {
        public int iBitmap; public int idCommand;
        public byte fsState, fsStyle;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] pad;
        public IntPtr dwData; public IntPtr iString;
    }
    const uint TB_BUTTONCOUNT = 0x400 + 24;
    const uint TB_GETBUTTON = 0x400 + 23;
    const uint TB_GETITEMRECT = 0x400 + 29;
    static void Main(string[] a)
    {
        string part = a[0];
        IntPtr tbwnd = FindWindow("Shell_TrayWnd", null);
        if (tbwnd != IntPtr.Zero) { ShowWindow(tbwnd, 5); Thread.Sleep(700); }
        IntPtr ovWnd = FindWindow("NotifyIconOverflowWindow", null);
        IntPtr ovTb = IntPtr.Zero;
        if (ovWnd != IntPtr.Zero) ovTb = FindWindowEx(ovWnd, IntPtr.Zero, "ToolbarWindow32", null);
        if (ovTb == IntPtr.Zero) { Console.WriteLine("no overflow toolbar"); ShowWindow(tbwnd, 0); return; }
        ShowWindow(ovWnd, 8); Thread.Sleep(400);
        if (ClickByOwner(ovTb, part, tbwnd, ovWnd)) Console.WriteLine("POSTED");
        else Console.WriteLine("not found");
    }
    static bool ClickByOwner(IntPtr tb, string part, IntPtr tbwnd, IntPtr ovWnd)
    {
        uint pid; GetWindowThreadProcessId(tb, out pid);
        IntPtr proc = OpenProcess(0x1F0FFF, false, pid);
        if (proc == IntPtr.Zero) return false;
        try
        {
            IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x1000, 0x40);
            if (remote == IntPtr.Zero) return false;
            try
            {
                int count = (int)SendMessage(tb, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
                byte[] buf = new byte[4096]; IntPtr dummy;
                for (int i = 0; i < count; i++)
                {
                    if (SendMessage(tb, TB_GETBUTTON, (IntPtr)i, remote) == IntPtr.Zero) continue;
                    if (!ReadProcessMemory(proc, remote, buf, (UIntPtr)Marshal.SizeOf(typeof(TBBUTTON)), out dummy)) continue;
                    TBBUTTON btn = new TBBUTTON();
                    btn.dwData = (IntPtr)BitConverter.ToInt64(buf, 16);
                    string owner = "";
                    if (btn.dwData != IntPtr.Zero)
                    {
                        byte[] dbuf = new byte[64]; IntPtr d3;
                        if (ReadProcessMemory(proc, btn.dwData, dbuf, (UIntPtr)64, out d3))
                            for (int off = 0; off <= 56; off += 4)
                            {
                                IntPtr mh = (IntPtr)BitConverter.ToInt64(dbuf, off);
                                if (mh == IntPtr.Zero) continue;
                                try { uint wp; GetWindowThreadProcessId(mh, out wp); if (wp == 0 || wp == pid) continue; owner = System.Diagnostics.Process.GetProcessById((int)wp).ProcessName; break; } catch { }
                            }
                    }
                    if (owner.IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (SendMessage(tb, TB_GETITEMRECT, (IntPtr)i, remote) == IntPtr.Zero) continue;
                    if (!ReadProcessMemory(proc, remote, buf, (UIntPtr)16, out dummy)) continue;
                    int rx = (BitConverter.ToInt32(buf, 0) + BitConverter.ToInt32(buf, 8)) / 2;
                    int ry = (BitConverter.ToInt32(buf, 4) + BitConverter.ToInt32(buf, 12)) / 2;
                    Console.WriteLine("owner " + owner + " btn#" + i + " client(" + rx + "," + ry + ")");
                    IntPtr lp = (IntPtr)((ry << 16) | (rx & 0xFFFF));
                    PostMessage(tb, 0x0201, (IntPtr)0x0001, lp);   // WM_LBUTTONDOWN
                    Thread.Sleep(80);
                    PostMessage(tb, 0x0202, IntPtr.Zero, lp);      // WM_LBUTTONUP
                    Thread.Sleep(1000);
                    ShowWindow(ovWnd, 0); ShowWindow(tbwnd, 0);
                    return true;
                }
            }
            finally { VirtualFreeEx(proc, remote, UIntPtr.Zero, 0x8000); }
        }
        finally { CloseHandle(proc); }
        ShowWindow(ovWnd, 0); ShowWindow(tbwnd, 0);
        return false;
    }
}
