using System;
using System.Runtime.InteropServices;
class SetWall
{
    [DllImport("user32.dll", SetLastError=true)] static extern bool SystemParametersInfo(int a, int b, string c, int d);
    static void Main(string[] a)
    {
        bool ok = SystemParametersInfo(20, 0, a[0], 1 | 2);
        Console.WriteLine("set wallpaper to [" + a[0] + "] -> " + ok);
    }
}
