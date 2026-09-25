using System;
using System.Runtime.InteropServices;
using System.Text;
class TrayDump2
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr s, uint t, uint pr);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr b, byte[] buf, UIntPtr s, out IntPtr r);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool i, uint pid);
    static void Main()
    {
        IntPtr ov = FindWindow("NotifyIconOverflowWindow", null);
        IntPtr tb = FindWindowEx(ov, IntPtr.Zero, "ToolbarWindow32", null);
        uint pid; GetWindowThreadProcessId(tb, out pid);
        IntPtr proc = OpenProcess(0x1F0FFF, false, pid);
        IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x1000, 0x40);
        byte[] buf = new byte[64]; IntPtr dummy;
        int count = (int)SendMessage(tb, 0x400 + 24, IntPtr.Zero, IntPtr.Zero);
        for (int i = 0; i < Math.Min(4, count); i++)
        {
            if (SendMessage(tb, 0x400 + 23, (IntPtr)i, remote) == IntPtr.Zero) continue;
            ReadProcessMemory(proc, remote, buf, (UIntPtr)32, out dummy);
            IntPtr dwData = (IntPtr)BitConverter.ToInt64(buf, 16);
            byte[] dbuf = new byte[64];
            ReadProcessMemory(proc, dwData, dbuf, (UIntPtr)64, out dummy);
            IntPtr cb0 = (IntPtr)BitConverter.ToInt64(dbuf, 0);
            uint cpid = 0; GetWindowThreadProcessId(cb0, out cpid);
            string pn = "?"; try { pn = System.Diagnostics.Process.GetProcessById((int)cpid).ProcessName; } catch { }
            Console.WriteLine("btn#" + i + " dwData=" + dwData + " cb@0=" + cb0 + " -> pid " + cpid + " [" + pn + "]");
        }
    }
}
