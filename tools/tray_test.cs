using System;
using System.Windows.Automation;

class TrayTest
{
    static void Main()
    {
        AutomationElement root = AutomationElement.RootElement;
        var bars = root.FindAll(TreeScope.Descendants,
            new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar),
                new PropertyCondition(AutomationElement.ClassNameProperty, "ToolbarWindow32")));
        Console.WriteLine("toolbars: " + bars.Count);
        foreach (AutomationElement bar in bars)
        {
            bool inTray = false;
            AutomationElement anc = TreeWalker.RawViewWalker.GetParent(bar);
            int depth = 0;
            while (anc != null && depth < 6)
            {
                if (anc.Current.ClassName == "TrayNotifyWnd") { inTray = true; break; }
                anc = TreeWalker.RawViewWalker.GetParent(anc);
                depth++;
            }
            if (!inTray) { Console.WriteLine("  toolbar (NOT tray), skipped"); continue; }
            var btns = bar.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            Console.WriteLine("  TRAY toolbar buttons: " + btns.Count);
            foreach (AutomationElement b in btns)
            {
                var r = b.Current.BoundingRectangle;
                Console.WriteLine(string.Format("    '{0}' rect=({1},{2},{3},{4})", b.Current.Name, (int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height));
            }
        }
    }
}
