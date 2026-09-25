using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
class LayTest
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rp);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint ck, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [STAThread]
    static void Main(string[] a)
    {
        IntPtr host = (IntPtr)int.Parse(a[0]);
        bool layered = a[1] == "1";
        Form f = new Form();
        f.FormBorderStyle = FormBorderStyle.None;
        f.ShowInTaskbar = false;
        f.StartPosition = FormStartPosition.Manual;
        f.Bounds = new Rectangle(0, 0, 1920, 1080);
        f.CreateControl(); f.Show();
        SetParent(f.Handle, host);
        MoveWindow(f.Handle, 0, 0, 1920, 1080, true);
        if (layered)
        {
            IntPtr ex = GetWindowLongPtr(f.Handle, -20);
            SetWindowLongPtr(f.Handle, -20, (IntPtr)((long)ex | 0x80000 | 0x20));
            SetLayeredWindowAttributes(f.Handle, 0, 255, 2);
        }
        ShowWindow(f.Handle, 5);
        Console.WriteLine("host=" + host + " layered=" + layered + " hwnd=" + f.Handle);
        int i = 0;
        DateTime until = DateTime.Now.AddSeconds(20);
        while (DateTime.Now < until)
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(f.Handle))
                {
                    int c = (i * 8) % 255;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(c, 255 - c, 128)))
                        g.FillRectangle(b, 700, 300, 500, 300);
                }
            }
            catch { }
            i++;
            Application.DoEvents();
            Thread.Sleep(50);
        }
        Console.WriteLine("painted " + i + " frames");
    }
}
