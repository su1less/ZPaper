// send keys to the foreground popup menu (#32768): DOWN xN then ENTER
using System;
using System.Runtime.InteropServices;
using System.Threading;
class MenuKey
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowW(string c, string t);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    static void Main(string[] a)
    {
        IntPtr h = FindWindowW("#32768", null);
        if (h == IntPtr.Zero) { Console.WriteLine("no menu window"); return; }
        int downs = a.Length > 0 ? int.Parse(a[0]) : 1;
        bool enter = a.Length < 2 || a[1] != "noenter";
        for (int i = 0; i < downs; i++)
        {
            PostMessage(h, 0x0100, (IntPtr)0x28, IntPtr.Zero);  // DOWN
            PostMessage(h, 0x0101, (IntPtr)0x28, IntPtr.Zero);
            Thread.Sleep(60);
        }
        Console.WriteLine("sent " + downs + " DOWN to " + h);
        if (enter)
        {
            Thread.Sleep(80);
            PostMessage(h, 0x0100, (IntPtr)0x0D, IntPtr.Zero);  // RETURN
            PostMessage(h, 0x0101, (IntPtr)0x0D, IntPtr.Zero);
            Console.WriteLine("sent ENTER");
        }
    }
}
