using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
class LtForm : Form
{
    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000; return cp; }
    }
    protected override bool ShowWithoutActivation { get { return true; } }
}
class LayTest2
{
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rp);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint ck, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [STAThread]
    static void Main(string[] a)
    {
        IntPtr host = (IntPtr)int.Parse(a[0]);
        LtForm f = new LtForm();
        f.FormBorderStyle = FormBorderStyle.None;
        f.ShowInTaskbar = false;
        f.StartPosition = FormStartPosition.Manual;
        f.Bounds = new Rectangle(0, 0, 1920, 1080);
        f.Show();
        SetParent(f.Handle, host);
        MoveWindow(f.Handle, 0, 0, 1920, 1080, true);
        IntPtr ex = GetWindowLongPtr(f.Handle, -20);
        SetWindowLongPtr(f.Handle, -20, (IntPtr)((long)ex | 0x80000 | 0x20));
        SetLayeredWindowAttributes(f.Handle, 0, 255, 2);
        ShowWindow(f.Handle, 5);
        int i = 0;
        DateTime until = DateTime.Now.AddSeconds(18);
        while (DateTime.Now < until)
        {
            using (Graphics g = Graphics.FromHwnd(f.Handle))
            {
                int c = (i * 8) % 255;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(c, 255 - c, 128)))
                    g.FillRectangle(b, 100, 750, 400, 250);
            }
            i++; Application.DoEvents(); Thread.Sleep(50);
        }
        Console.WriteLine("done " + i);
    }
}
