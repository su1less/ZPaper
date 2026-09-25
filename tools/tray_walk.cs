using System;
using System.Runtime.InteropServices;
using System.Text;

class TrayWalk
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int SendMessage(IntPtr h, uint m, IntPtr w, StringBuilder l);

    static void Dump(IntPtr h, string indent, int depth)
    {
        if (h == IntPtr.Zero || depth > 5) return;
        StringBuilder c = new StringBuilder(128);
        GetClassName(h, c, 128);
        Console.WriteLine(indent + "0x" + h.ToString("X") + " [" + c + "]");
        IntPtr child = FindWindowEx(h, IntPtr.Zero, null, null);
        while (child != IntPtr.Zero)
        {
            Dump(child, indent + "  ", depth + 1);
            child = FindWindowEx(h, child, null, null);
        }
    }

    static void Main()
    {
        IntPtr tray = FindWindow("Shell_TrayWnd", null);
        Console.WriteLine("Shell_TrayWnd: 0x" + tray.ToString("X"));
        Dump(tray, "", 0);
    }
}
