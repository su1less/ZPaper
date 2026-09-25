// tickprobe.exe <seconds> - measures the dock's animation heartbeat jitter by
// sampling the dock's repaint cadence indirectly: watches the desktop for the
// dock window and counts message-driven repaints via GetWindowRect delta won't
// work - instead we measure SYSTEM timer behavior seen by a peer process:
// average gap between timeGetTime() advances > 0 in a tight sleep loop under
// timeBeginPeriod(1) both on and off. Simpler client-side proxy: sample
// timeGetTime resolution.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class TickProbe
{
    [DllImport("winmm.dll")] public static extern uint timeGetTime();

    static void Main(string[] a)
    {
        int secs = a.Length > 0 ? int.Parse(a[0]) : 5;
        // measure scheduler granularity as experienced by a busy peer loop
        var sw = Stopwatch.StartNew();
        int samples = 0; long sum = 0; long max = 0; long min = long.MaxValue;
        long last = timeGetTime();
        var spin = Stopwatch.StartNew();
        while (spin.Elapsed.TotalSeconds < secs)
        {
            long now = timeGetTime();
            if (now != last)
            {
                long d = now - last;
                if (d > 0 && d < 500) { sum += d; samples++; if (d > max) max = d; if (d < min) min = d; }
                last = now;
            }
        }
        Console.WriteLine(string.Format(
            "timer-tick samples={0} avgGap={1:F2}ms min={2}ms max={3}ms  (tight avg ~1ms = good, ~15.6ms = coarse)",
            samples, (double)sum / samples, min, max));
    }
}
