// probe: where does ContextMenu.Show(form, pt) actually land?
// usage: MenuProbe.exe pos   -> request positive point (500, 40)
//        MenuProbe.exe neg   -> request negative-y point (500, -66)  (old chooser math)
// writes probe_out.txt next to the exe
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class Probe : Form
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    static string mode = "pos";
    ContextMenu m;
    Timer t = new Timer();
    int tick;

    Probe()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Rectangle b = Screen.PrimaryScreen.Bounds;
        Location = new Point(0, b.Height - 110);   // mimic the dock strip
        Size = new Size(b.Width, 110);
        BackColor = Color.Black;
        ShowInTaskbar = false;

        m = new ContextMenu();
        m.MenuItems.Add("ps", delegate { });
        m.MenuItems.Add("ToDesk", delegate { });

        t.Interval = 400;
        t.Tick += delegate
        {
            tick++;
            if (tick == 1)
                m.Show(this, mode == "pos" ? new Point(500, 40) : new Point(500, -66));
            else if (tick == 3)
            {
                IntPtr hw = FindWindow("#32768", null);
                string s = "no-menu-window";
                RECT r;
                if (hw != IntPtr.Zero && GetWindowRect(hw, out r))
                    s = r.L + "," + r.T + " - " + r.R + "," + r.B + " (w=" + (r.R - r.L) + " h=" + (r.B - r.T) + ")";
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe_out.txt"),
                    "mode=" + mode + " formTop=" + Top + " p2s(500,40)=" + PointToScreen(new Point(500, 40))
                    + " menuRect=" + s + "\r\n");
                keybd_event(0x1B, 0, 0, UIntPtr.Zero);   // ESC closes the tracked menu
            }
            else if (tick == 6) Application.Exit();
        };
        t.Start();
        Show();
    }

    [STAThread]
    static void Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }
        if (args.Length > 0) mode = args[0];
        try { File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe_out.txt")); } catch { }
        Application.Run(new Probe());
    }

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
}
