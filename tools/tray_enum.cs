using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

class TrayEnum
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int SendMessage(IntPtr h, uint m, IntPtr w, StringBuilder l);
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, uint m, IntPtr w, ref TBBUTTON l);
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, uint m, IntPtr w, ref RECT l);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool b, uint p);
    [DllImport("kernel32.dll")] static extern bool QueryFullProcessImageName(IntPtr h, int f, StringBuilder s, ref int sz);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] struct TBBUTTON
    {
        public int iBitmap; public int idCommand; public byte fsState; public byte fsStyle;
        public byte bReserved0; public byte bReserved1; public IntPtr dwData; public IntPtr iString;
    }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    const uint TB_BUTTONCOUNT = 0x0418;
    const uint TB_GETBUTTON = 0x0417;
    const uint TB_GETBUTTONTEXT = 0x042D;
    const uint TB_GETITEMRECT = 0x041D;
    const uint TBIF_COMMAND = 0x20, TBIF_BYINDEX = 0x80000000;

    static string ProcOfHwnd(IntPtr h)
    {
        if (!IsWindow(h)) return "n/a";
        uint pid; GetWindowThreadProcessId(h, out pid);
        IntPtr hp = OpenProcess(0x1000, false, pid);
        if (hp == IntPtr.Zero) return "pid" + pid;
        StringBuilder sb = new StringBuilder(1024);
        int sz = 1024;
        if (QueryFullProcessImageName(hp, 0, sb, ref sz)) { CloseHandle(hp); return System.IO.Path.GetFileName(sb.ToString(0, sz)) + " (pid" + pid + ")"; }
        CloseHandle(hp);
        return "pid" + pid;
    }

    static void DumpToolbar(string name, IntPtr tb)
    {
        int count = SendMessage(tb, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
        Console.WriteLine(name + " buttons: " + count);
        for (int i = 0; i < count; i++)
        {
            TBBUTTON btn = new TBBUTTON();
            int r = SendMessage(tb, TB_GETBUTTON, (IntPtr)i, ref btn);
            if (r == 0) continue;
            StringBuilder txt = new StringBuilder(128);
            SendMessage(tb, TB_GETBUTTONTEXT, (IntPtr)btn.idCommand, txt);
            RECT rc = new RECT();
            SendMessage(tb, TB_GETITEMRECT, (IntPtr)i, ref rc);
            POINT pt = new POINT(); pt.X = rc.L; pt.Y = rc.T;
            ClientToScreen(tb, ref pt);
            Console.WriteLine(string.Format("  [{0}] id={1} dwData=0x{2:X} ({3}) text='{4}' rect=({5},{6}) size({7}x{8})",
                i, btn.idCommand, btn.dwData.ToInt64(), ProcOfHwnd(btn.dwData), txt, pt.X, pt.Y, rc.R - rc.L, rc.B - rc.T));
        }
    }

    static void Main()
    {
        IntPtr tray = FindWindow("Shell_TrayWnd", null);
        IntPtr notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        IntPtr pager = FindWindowEx(notify, IntPtr.Zero, "SysPager", null);
        IntPtr mainTb = pager != IntPtr.Zero ? FindWindowEx(pager, IntPtr.Zero, "ToolbarWindow32", null) : IntPtr.Zero;
        IntPtr overTb = FindWindowEx(notify, IntPtr.Zero, "ToolbarWindow32", null);
        Console.WriteLine("main toolbar: 0x" + mainTb.ToString("X") + "  overflow toolbar: 0x" + overTb.ToString("X"));
        if (mainTb != IntPtr.Zero) DumpToolbar("MAIN", mainTb);
        if (overTb != IntPtr.Zero) DumpToolbar("OVERFLOW", overTb);
    }
}
