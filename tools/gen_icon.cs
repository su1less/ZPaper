// gen_icon.exe [out.ico] [preview.png] - ZPapaer app icon generator
// design: sunny blue gradient rounded card + bold white Z + small sun peeking
// from behind the Z's top-right corner. Sizes <=32 drop the sun and thicken
// the glyph so the Z stays crisp in the tray/taskbar.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class GenIcon
{
    static void Main(string[] a)
    {
        string icoPath = a.Length > 0 ? a[0] : "app.ico";
        string prevPath = a.Length > 1 ? a[1] : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(icoPath)) ?? ".", "..", "docs", "icon_preview.png");
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        Bitmap[] imgs = new Bitmap[sizes.Length];
        for (int i = 0; i < sizes.Length; i++) imgs[i] = Render(sizes[i]);

        WriteIco(icoPath, sizes, imgs);
        WritePreview(prevPath, imgs);
        Render(512).Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(prevPath)) ?? ".", "icon_512.png"), ImageFormat.Png);
        Console.WriteLine("written: " + icoPath + " , " + prevPath);
    }

    static PointF P(float x, float y) { return new PointF(x, y); }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
        p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
        p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
        p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Bitmap Render(int s)
    {
        Bitmap b = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.Clear(Color.Transparent);
            bool tiny = s <= 32;

            // ---- card: sunny sky-blue gradient rounded square ----
            RectangleF full = new RectangleF(0.5f, 0.5f, s - 1f, s - 1f);
            float rad = s * (tiny ? 0.25f : 0.225f);
            using (GraphicsPath card = Round(full, rad))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(full,
                    Color.FromArgb(255, 99, 181, 255), Color.FromArgb(255, 23, 90, 208), 90f))
                    g.FillPath(lg, card);
                // top-left sunshine glow
                using (GraphicsPath glow = new GraphicsPath())
                {
                    glow.AddEllipse(-s * 0.3f, -s * 0.35f, s * 1.1f, s * 1.1f);
                    using (PathGradientBrush pg = new PathGradientBrush(glow))
                    {
                        pg.CenterColor = Color.FromArgb(tiny ? 60 : 90, 255, 255, 255);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                        g.SetClip(card);
                        g.FillPath(pg, glow);
                        g.ResetClip();
                    }
                }
                // thin light rim keeps the card crisp on dark taskbars
                using (Pen rim = new Pen(Color.FromArgb(120, 210, 235, 255), Math.Max(1f, s / 110f)))
                    g.DrawPath(rim, card);
            }

            // ---- S monogram (surname initial) tucked behind the Z's top-right,
            // with rays radiating from behind it - the "shining S". Dropped on
            // tiny sizes where only the Z must survive. ----
            if (!tiny)
            {
                float r = s * 0.072f;
                float ux = s * 0.80f, uy = s * 0.175f;    // upper loop center
                float lx = s * 0.748f, ly = s * 0.325f;   // lower loop center (left of upper = S lean)
                using (Pen rp = new Pen(Color.FromArgb(195, 255, 255, 255), Math.Max(1.4f, s * 0.024f)))
                {
                    rp.StartCap = LineCap.Round; rp.EndCap = LineCap.Round;
                    for (int k = 0; k < 5; k++)
                    {
                        double ang = (-100 + k * 25) * Math.PI / 180.0;   // fan into the top-right
                        g.DrawLine(rp,
                            ux + (float)Math.Cos(ang) * r * 1.6f, uy + (float)Math.Sin(ang) * r * 1.6f,
                            ux + (float)Math.Cos(ang) * r * 2.05f, uy + (float)Math.Sin(ang) * r * 2.05f);
                    }
                }
                using (GraphicsPath sp = new GraphicsPath())
                {
                    // monoline S: upper loop CCW ending at the 6 o'clock junction,
                    // lower loop clockwise from 12 o'clock around to 7:30 - the two
                    // same-height junction points make the horizontal S middle
                    sp.AddArc(ux - r, uy - r, 2 * r, 2 * r, 320f, -230f);
                    sp.AddArc(lx - r, ly - r, 2 * r, 2 * r, 270f, 225f);
                    using (Pen spen = new Pen(Color.White, s * 0.062f))
                    {
                        spen.StartCap = LineCap.Round; spen.EndCap = LineCap.Round;
                        spen.LineJoin = LineJoin.Round;
                        g.DrawPath(spen, sp);
                    }
                }
            }

            // ---- the Z: top bar / diagonal / bottom bar as one path ----
            float gw = s * (tiny ? 0.64f : 0.55f);
            float gh = s * (tiny ? 0.70f : 0.62f);
            float gx = s * (tiny ? 0.18f : 0.15f);
            float gy = s * (tiny ? 0.15f : 0.21f);
            float t1 = gh * (tiny ? 0.20f : 0.165f);          // bar thickness
            float dCut = gw * (tiny ? 0.46f : 0.40f);         // diagonal horizontal cut
            float diagTop = gy + t1, diagBot = gy + gh - t1;
            using (GraphicsPath zp = new GraphicsPath())
            {
                zp.AddPolygon(new PointF[] { P(gx, gy), P(gx + gw, gy), P(gx + gw, gy + t1), P(gx, gy + t1) });
                zp.AddPolygon(new PointF[] { P(gx, gy + gh - t1), P(gx + gw, gy + gh - t1), P(gx + gw, gy + gh), P(gx, gy + gh) });
                zp.AddPolygon(new PointF[] {
                    P(gx + gw - dCut, diagTop), P(gx + gw, diagTop),
                    P(gx, diagBot), P(gx + dCut, diagBot) });
                if (!tiny)
                    using (Pen soft = new Pen(Color.White, s * 0.025f))
                    {
                        soft.LineJoin = LineJoin.Round;
                        g.DrawPath(soft, zp);
                    }
                using (SolidBrush zb = new SolidBrush(Color.White))
                    g.FillPath(zb, zp);
            }
        }
        return b;
    }

    // ---- ICO container: BMP(DIB) entries <=64, PNG for 128/256 ----
    static void WriteIco(string path, int[] sizes, Bitmap[] imgs)
    {
        MemoryStream[] data = new MemoryStream[sizes.Length];
        for (int i = 0; i < sizes.Length; i++)
            data[i] = sizes[i] <= 64 ? ToBmp(imgs[i]) : ToPng(imgs[i]);
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                byte b = (byte)(s == 256 ? 0 : s);
                w.Write(b); w.Write(b); w.Write((byte)0); w.Write((byte)0);
                w.Write((ushort)1); w.Write((ushort)32);
                w.Write((int)data[i].Length); w.Write((int)offset);
                offset += (int)data[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++) { data[i].Position = 0; data[i].CopyTo(fs); }
        }
    }

    static MemoryStream ToPng(Bitmap b)
    {
        MemoryStream ms = new MemoryStream();
        b.Save(ms, ImageFormat.Png);
        return ms;
    }

    static MemoryStream ToBmp(Bitmap b)
    {
        int s = b.Width;
        MemoryStream ms = new MemoryStream();
        // no using: disposing the writer would close the stream we still need
        BinaryWriter w = new BinaryWriter(ms);
        {
            w.Write((uint)40); w.Write((int)s); w.Write((int)(s * 2));
            w.Write((ushort)1); w.Write((ushort)32); w.Write((uint)0);
            w.Write((uint)(s * s * 4)); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = s - 1; y >= 0; y--)
                for (int x = 0; x < s; x++)
                {
                    Color c = b.GetPixel(x, y);
                    w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                }
            byte[] maskRow = new byte[((s + 31) / 32) * 4];
            for (int y = 0; y < s; y++) w.Write(maskRow);
        }
        return ms;
    }

    // ---- preview sheet: hero + native-size rows on light/dark ----
    static void WritePreview(string path, Bitmap[] imgs)
    {
        int W = 1500, H = 700;
        Bitmap sheet = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(246, 248, 252));
            // hero
            Bitmap hero = Render(512);
            g.DrawImage(hero, 30, 90, 512, 512);
            // name
            using (Font f = new Font("Segoe UI", 30, FontStyle.Bold))
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(30, 60, 120)))
                g.DrawString("ZPapaer", f, bb, 590, 50);
            using (Font f = new Font("Microsoft YaHei UI", 14))
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(120, 130, 160)))
                g.DrawString("动态桌面 · Z + 姓氏 S · 白蓝 · 阳光", f, bb, 594, 112);
            // rows: light then dark, sequential placement
            int x0 = 590, yL = 180, yD = 400, stripH = 170, gap = 34;
            g.FillRectangle(new SolidBrush(Color.White), x0 - 14, yL - 14, 880, stripH + 28);
            g.FillRectangle(new SolidBrush(Color.FromArgb(27, 36, 48)), x0 - 14, yD - 14, 880, stripH + 28);
            int[] order = new int[] { 0, 1, 2, 3, 4, 5, 6 };  // 16..256
            int total = 0;
            foreach (int i in order) total += imgs[i].Width;
            int x = x0 + (880 - (total + gap * (order.Length - 1))) / 2;
            foreach (int i in order)
            {
                int s = imgs[i].Width;
                g.DrawImage(imgs[i], x, yL + (stripH - s) / 2, s, s);
                g.DrawImage(imgs[i], x, yD + (stripH - s) / 2, s, s);
                x += s + gap;
            }
            hero.Dispose();
        }
        sheet.Save(path, ImageFormat.Png);
        sheet.Dispose();
    }
}
