// dockshot - DPI-aware capture of the bottom band of the primary screen
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

static class Shot
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    struct POINT { public int X, Y; }

    static int Main(string[] args)
    {
        SetProcessDPIAware();
        Rectangle b = System.Windows.Forms.SystemInformation.VirtualScreen;
        int h = args.Length > 0 ? int.Parse(args[0]) : 220;
        using (Bitmap bmp = new Bitmap(b.Width, h))
        {
            using (Graphics g = Graphics.FromImage(bmp))
                g.CopyFromScreen(b.Left, b.Bottom - h, 0, 0, bmp.Size);
            bmp.Save("dockshot.png", ImageFormat.Png);
        }
        POINT c;
        GetCursorPos(out c);
        Console.WriteLine("saved dockshot.png " + b.Width + "x" + h + " cursor=" + c.X + "," + c.Y);
        return 0;
    }
}
