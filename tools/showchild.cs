using System;
using System.Runtime.InteropServices;
using System.Text;
class ShowChild
{
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    static void Main(string[] a)
    {
        IntPtr p = (IntPtr)int.Parse(a[0]);
        EnumChildWindows(p, delegate(IntPtr h, IntPtr l)
        {
            StringBuilder c = new StringBuilder(128); GetClassName(h, c, 128);
            string cls = c.ToString();
            if (cls == "Chrome_RenderWidgetHostHWND" && !IsWindowVisible(h))
            {
                bool ok = ShowWindow(h, 5); // SW_SHOW
                Console.WriteLine("showed " + cls + " " + h + " -> " + ok + " nowVis=" + IsWindowVisible(h));
            }
            return true;
        }, IntPtr.Zero);
    }
}
