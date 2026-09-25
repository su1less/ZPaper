using System;
using System.Drawing;
using System.Windows.Forms;
class TransTest
{
    [STAThread]
    static void Main()
    {
        try { PictureBox pb = new PictureBox(); pb.BackColor = Color.Transparent; Console.WriteLine("PictureBox transparent: OK"); }
        catch (Exception ex) { Console.WriteLine("PictureBox transparent FAIL: " + ex.Message); }
        try { Label lb = new Label(); lb.BackColor = Color.Transparent; Console.WriteLine("Label transparent: OK"); }
        catch (Exception ex) { Console.WriteLine("Label transparent FAIL: " + ex.Message); }
        try { Panel pn = new Panel(); pn.BackColor = Color.Transparent; Console.WriteLine("Panel transparent: OK"); }
        catch (Exception ex) { Console.WriteLine("Panel transparent FAIL: " + ex.Message); }
        try { TextBox tb = new TextBox(); tb.BackColor = Color.Transparent; Console.WriteLine("TextBox transparent: OK"); }
        catch (Exception ex) { Console.WriteLine("TextBox transparent FAIL: " + ex.Message); }
    }
}
