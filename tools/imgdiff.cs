using System;
using System.Drawing;
class ImgDiff
{
    static void Main(string[] a)
    {
        using (Bitmap x = new Bitmap(a[0]), y = new Bitmap(a[1]))
        {
            long sum = 0; int n = 0;
            for (int yy = 0; yy < x.Height; yy += 4)
                for (int xx = 0; xx < x.Width; xx += 4)
                {
                    Color c1 = x.GetPixel(xx, yy), c2 = y.GetPixel(xx, yy);
                    sum += Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
                    n++;
                }
            Console.WriteLine("avg diff = " + (n == 0 ? 0 : sum / (double)(n * 3)).ToString("F2") + (sum / (double)(n * 3) > 0.3 ? "  -> ANIMATING" : "  -> static"));
        }
    }
}
