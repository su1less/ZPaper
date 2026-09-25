// uiprobe.exe <dockHwnd> - measures the dock UI thread's message-response
// latency distribution (SendMessage round-trip = time the UI thread is busy or
// blocked) while sweeping the mouse across the dock to drive the springs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

class UiProbe
{
    [DllImport("user32.dll")] public static extern bool SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    static void Main(string[] a)
    {
        IntPtr dock = (IntPtr)long.Parse(a[0]);
        RECT r;
        if (!GetWindowRect(dock, out r)) { Console.WriteLine("bad hwnd"); return; }
        Console.WriteLine("dock rect " + r.L + "," + r.T + " - " + r.R + "," + r.B);

        var p = Process.GetProcessById(pidOf(dock));
        var cpu0 = p.TotalProcessorTime;
        var wall = Stopwatch.StartNew();

        // idle baseline 2s
        var idle = Sample(dock, 2000, null);
        // sweep 6s: sinusoidal across the icon row
        int cx = (r.L + r.R) / 2, amp = (r.R - r.L) / 2 - 60, y = r.B - 40;
        var busy = Sample(dock, 6000, delegate(double frac)
        {
            double ang = frac * Math.PI * 2 / 1.3;
            SetCursorPos(cx + (int)(Math.Sin(ang) * amp), y);
        });
        var cpuMs = (p.TotalProcessorTime - cpu0).TotalMilliseconds;
        double wallS = wall.Elapsed.TotalSeconds;

        Report("IDLE ", idle);
        Report("SWEEP", busy);
        Console.WriteLine(string.Format("ZPapaer CPU during test: {0:F0}ms of {1:F1}s wall = {2:F0}% of one core",
            cpuMs, wallS, cpuMs / (wallS * 10.0)));
    }

    static int pidOf(IntPtr h)
    {
        uint pid = 0;
        GetWindowThreadProcessId(h, out pid);
        return (int)pid;
    }
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    delegate void Drive(double frac);

    static List<double> Sample(IntPtr dock, int ms, Drive drive)
    {
        var lat = new List<double>();
        var sw = Stopwatch.StartNew();
        long t0 = Stopwatch.GetTimestamp();
        double freq = Stopwatch.Frequency;
        IntPtr res;
        int n = 0;
        while (sw.ElapsedMilliseconds < ms)
        {
            if (drive != null) drive(sw.ElapsedMilliseconds / (double)ms);
            long a = Stopwatch.GetTimestamp();
            SendMessageTimeout(dock, 0x00, IntPtr.Zero, IntPtr.Zero, 0, 2000, out res); // WM_NULL, block on UI thread
            long b = Stopwatch.GetTimestamp();
            lat.Add((b - a) / freq * 1000.0);
            n++;
        }
        return lat;
    }

    static void Report(string tag, List<double> lat)
    {
        lat.Sort();
        double avg = 0; foreach (double v in lat) avg += v;
        avg /= Math.Max(1, lat.Count);
        Console.WriteLine(string.Format(
            "{0}: n={1} avg={2:F2}ms p50={3:F2} p95={4:F2} p99={5:F2} max={6:F1}  over10ms={7} over30ms={8} over60ms={9}",
            tag, lat.Count, avg,
            P(lat, 0.50), P(lat, 0.95), P(lat, 0.99), lat[lat.Count - 1],
            CountOver(lat, 10), CountOver(lat, 30), CountOver(lat, 60)));
    }

    static double P(List<double> l, double f) { return l[Math.Min(l.Count - 1, (int)(l.Count * f))]; }
    static int CountOver(List<double> l, double t) { int c = 0; foreach (double v in l) if (v > t) c++; return c; }
}
