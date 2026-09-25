using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
class WhoseChild
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    static void Main(string[] a)
    {
        foreach (string h in a)
        {
            IntPtr host = (IntPtr)int.Parse(h);
            bool found = false;
            EnumChildWindows(host, delegate(IntPtr c, IntPtr l)
            {
                uint cp; GetWindowThreadProcessId(c, out cp);
                if (cp != 13336) { Console.WriteLine("host " + host + " child " + c + " pid " + cp + (found ? "" : "")); found = true; }
                return true;
            }, IntPtr.Zero);
            if (!found) Console.WriteLine("host " + host + ": no foreign children");
        }
    }
}
