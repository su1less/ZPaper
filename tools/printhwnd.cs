using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
class PrintHwnd
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static void Main(string[] a)
    {
        IntPtr h = (IntPtr)int.Parse(a[0]);
        RECT r; GetWindowRect(h, out r);
        using (Bitmap b = new Bitmap(r.R - r.L, r.B - r.T))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                IntPtr hdc = g.GetHdc();
                PrintWindow(h, hdc, 2);   // PW_RENDERFULLCONTENT
                g.ReleaseHdc(hdc);
            }
            b.Save(a[1], ImageFormat.Png);
            Console.WriteLine("saved " + a[1]);
        }
    }
}
