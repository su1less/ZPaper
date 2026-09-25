using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

class EnterWeChat2
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);

    static bool FindGreen(out int cx, out int cy)
    {
        cx = cy = -1;
        using (var bmp = new Bitmap(1920, 1080))
        {
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
            var rows = new System.Collections.Generic.Dictionary<int, int>();
            for (int y = 300; y < 900; y += 2)
                for (int x = 600; x < 1300; x += 2)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.G > 130 && c.G > c.R + 40 && c.G > c.B + 40)
                    {
                        if (!rows.ContainsKey(y)) rows[y] = 0;
                        rows[y]++;
                    }
                }
            int bestRow = -1, bestN = 0;
            foreach (var kv in rows)
                if (kv.Value > bestN) { bestN = kv.Value; bestRow = kv.Key; }
            if (bestRow < 0) return false;
            int minX = 2000, maxX = 0, count = 0;
            for (int y = bestRow - 6; y <= bestRow + 6; y += 2)
                for (int x = 600; x < 1300; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.G > 130 && c.G > c.R + 40 && c.G > c.B + 40)
                    { if (x < minX) minX = x; if (x > maxX) maxX = x; count++; }
                }
            if (count < 20) return false;
            cx = (minX + maxX) / 2;
            cy = bestRow;
            return true;
        }
    }

    static void Main()
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            int cx, cy;
            if (!FindGreen(out cx, out cy))
            {
                Console.WriteLine("attempt " + attempt + ": green button gone -> page changed");
                return;
            }
            Console.WriteLine("attempt " + attempt + ": button at (" + cx + "," + cy + ") - clicking");
            SetCursorPos(cx, cy);
            Thread.Sleep(200);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(2500);
        }
        Console.WriteLine("button still present after 4 attempts");
    }
}
