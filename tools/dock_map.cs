using System;
using System.Drawing;
using System.Runtime.InteropServices;

class DockMap
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    static void Main()
    {
        SetCursorPos(960, 200);   // keep cursor off the dock so it rests
        System.Threading.Thread.Sleep(800);
        using (var bmp = new Bitmap(1920, 1080))
        {
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
            // scan icon rows y=1020..1048, print color runs along x
            Console.WriteLine("color runs along dock (y=1032):");
            for (int x = 500; x <= 1450; x++)
            {
                Color c = bmp.GetPixel(x, 1032);
                int r = c.R, gg = c.G, b = c.B;
                string tag = "";
                if (gg > 130 && gg > r + 40 && gg > b + 30) tag = " GREEN";
                if (r > 150 && gg < 110 && b < 110) tag = " RED";
                if (r > 180 && gg > 180 && b > 180) tag = " WHITE";
                if (b > 150 && b > r + 30 && gg < 160) tag = " BLUE";
                if (tag != "" && x % 2 == 0) Console.WriteLine("x=" + x + " rgb(" + r + "," + gg + "," + b + ")" + tag);
            }
        }
    }
}
