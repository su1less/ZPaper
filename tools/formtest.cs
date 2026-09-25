using System;
using System.Drawing;
using System.Windows.Forms;
class FormTest
{
    [STAThread]
    static void Main()
    {
        try { Form f = new Form(); f.BackColor = Color.FromArgb(238, 16, 21, 36); Console.WriteLine("Form alpha238 BackColor: OK"); }
        catch (Exception ex) { Console.WriteLine("Form alpha238 FAIL: " + ex.Message); }
        try { FlowLayoutPanel g = new FlowLayoutPanel(); g.BackColor = Color.FromArgb(238, 16, 21, 36); Console.WriteLine("FlowPanel alpha238: OK"); }
        catch (Exception ex) { Console.WriteLine("FlowPanel alpha238 FAIL: " + ex.Message); }
    }
}
