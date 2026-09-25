using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

class EnterWeChat
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);

    static void Main()
    {
        // find the green "进入微信" button above the dock (y < 950)
        int bestX = -1, bestY = -1, bestN = 0;
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
            int bestRow = -1;
            foreach (var kv in rows)
                if (kv.Value > bestN) { bestN = kv.Value; bestRow = kv.Key; }
            if (bestRow < 0) { Console.WriteLine("green button not found"); return; }
            // find x range of that row
            int minX = 2000, maxX = 0;
            for (int x = 600; x < 1300; x++)
            {
                Color c = bmp.GetPixel(x, bestRow);
                if (c.G > 130 && c.G > c.R + 40 && c.G > c.B + 40)
                { if (x < minX) minX = x; if (x > maxX) maxX = x; }
            }
            bestX = (minX + maxX) / 2;
            bestY = bestRow;
            Console.WriteLine("green button at (" + bestX + "," + bestY + ") row pixels " + bestN);
        }
        if (bestX < 0) return;
        SetCursorPos(bestX, bestY);
        Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Console.WriteLine("clicked 进入微信");
        Thread.Sleep(6000);
    }
}
