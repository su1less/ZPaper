using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
class Snap
{
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        RECT r; GetWindowRect(h, out r);
        int w = r.R - r.L, hh = r.B - r.T;
        if (w > 0 && hh > 0)
        using (Bitmap b = new Bitmap(w, hh))
        {
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.L, r.T, 0, 0, b.Size);
            b.Save(a[1], ImageFormat.Png);
            Console.WriteLine("saved " + a[1] + " " + w + "x" + hh + " origin(" + r.L + "," + r.T + ")");
        }
    }
}
