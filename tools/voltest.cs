// e2e: find the volume slider window (272x116 layered, our pid), postclick the
// track at ~60%, read back the endpoint volume, restore. One process = no
// console focus churn that would close the slider mid-test.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class VolTest
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    static IntPtr MkL(int lo, int hi) { return (IntPtr)((hi << 16) | (lo & 0xFFFF)); }

    // ---- volume read (same interop as the app) ----
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDE { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDEnum { int E(int a, int b, out IntPtr c); int GDE(int f, int r, out IMMDev d); int GD(string id, out IMMDev d); int R(IntPtr c); int U(IntPtr c); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDev { int Act(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o); int Ops(int a, out IntPtr s); int GI([MarshalAs(UnmanagedType.LPWStr)] out string id); int GS(out int st); }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAEV
    {
        int RCC(IntPtr n); int UCC(IntPtr n); int GCC(out uint c);
        int SMVL(float l, ref Guid ctx); int GMVL(out float l);
        int SMVLS(float l, ref Guid ctx); int GMVLS(out float l);
        int SCVL(uint c, float l, ref Guid ctx); int SCVLS(uint c, float l, ref Guid ctx);
        int GCVL(uint c, out float l); int GCVLS(uint c, out float l);
        int SM(bool m, ref Guid ctx); int GM(out bool m);
        int SUP(ref Guid ctx); int SDN(ref Guid ctx); int QHS(out uint s);
        int GVR(out float min, out float max, out float step);
    }
    static Guid Ctx = Guid.Empty;
    static float ReadVol()
    {
        IMMDEnum en = (IMMDEnum)new MMDE();
        IMMDev d; en.GDE(0, 1, out d);
        object o; Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        d.Act(ref iid, 23, IntPtr.Zero, out o);
        float v; ((IAEV)o).GMVLS(out v);
        return v;
    }

    static uint target;
    static IntPtr slider;
    static void Main(string[] args)
    {
        foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName("ZPapaer")) target = (uint)p.Id;
        if (target == 0) { Console.WriteLine("zpapaer not running"); return; }
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == target && IsWindowVisible(h))
            {
                RECT r; GetWindowRect(h, out r);
                if (r.R - r.L == 272 && r.B - r.T == 116) slider = h;
            }
            return true;
        }, IntPtr.Zero);
        if (slider == IntPtr.Zero) { Console.WriteLine("SLIDER NOT OPEN"); return; }
        Console.WriteLine("slider hwnd=" + slider);

        float before = ReadVol();
        Console.WriteLine("volume before: " + (before * 100f).ToString("F0") + "%");

        // track geometry: x0=70, w=120 (272-70-82), click at 60% -> client x=142, y=66
        PostMessage(slider, 0x0200, IntPtr.Zero, MkL(120, 66));
        Thread.Sleep(80);
        PostMessage(slider, 0x0201, (IntPtr)1, MkL(142, 66));
        Thread.Sleep(60);
        PostMessage(slider, 0x0202, IntPtr.Zero, MkL(142, 66));
        Thread.Sleep(400);

        float after = ReadVol();
        Console.WriteLine("volume after click-at-60%: " + (after * 100f).ToString("F0") + "%");
        Console.WriteLine(after > before + 0.30f ? "VOLTEST OK (write went through)" : "VOLTEST INCONCLUSIVE");

        // restore
        IMMDEnum en2 = (IMMDEnum)new MMDE();
        IMMDev d2; en2.GDE(0, 1, out d2);
        object o2; Guid iid2 = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        d2.Act(ref iid2, 23, IntPtr.Zero, out o2);
        ((IAEV)o2).SMVLS(before, ref Ctx);
        Console.WriteLine("restored to " + (before * 100f).ToString("F0") + "%");
    }
}
