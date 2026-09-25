using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
class SpawnWW
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);


    static int wwCount;
    static void Main(string[] a)
    {
        IntPtr progman = FindWindow("Progman", null);
        for (int round = 0; round < 3; round++)
        {
            IntPtr res;
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 2, 1000, out res);
            Thread.Sleep(800);
            wwCount = 0;
            StringBuilder list = new StringBuilder();
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                StringBuilder sb = new StringBuilder(64);
                GetClassName(h, sb, 64);
                if (sb.ToString() == "WorkerW")
                {
                    wwCount++;
                    IntPtr def = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
                    list.Append(" WorkerW#" + h + (def != IntPtr.Zero ? "(hasDefView)" : ""));
                }
                return true;
            }, IntPtr.Zero);
            IntPtr defUnderProgman = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            Console.WriteLine("round " + round + ": WorkerW count=" + wwCount + list + " DefViewUnderProgman=" + (defUnderProgman != IntPtr.Zero));
            if (wwCount >= 2) { Console.WriteLine("classic structure restored"); break; }
        }
    }
}
