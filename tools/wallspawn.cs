using System;
using System.Runtime.InteropServices;
class WallSpawn
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
    static void Try(string label, IntPtr w, IntPtr l)
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr r; SendMessageTimeout(progman, 0x052C, w, l, 2, 1000, out r);
        System.Threading.Thread.Sleep(600);
        IntPtr ww1 = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WorkerW", null);
        IntPtr ww2 = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        Console.WriteLine(label + ": WorkerW top=" + ww1 + " underProgman=" + ww2);
    }
    static void Main()
    {
        Try("0x052C (0,0)", (IntPtr)0, (IntPtr)0);
        Try("0x052C (0,1)", (IntPtr)0, (IntPtr)1);
        Try("0x052C (1,0)", (IntPtr)1, (IntPtr)0);
        Try("0x052C (0,0) again", (IntPtr)0, (IntPtr)0);
    }
}
