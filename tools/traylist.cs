// trayclick.exe <namePart> - classic-taskbar tray icon clicker.
// Enumerates notify icons (visible tray + overflow) via TB_* messages, finds the
// one whose tooltip contains namePart, real-clicks its center.
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class TrayClick
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p, IntPtr a, UIntPtr size, uint type);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr base_, byte[] buf, UIntPtr size, out IntPtr read);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

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
    const uint TB_GETBUTTONTEXT = 0x400 + 75;   // TB_GETBUTTONTEXTW

    static void Main(string[] a)
    {
        string part = a[0];
        // 1. show the taskbar itself
        IntPtr tbwnd = FindWindow("Shell_TrayWnd", null);
        if (tbwnd != IntPtr.Zero) { ShowWindow(tbwnd, 5); Thread.Sleep(700); }

        // candidate toolbars: visible tray + overflow window
        IntPtr trayNotify = FindWindowEx(tbwnd, IntPtr.Zero, "TrayNotifyWnd", null);
        IntPtr visTb = IntPtr.Zero;
        if (trayNotify != IntPtr.Zero)
        {
            IntPtr pager = FindWindowEx(trayNotify, IntPtr.Zero, "SysPager", null);
            if (pager != IntPtr.Zero) visTb = FindWindowEx(pager, IntPtr.Zero, "ToolbarWindow32", null);
        }
        IntPtr ovWnd = FindWindow("NotifyIconOverflowWindow", null);
        IntPtr ovTb = IntPtr.Zero;
        if (ovWnd != IntPtr.Zero)
        {
            // EP-classic overflow: ToolbarWindow32 sits directly under the window
            ovTb = FindWindowEx(ovWnd, IntPtr.Zero, "ToolbarWindow32", null);
            if (ovTb == IntPtr.Zero)
            {
                IntPtr pager2 = FindWindowEx(ovWnd, IntPtr.Zero, "SysPager", null);
                if (pager2 != IntPtr.Zero) ovTb = FindWindowEx(pager2, IntPtr.Zero, "ToolbarWindow32", null);
            }
        }
        Console.WriteLine("visTb=" + visTb + " ovWnd=" + ovWnd + " ovTb=" + ovTb);

        // try overflow first (QQ usually there), then visible tray
        IntPtr targetTb = IntPtr.Zero; RECT targetRect = new RECT(); IntPtr targetOwner = IntPtr.Zero;
        bool found = false;
        IntPtr[] tbs = { ovTb, visTb };
        IntPtr[] owners = { ovWnd, tbwnd };
        bool[] needShow = { true, false };
        for (int k = 0; k < 2 && !found; k++)
        {
            if (tbs[k] == IntPtr.Zero) continue;
            if (needShow[k] && owners[k] != IntPtr.Zero) { ShowWindow(owners[k], 8); Thread.Sleep(500); }  // SW_SHOWNOACTIVATE
            if (FindIcon(tbs[k], part, out targetRect)) { targetTb = tbs[k]; targetOwner = owners[k]; found = true; }
        }
        if (!found) { Console.WriteLine("icon not found: " + part); Cleanup(tbwnd, ovWnd); return; }

        POINT c = new POINT(); c.X = (targetRect.L + targetRect.R) / 2; c.Y = (targetRect.T + targetRect.B) / 2;
        ClientToScreen(targetTb, ref c);
        Console.WriteLine("icon at screen (" + c.X + "," + c.Y + ") - clicking");
        POINT o; GetCursorPos(out o);
        SetCursorPos(c.X, c.Y); Thread.Sleep(250);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(1200);
        SetCursorPos(o.X, o.Y);
        Console.WriteLine("clicked");
        Cleanup(tbwnd, ovWnd);
    }

    static void Cleanup(IntPtr tbwnd, IntPtr ovWnd)
    {
        if (ovWnd != IntPtr.Zero) ShowWindow(ovWnd, 0);
        if (tbwnd != IntPtr.Zero) ShowWindow(tbwnd, 0);
    }

    static bool FindIcon(IntPtr tb, string part, out RECT rectOut)
    {
        rectOut = new RECT();
        uint pid; GetWindowThreadProcessId(tb, out pid);
        IntPtr proc = OpenProcess(0x1F0FFF, false, pid);   // PROCESS_ALL_ACCESS
        if (proc == IntPtr.Zero) { Console.WriteLine("OpenProcess failed"); return false; }
        try
        {
            IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x1000, 0x40);
            if (remote == IntPtr.Zero) return false;
            try
            {
                int count = (int)SendMessage(tb, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
                Console.WriteLine("toolbar " + tb + " buttons=" + count);
                byte[] buf = new byte[4096]; IntPtr dummy;
                for (int i = 0; i < count; i++)
                {
                    IntPtr r1 = SendMessage(tb, TB_GETBUTTON, (IntPtr)i, remote);
                    if (r1 == IntPtr.Zero) continue;
                    if (!ReadProcessMemory(proc, remote, buf, (UIntPtr)Marshal.SizeOf(typeof(TBBUTTON)), out dummy)) continue;
                    TBBUTTON btn = BytesToButton(buf);
                    // tooltip via TB_GETBUTTONTEXT into the remote buffer (reliable)
                    string tip = "";
                    IntPtr lenP = SendMessage(tb, TB_GETBUTTONTEXT, (IntPtr)i, remote);
                    if ((int)lenP > 0)
                    {
                        byte[] tbuf = new byte[(int)lenP * 2 + 4]; IntPtr dummy2;
                        if (ReadProcessMemory(proc, remote, tbuf, (UIntPtr)tbuf.Length, out dummy2))
                        {
                            tip = Encoding.Unicode.GetString(tbuf, 0, (int)lenP * 2);
                            int z = tip.IndexOf('\0'); if (z >= 0) tip = tip.Substring(0, z);
                        }
                    }
                    // rect
                    if (SendMessage(tb, TB_GETITEMRECT, (IntPtr)i, remote) == IntPtr.Zero) continue;
                    if (!ReadProcessMemory(proc, remote, buf, (UIntPtr)16, out dummy)) continue;
                    RECT r; r.L = BitConverter.ToInt32(buf, 0); r.T = BitConverter.ToInt32(buf, 4);
                    r.R = BitConverter.ToInt32(buf, 8); r.B = BitConverter.ToInt32(buf, 12);
                    // owner process via dwData block scan (callback HWND lives in there)
                    string owner = "";
                    if (btn.dwData != IntPtr.Zero)
                    {
                        byte[] dbuf = new byte[64]; IntPtr d3;
                        if (ReadProcessMemory(proc, btn.dwData, dbuf, (UIntPtr)64, out d3))
                        {
                            for (int off = 0; off <= 0; off += 4)
                            {
                                IntPtr maybeHwnd = (IntPtr)BitConverter.ToInt64(dbuf, off);
                                if (maybeHwnd == IntPtr.Zero) continue;
                                try
                                {
                                    uint wp; GetWindowThreadProcessId(maybeHwnd, out wp);
                                    if (wp == 0 || wp == pid) continue;  // no window / explorer itself
                                    owner = System.Diagnostics.Process.GetProcessById((int)wp).ProcessName;
                                    break;
                                }
                                catch { }
                            }
                        }
                    }
                    Console.WriteLine("  btn#" + i + " tip=[" + tip.Replace('\n', ' ') + "] owner=[" + owner + "] rect=" + r.L + "," + r.T + "," + r.R + "," + r.B);
                    bool tipHit = tip.Length > 0 && tip.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool ownHit = owner.Length > 0 && owner.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
                    if ((tipHit || ownHit) && r.R > r.L)
                    { rectOut = r; return true; }
                }
            }
            finally { VirtualFreeEx(proc, remote, UIntPtr.Zero, 0x8000); }
        }
        finally { CloseHandle(proc); }
        return false;
    }

    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    static TBBUTTON BytesToButton(byte[] b)
    {
        TBBUTTON t = new TBBUTTON();
        t.iBitmap = BitConverter.ToInt32(b, 0);
        t.idCommand = BitConverter.ToInt32(b, 4);
        t.fsState = b[8]; t.fsStyle = b[9];
        t.dwData = (IntPtr)BitConverter.ToInt64(b, 16);
        t.iString = (IntPtr)BitConverter.ToInt64(b, 24);
        return t;
    }
}
