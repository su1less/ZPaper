using System;
using System.Runtime.InteropServices;
using System.Text;
class ListPid
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [StructLayout(LayoutKind.Sequential)] struct R { public int L, T, Rt, B; }
    static uint want;

    static void Main(string[] a)
    {
        want = uint.Parse(a[0]);
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid != want) return true;
            StringBuilder sb = new StringBuilder(128);
            GetClassName(h, sb, 128);
            R r; GetWindowRect(h, out r);
            IntPtr par = GetParent(h);
            Console.WriteLine("hwnd=" + h + " cls=" + sb + " parent=" + par + " vis=" + IsWindowVisible(h)
                + " rect(" + r.L + "," + r.T + ")-(" + r.Rt + "," + r.B + ")");
            return true;
        }, IntPtr.Zero);
    }
}
