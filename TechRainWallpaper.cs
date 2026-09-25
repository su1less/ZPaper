// TechRainWallpaper v4 - animated wallpaper + Mac-style dock + desktop widgets
//  - wallpaper: 3 themes (rain city / aurora / nebula), weather-synced
//  - dock: precise macOS-style magnification (neighbor displacement, elastic panel,
//          jumbo 256px icons, bounce on launch, shadows)
//  - widgets: weather+clock (top-right), audio VU bars (bottom-left)
//  - native taskbar auto-hidden while running, restored on exit
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace TechRain
{
    // ============ shared weather state ============
    static class WeatherState
    {
        public static string City = "定位中…", Desc = "--", TempC = "--", Wind = "--", Humidity = "--", Region = "";
        public static float RainAmt = 0.55f, Kmph = 8;
        public static bool Snow = false;
        public static string[] DayWeek = { "--", "--", "--" };
        public static string[] DayTemp = { "--", "--", "--" };
        public static string[] DayDesc = { "--", "--", "--" };
        public static DateTime Updated = DateTime.MinValue;
        public static AutoResetEvent Wake = new AutoResetEvent(false);

        public static void Start()
        {
            Thread t = new Thread(Loop);
            t.IsBackground = true;
            t.Start();
        }

        static void Loop()
        {
            while (true)
            {
                try { Fetch(); }
                catch { }
                Wake.WaitOne(30 * 60 * 1000);
            }
        }

        static void Fetch()
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://wttr.in/?format=j1&lang=zh");
            req.Timeout = 9000; req.UserAgent = "curl/8.0";
            string json;
            using (var resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
                json = sr.ReadToEnd();
            var js = new JavaScriptSerializer();
            var root = (Dictionary<string, object>)js.DeserializeObject(json);
            var cc = (object[])root["current_condition"];
            var cur = (Dictionary<string, object>)cc[0];
            Desc = Text(Dict(cur, "weatherDesc"));
            TempC = Str(cur, "temp_C");
            string kmphS = Str(cur, "windspeedKmph");
            float kmph; float.TryParse(kmphS, out kmph);
            Kmph = kmph;
            Wind = string.Format("{0:0} km/h", kmph);
            Humidity = Str(cur, "humidity") + "%";
            try
            {
                var area = (object[])root["nearest_area"];
                var a0 = (Dictionary<string, object>)area[0];
                City = Text(Dict(a0, "areaName"));
                string region = Text(Dict(a0, "region"));
                if (region.Length > 0 && region != City) Region = region; else Region = "";
            }
            catch { }
            var days = root["weather"] as object[];
            if (days != null)
            {
                for (int i = 0; i < 3 && i < days.Length; i++)
                {
                    var d = (Dictionary<string, object>)days[i];
                    DateTime dt = DateTime.ParseExact(Str(d, "date"), "yyyy-MM-dd", null);
                    string[] wk = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
                    DayWeek[i] = wk[(int)dt.DayOfWeek];
                    DayTemp[i] = Str(d, "mintempC") + "~" + Str(d, "maxtempC") + "°";
                    string dd = "--";
                    var hourly = d["hourly"] as object[];
                    if (hourly != null && hourly.Length > 4)
                    {
                        var h4 = (Dictionary<string, object>)hourly[4];
                        dd = Text(Dict(h4, "lang_zh"));
                        if (dd == "--") dd = Text(Dict(h4, "weatherDesc"));
                    }
                    DayDesc[i] = dd;
                }
            }
            string dl = Desc.ToLower();
            float amt = 0; bool snow = false;
            if (dl.Contains("雷")) { amt = 1.0f; }
            else if (dl.Contains("雨")) amt = 0.85f;
            else if (dl.Contains("阵雨")) amt = 0.8f;
            else if (dl.Contains("毛毛") || dl.Contains("drizzle")) amt = 0.5f;
            else if (dl.Contains("雪") || dl.Contains("sleet")) { amt = 0.55f; snow = true; }
            else if (dl.Contains("雾") || dl.Contains("mist") || dl.Contains("霾")) amt = 0.15f;
            RainAmt = amt; Snow = snow;
            Updated = DateTime.Now;
            AppShell.OnWeatherFetched(amt, snow, Math.Max(0.12f, Math.Min(1.5f, kmph / 38f)));
        }

        static Dictionary<string, object> Dict(Dictionary<string, object> d, string key)
        {
            var a = d[key] as object[];
            if (a != null && a.Length > 0) return (Dictionary<string, object>)a[0];
            return null;
        }
        static string Text(Dictionary<string, object> d) { return d == null ? "--" : (string)d["value"]; }
        static string Str(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) ? Convert.ToString(v) : "--"; }
    }

    // ============ tray / global app shell ============
    static class AppShell
    {
        public static Scene SceneRef;
        public static bool Paused = false;
        public static bool IconsOverWallpaper = false;   // icon layer above the live wallpaper (hides the animation)
        public static object SceneLock = new object();
        public static bool ShowWidgets = true;
        public static bool TempShowTaskbar = false;
        public static WeatherWidgetForm WeatherW;
        public static VolumeWidgetForm VolumeW;
        public static event Action UIChanged;
        public static event Action<int> ThemeChanged;
        static NotifyIcon tray;
        static MenuItem pauseItem, widgetItem;
        static MenuItem[] themeItems = new MenuItem[4];
        static int theme = 1;

        public static void Init()
        {
            theme = Program.ReadTheme();
            Bitmap b = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(18, 30, 56));
                using (SolidBrush br = new SolidBrush(Color.FromArgb(255, 80, 210, 255)))
                    g.FillEllipse(br, 4, 4, 8, 8);
                using (Pen p = new Pen(Color.FromArgb(160, 200, 240, 255), 1.4f))
                { g.DrawLine(p, 3, 2, 3, 7); g.DrawLine(p, 7, 1, 7, 6); g.DrawLine(p, 11, 2, 11, 7); }
            }
            tray = new NotifyIcon();
            try { tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch
            {
                // exe icon missing: fall back to the hand-drawn mark
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.Clear(Color.FromArgb(18, 30, 56));
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(255, 80, 210, 255)))
                        g.FillEllipse(br, 4, 4, 8, 8);
                    using (Pen p = new Pen(Color.FromArgb(160, 200, 240, 255), 1.4f))
                    { g.DrawLine(p, 3, 2, 3, 7); g.DrawLine(p, 7, 1, 7, 6); g.DrawLine(p, 11, 2, 11, 7); }
                }
                tray.Icon = System.Drawing.Icon.FromHandle(b.GetHicon());
            }
            tray.Text = "动态桌面";
            tray.Visible = true;
            ContextMenu menu = new ContextMenu();
            MenuItem themes = new MenuItem("壁纸主题");
            for (int i = 1; i <= 4; i++)
            {
                int idx = i;
                MenuItem mi = new MenuItem(Scene.NameOf(i), delegate { SwitchTheme(idx); });
                mi.RadioCheck = true;
                mi.Checked = (i == theme);
                themes.MenuItems.Add(mi);
                themeItems[i - 1] = mi;
            }
            menu.MenuItems.Add(themes);
            widgetItem = new MenuItem("桌面小组件", delegate { ShowWidgets = !ShowWidgets; widgetItem.Checked = ShowWidgets; FireUI(); });
            widgetItem.Checked = true;
            menu.MenuItems.Add(widgetItem);
            pauseItem = new MenuItem("暂停动画", delegate { Paused = !Paused; pauseItem.Text = Paused ? "恢复动画" : "暂停动画"; });
            menu.MenuItems.Add(pauseItem);
            MenuItem ioTray = new MenuItem("图标浮在壁纸上", delegate { ToggleIconsOver(); });
            ioTray.Checked = IconsOverWallpaper;
            menu.MenuItems.Add(ioTray);
            menu.MenuItems.Add(new MenuItem("临时显示任务栏", delegate
            {
                TempShowTaskbar = !TempShowTaskbar;
                if (TempShowTaskbar) DockForm.ShowTaskbarNow();
                else DockForm.HideTaskbarNow();
            }));
            menu.MenuItems.Add(new MenuItem("立即刷新天气", delegate { WeatherState.Wake.Set(); }));
            menu.MenuItems.Add(new MenuItem("退出（恢复任务栏）", delegate { ExitApp(); }));
            tray.ContextMenu = menu;
        }

        public static int CurrentTheme { get { return theme; } }

        public static void SwitchTheme(int t)
        {
            theme = t;
            Program.WriteTheme(t);
            for (int i = 0; i < 4; i++) themeItems[i].Checked = (i + 1 == t);
            if (ThemeChanged != null) ThemeChanged(t);
        }

        public static void TogglePause()
        {
            Paused = !Paused;
            if (pauseItem != null) pauseItem.Text = Paused ? "恢复动画" : "暂停动画";
        }

        public static void ToggleIconsOver()
        {
            IconsOverWallpaper = !IconsOverWallpaper;
            if (WallRef != null) WallRef.ApplyLayerOrder();
        }

        public static WallpaperForm WallRef;

        public static void ToggleWidgetsMenu()
        {
            ShowWidgets = !ShowWidgets;
            if (widgetItem != null) widgetItem.Checked = ShowWidgets;
            FireUI();
        }

        public static void ToggleTaskbarMenu()
        {
            TempShowTaskbar = !TempShowTaskbar;
            if (TempShowTaskbar) DockForm.ShowTaskbarNow();
            else DockForm.HideTaskbarNow();
        }

        public static void OnWeatherFetched(float amt, bool snow, float windScale)
        {
            if (SceneRef != null) SceneRef.SetWeather(amt, snow, windScale);
            try
            {
                if (tray != null)
                    tray.Text = string.Format("动态桌面: {0} {1}° {2}", WeatherState.Desc, WeatherState.TempC, WeatherState.Wind);
            }
            catch { }
        }

        static void FireUI()
        {
            try
            {
                if (WeatherW != null) { if (ShowWidgets) WeatherW.Show(); else WeatherW.Hide(); }
                if (VolumeW != null) { if (ShowWidgets) VolumeW.Show(); else VolumeW.Hide(); }
            }
            catch { }
            if (UIChanged != null) UIChanged();
        }

        public static void ExitApp()
        {
            try { DockForm.RestoreTaskbarNow(); } catch { }
            Program.ExitRequested = true;
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            Application.ExitThread();
            Application.Exit();
        }

        public static void Dispose() { if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; } }
    }

    static class Program
    {
        public static bool ExitRequested;
        public static string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static int LogCounter = 0;

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(BaseDir, "log.txt"),
                    string.Format("{0:yyyy-MM-dd HH:mm:ss} {1}\r\n", DateTime.Now, msg));
            }
            catch { }
        }

        static void Crash(string where, object ex)
        {
            Log("FATAL " + where + ": " + ex);
        }

        [STAThread]
        static void Main(string[] args)
        {
            SetProcessDPIAware();
            try { timeBeginPeriod(1); } catch { }   // default 15.6ms tick makes every Timer drift/jitter
            BoostWorkingSet();
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            { Crash("AppDomain", e.ExceptionObject); };
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            { Crash("ThreadException", e.Exception); };
            if (args.Length > 0 && args[0] == "--snapshot")
            {
                int th = 1;
                if (args.Length > 2) int.TryParse(args[1], out th);
                Rectangle b = Screen.PrimaryScreen.Bounds;
                Scene s = new Scene(b.Width, b.Height, th);
                s.Simulate(5.0);
                s.SaveFrame(args[args.Length - 1]);
                return;
            }
            if (args.Length > 1 && args[0] == "--act")
            {
                // debug hook: run the exact click-time activation path from CLI
                // (TechRainWallpaper.exe --act QQ) without touching the running UI
                Application.EnableVisualStyles();
                DockForm.DebugActivate(args[1]);
                return;
            }
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            Application.EnableVisualStyles();
            WeatherState.Start();
            AppShell.Init();
            while (!ExitRequested)
            {
                WallpaperForm wf = null;
                DockForm dock = null;
                WeatherWidgetForm weatherW = null;
                VolumeWidgetForm volumeW = null;
                try
                {
                    wf = new WallpaperForm();
                    IntPtr hh = wf.Handle;
                    IntPtr wall = FindWallpaper();
                    if (wall != IntPtr.Zero) wf.AttachTo(wall);
                    dock = new DockForm();
                    dock.Show();
                    Rectangle scr = Screen.PrimaryScreen.Bounds;
                    weatherW = new WeatherWidgetForm(scr.Width - 360 - 16, 16);
                    weatherW.Show();
                    volumeW = new VolumeWidgetForm(16, scr.Height - 88 - 16);
                    volumeW.Show();
                    AppShell.WeatherW = weatherW;
                    AppShell.VolumeW = volumeW;
                    // sink widgets to the bottom of the normal z-order band (HWND_BOTTOM)
                    SetWindowPos(weatherW.Handle, (IntPtr)0, 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
                    SetWindowPos(volumeW.Handle, (IntPtr)0, 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
                    Application.Run();
                    // normal exit sets ExitRequested in AppShell.ExitApp first;
                    // reaching here without it means an anomaly -> retry loop, never die silently
                }
                catch (Exception) { Thread.Sleep(1500); }
                finally
                {
                    try { if (weatherW != null) weatherW.Dispose(); } catch { }
                    try { if (volumeW != null) volumeW.Dispose(); } catch { }
                    try { if (dock != null) { DockForm.RestoreTaskbarNow(); dock.Dispose(); } } catch { }
                    try { if (wf != null) { wf.Cleanup(); wf.Dispose(); } } catch { }
                }
            }
            AppShell.Dispose();
        }

        public static int ReadTheme()
        {
            try
            {
                string p = Path.Combine(BaseDir, "theme.txt");
                if (File.Exists(p)) { int t; int.TryParse(File.ReadAllText(p).Trim(), out t); if (t >= 1 && t <= 4) return t; }
            }
            catch { }
            return 1;
        }
        public static void WriteTheme(int t)
        {
            try { File.WriteAllText(Path.Combine(BaseDir, "theme.txt"), t.ToString()); } catch { }
        }
        public static void SetStaticWallpaper(string png)
        {
            try { SystemParametersInfo(20, 0, png, 3); } catch { }
        }

        // ---------- wallpaper layer plumbing ----------
        static GLEnumProc enumProc = new GLEnumProc(EnumCallback);
        static IntPtr foundWall;

        public static IntPtr FindWallpaper()
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;
            IntPtr res;
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 2, 1000, out res);
            // classic chain first: the WorkerW right AFTER the one hosting DefView
            // is the true wallpaper host (this is the boot-time layout that works).
            // Candidates must be VISIBLE: an invisible spawned WorkerW hosts a
            // surface that never paints (Invalidate is a no-op there)
            foundWall = IntPtr.Zero;
            EnumWindows(enumProc, IntPtr.Zero);
            if (foundWall != IntPtr.Zero) return foundWall;
            IntPtr under = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            if (under != IntPtr.Zero && IsWindowVisible(under)) return under;
            // EP fallback: any full-screen visible WorkerW EXCEPT the icon layer
            Rectangle sb = Screen.PrimaryScreen.Bounds;
            GLEnumProc wwCb = delegate(IntPtr h, IntPtr l)
            {
                StringBuilder cn = new StringBuilder(64);
                if (GetClassName(h, cn, 64) > 0 && cn.ToString() == "WorkerW" && IsWindowVisible(h))
                {
                    if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) return true;   // icon layer
                    RECT wr;
                    if (GetWindowRect(h, out wr)
                        && wr.Left <= sb.Left && wr.Top <= sb.Top && wr.Right >= sb.Right && wr.Bottom >= sb.Bottom)
                    { foundWall = h; return false; }
                }
                return true;
            };
            EnumWindows(wwCb, IntPtr.Zero);
            if (foundWall != IntPtr.Zero) return foundWall;
            return progman;
        }

        // every full-screen visible WorkerW that is NOT the icon layer (a WorkerW
        // hosting SHELLDLL_DefView must never be used: parenting our full-screen form
        // into it perturbs explorer and turns the wallpaper surface opaque)
        public static List<IntPtr> WallpaperHosts()
        {
            Rectangle sb = Screen.PrimaryScreen.Bounds;
            List<IntPtr> hosts = new List<IntPtr>();
            GLEnumProc wwCb = delegate(IntPtr h, IntPtr l)
            {
                StringBuilder cn = new StringBuilder(64);
                if (GetClassName(h, cn, 64) > 0 && cn.ToString() == "WorkerW" && IsWindowVisible(h))
                {
                    if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) return true;   // icon layer
                    RECT wr;
                    if (GetWindowRect(h, out wr)
                        && wr.Left <= sb.Left && wr.Top <= sb.Top && wr.Right >= sb.Right && wr.Bottom >= sb.Bottom)
                        hosts.Add(h);
                }
                return true;
            };
            EnumWindows(wwCb, IntPtr.Zero);
            return hosts;
        }

        static bool EnumCallback(IntPtr h, IntPtr l)
        {
            IntPtr def = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (def != IntPtr.Zero)
            {
                IntPtr wall = FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
                if (wall != IntPtr.Zero && IsWindowVisible(wall)) { foundWall = wall; return false; }
            }
            return true;
        }

        // ---------- win32 ----------
        public delegate bool GLEnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string name);
        [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
        [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool repaint);
        [DllImport("user32.dll")] public static extern bool EnumWindows(GLEnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsHungAppWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [StructLayout(LayoutKind.Sequential)]
        public class BITMAPINFO
        {
            public int biSize = 40;
            public int biWidth;
            public int biHeight;    // negative = top-down
            public short biPlanes = 1;
            public short biBitCount = 32;
            public int biCompression;   // 0 = BI_RGB
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int dx, int dy, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetProcessWorkingSetSizeEx(IntPtr h, IntPtr min, IntPtr max, uint flags);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr tok);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool LookupPrivilegeValue(string sys, string name, out long luid);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool AdjustTokenPrivileges(IntPtr tok, bool disableAll, ref TOKPRIV1LUID newst, int len, IntPtr prev, IntPtr ret);
        [StructLayout(LayoutKind.Sequential)] struct TOKPRIV1LUID { public int Count; public long Luid; public int Attr; }

        // When any app goes foreground the memory manager shaves background working
        // sets; with 1-2GB free RAM it shaved us to 146MB and the wallpaper/dock
        // buffers faulted back from the pagefile 18k times a SECOND - every
        // animation stalled 50-300ms waiting on disk with near-zero CPU. A hard
        // working-set floor keeps the hot pages resident.
        public static void BoostWorkingSet()
        {
            try
            {
                IntPtr tok;
                if (OpenProcessToken(GetCurrentProcess(), 0x20 | 0x8, out tok))
                {
                    TOKPRIV1LUID tp = new TOKPRIV1LUID();
                    tp.Count = 1;
                    tp.Attr = 2;   // SE_PRIVILEGE_ENABLED
                    long luid;
                    if (LookupPrivilegeValue(null, "SeIncreaseWorkingSetPrivilege", out luid))
                    {
                        tp.Luid = luid;
                        AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                    }
                    CloseHandle(tok);
                }
                IntPtr min = (IntPtr)(320L * 1024 * 1024), max = (IntPtr)(768L * 1024 * 1024);
                bool hard = SetProcessWorkingSetSizeEx(GetCurrentProcess(), min, max, 0x1);   // QUOTA_LIMITS_HARDWS_MIN_ENABLE
                if (!hard)
                    SetProcessWorkingSetSizeEx(GetCurrentProcess(), min, max, 0);   // soft floor as fallback
                Log("[MEM] working-set floor 320MB hard=" + hard);
            }
            catch (Exception ex) { Log("[MEM] working-set floor failed: " + ex.Message); }
        }
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] public static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr size, uint type, uint protect);
        [DllImport("kernel32.dll")] public static extern bool VirtualFreeEx(IntPtr p, IntPtr a, UIntPtr size, uint type);
        [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr p, IntPtr base_, byte[] buf, UIntPtr size, out IntPtr read);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        // 64-bit user32 only exports the W variants - without EntryPoint these
        // throw EntryPointNotFoundException on EVERY call (the wallpaper's
        // click-through/layered styles were silently never applied)
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SystemParametersInfo(int a, int b, string c, int d);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [StructLayout(LayoutKind.Sequential)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [DllImport("user32.dll")]
        public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dstDc, ref Point dst, ref Size size, IntPtr srcDc, ref Point src, int crKey, ref BLENDFUNCTION blend, int flags);
        [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

        // taskbar auto-hide (appbar)
        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge;
            public RECT rc; public IntPtr lParam;
        }
        [DllImport("shell32.dll")] public static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA abd);
        public const uint ABM_GETSTATE = 4, ABM_SETSTATE = 0x0A, ABS_AUTOHIDE = 1;

        // shell icon cache (jumbo 256px icons)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon; public int iIcon; public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
        [DllImport("shell32.dll")]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentProcessId();

        // our full-screen panels (launcher / desktop) cover the wallpaper: the
        // self-heal's screen sample would read the STATIC pixels of our own
        // window and wrongly kick the attachment - the heal must stand down
        // while we own the foreground
        public static bool ForegroundBelongsToUs()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            return pid == GetCurrentProcessId();
        }

        public static bool ForegroundIsFullscreen(IntPtr self, out IntPtr fgOut)
        {
            fgOut = IntPtr.Zero;
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == self) return false;
            StringBuilder sb = new StringBuilder(64);
            GetClassName(fg, sb, 64);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "SHELLDLL_DefView") return false;
            RECT r; GetWindowRect(fg, out r);
            Rectangle b = Screen.FromHandle(fg).Bounds;
            fgOut = fg;
            // ANY window covering the screen counts (maximized included): the user
            // wants the dock hidden and the wallpaper paused then, to save resources
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }
    }

    // ============ scene (wallpaper renderer) ============
    class Scene
    {
        public int W, H, Theme;
        Bitmap bg;
        // per-section frame-cost profile ([SCENE] log lines)
        static double ptBg, ptStars, ptHole, ptShoots;
        static int ptN;

        // scale every pixel (PArgb: alpha AND rgb, keeps premultiplication
        // consistent) - replaces a constant ColorMatrix ImageAttribute at draw
        // time; the CM path is per-pixel software and starved the GDI lock
        static void BakeAlphaMul(Bitmap b, float mul)
        {
            Rectangle r = new Rectangle(0, 0, b.Width, b.Height);
            BitmapData d = b.LockBits(r, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                int bytes = Math.Abs(d.Stride) * d.Height;
                byte[] px = new byte[bytes];
                Marshal.Copy(d.Scan0, px, 0, bytes);
                for (int i = 0; i + 3 < bytes; i += 4)
                {
                    px[i] = (byte)(px[i] * mul);
                    px[i + 1] = (byte)(px[i + 1] * mul);
                    px[i + 2] = (byte)(px[i + 2] * mul);
                    px[i + 3] = (byte)(px[i + 3] * mul);
                }
                Marshal.Copy(px, 0, d.Scan0, bytes);
            }
            finally { b.UnlockBits(d); }
        }
        Bitmap frame;
        Random rnd = new Random();

        struct Drop { public float X, Y, Len, Spd, Alpha, Width, Jit; }
        struct Splash { public float X, Y, VX, VY, T0; }
        struct Ripple { public float X, T0; }
        struct Star { public float X, Y, Base, Phase, Spd; public int Sz; }
        struct Shoot { public float X, Y, VX, VY, T0; }
        struct Fog { public float X, Y, Sc, Spd; public int Spr; }
        struct Glow { public float X, Y, R, Ph, Spd; public int Ci; }
        struct BHP { public float Th, D, Sz, Spd, Ph, Fl; }          // accretion disk particle
        struct Planet { public float Th, A, B, Sz, Spd; public int C; }

        List<Drop>[] rain = new List<Drop>[3];
        List<Splash> splashes = new List<Splash>();
        List<Ripple> ripples = new List<Ripple>();
        List<Star> stars = new List<Star>();
        List<Shoot> shoots = new List<Shoot>();
        List<PointF> bolt = new List<PointF>();
        List<List<PointF>> boltBranches = new List<List<PointF>>();
        List<Fog> fogs = new List<Fog>();
        List<Glow> glows = new List<Glow>();
        List<BHP> disk = new List<BHP>();
        List<Planet> planets = new List<Planet>();
        float bhX, bhY, bhR;
        double tiltPhase;
        float[][] band = new float[3][];

        Bitmap[] fogSpr; Bitmap[][] fogBaked;
        PathGradientBrush[] glowPgb;
        Pen[][] rainPens;
        Pen[] ripplePens; SolidBrush snowBrush;
        SolidBrush[] starBrushes;
        Brush[] splashBrush;

        public double T;
        public float Rain = 0.55f, RainTarget = 0.55f;
        public float WindBase = 0.35f, WindTarget = 0.35f;
        public bool Snow = false, SnowTarget = false;
        float snowMix = 0f;

        double nextFlash = 6, flashT0 = -10, boltVis = -10;
        double nextGust = 5, lastGustT = -10, gustAmp = 0;
        double nextShoot = 4;

        static readonly string[] ThemeNames = { "雨夜霓虹", "极光雪夜", "深空星云", "黑洞吸积盘" };
        public static string NameOf(int t) { return ThemeNames[t - 1]; }

        public Scene(int w, int h, int theme)
        {
            W = w; H = h; Theme = theme;
            bg = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            frame = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            MakeSprites(); MakePens();
            RenderBase();
            InitParticles();
            if (Theme == 1) { Rain = RainTarget = 0.55f; }
        }

        void MakePens()
        {
            rainPens = new Pen[3][];
            float[] wdt = { 1.0f, 1.5f, 2.3f };
            for (int l = 0; l < 3; l++)
            {
                rainPens[l] = new Pen[4];
                for (int a = 0; a < 4; a++)
                {
                    int al = 45 + a * 28 + l * 18;
                    rainPens[l][a] = new Pen(Color.FromArgb(Math.Min(220, al), 175, 215, 255), wdt[l]);
                }
            }
            ripplePens = new Pen[3];
            for (int i = 0; i < 3; i++) ripplePens[i] = new Pen(Color.FromArgb(55 - i * 17, 160, 220, 255), 1.2f);
            snowBrush = new SolidBrush(Color.FromArgb(200, 235, 245, 255));
            starBrushes = new SolidBrush[12];
            for (int i = 0; i < 12; i++) starBrushes[i] = new SolidBrush(Color.FromArgb(18 + i * 18, 215, 235, 255));
            splashBrush = new Brush[3];
            for (int i = 0; i < 3; i++) splashBrush[i] = new SolidBrush(Color.FromArgb(90 + i * 50, 190, 225, 255));
        }

        void MakeSprites()
        {
            fogSpr = new Bitmap[3];
            int[] fs = { 300, 440, 620 };
            for (int i = 0; i < 3; i++)
            {
                int s = fs[i];
                Bitmap b = new Bitmap(s, s / 2);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddEllipse(0, 0, s, s / 2);
                        using (PathGradientBrush pg = new PathGradientBrush(p))
                        {
                            pg.CenterColor = Color.FromArgb(30, 150, 180, 210);
                            pg.SurroundColors = new Color[] { Color.FromArgb(0, 150, 180, 210) };
                            g.FillPath(pg, p);
                        }
                    }
                }
                fogSpr[i] = b;
            }
            // baked alpha variants replace the ColorMatrix ImageAttributes at draw
            // time: the CM path is per-pixel software and cost ~half the frame on
            // theme 1/2. fogBaked[sprite][alphaIdx], alphaIdx matches the old attr.
            fogBaked = new Bitmap[3][];
            for (int sI = 0; sI < 3; sI++)
            {
                fogBaked[sI] = new Bitmap[3];
                for (int aI = 0; aI < 3; aI++)
                {
                    Bitmap b = new Bitmap(fogSpr[sI].Width, fogSpr[sI].Height, PixelFormat.Format32bppPArgb);
                    using (Graphics g = Graphics.FromImage(b))
                        g.DrawImage(fogSpr[sI], 0, 0, b.Width, b.Height);
                    BakeAlphaMul(b, 0.35f + aI * 0.3f);
                    fogBaked[sI][aI] = b;
                }
            }
            Color[] gcol = { Color.FromArgb(90, 80, 200, 255), Color.FromArgb(90, 170, 90, 255), Color.FromArgb(90, 220, 120, 255) };
            // 6 drifting radial glows used to be 512px sprites stretched per frame
            // (~5M software-blended px/frame, one full core, convoying the kernel
            // GDI lock against the dock). A unit-ellipse PathGradientBrush scaled
            // by the world transform paints the same falloff for a fraction of it.
            glowPgb = new PathGradientBrush[3];
            for (int i = 0; i < 3; i++)
            {
                GraphicsPath p = new GraphicsPath();
                p.AddEllipse(0f, 0f, 1f, 1f);
                PathGradientBrush pg = new PathGradientBrush(p);
                // center alpha 90*0.16 matches the ColorMatrix the sprites drew with
                pg.CenterColor = Color.FromArgb((int)(90 * 0.16f), gcol[i]);
                pg.SurroundColors = new Color[] { Color.FromArgb(0, gcol[i].R, gcol[i].G, gcol[i].B) };
                glowPgb[i] = pg;
                p.Dispose();
            }
        }

        void RenderBase()
        {
            string[] files = { "bg\\city1.jpg", "bg\\aurora1.jpg", "bg\\nebula1.jpg", "bg\\nebula2.jpg" };
            string path = Path.Combine(Program.BaseDir, files[Theme - 1]);
            using (Graphics g = Graphics.FromImage(bg))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.FillRectangle(Brushes.Black, 0, 0, W, H);
                if (File.Exists(path))
                {
                    using (Image src = Image.FromFile(path))
                    {
                        float s = Math.Max(W / (float)src.Width, H / (float)src.Height);
                        int rw = (int)(src.Width * s), rh = (int)(src.Height * s);
                        g.DrawImage(src, (W - rw) / 2, (H - rh) / 2, rw, rh);
                    }
                }
                else
                {
                    using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                        Color.FromArgb(255, 6, 9, 20), Color.FromArgb(255, 16, 30, 58), 90f))
                        g.FillRectangle(lg, 0, 0, W, H);
                }
                using (LinearGradientBrush t = new LinearGradientBrush(new Rectangle(0, 0, W, (int)(H * 0.2)),
                    Color.FromArgb(110, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90f))
                    g.FillRectangle(t, 0, 0, W, (int)(H * 0.2));
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, (int)(H * 0.7), W, (int)(H * 0.3)),
                    Color.FromArgb(0, 0, 0, 0), Color.FromArgb(130, 0, 0, 0), 90f))
                    g.FillRectangle(b, 0, (int)(H * 0.7), W, (int)(H * 0.3));
                using (LinearGradientBrush l = new LinearGradientBrush(new Rectangle(0, 0, (int)(W * 0.07), H),
                    Color.FromArgb(60, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 0f))
                    g.FillRectangle(l, 0, 0, (int)(W * 0.07), H);
                using (LinearGradientBrush r = new LinearGradientBrush(new Rectangle((int)(W * 0.93), 0, (int)(W * 0.07) + 1, H),
                    Color.FromArgb(0, 0, 0, 0), Color.FromArgb(60, 0, 0, 0), 0f))
                    g.FillRectangle(r, (int)(W * 0.93), 0, (int)(W * 0.07) + 1, H);
            }
        }

        void InitParticles()
        {
            int[] counts = { 150, 85, 40 };
            for (int l = 0; l < 3; l++)
            {
                rain[l] = new List<Drop>();
                for (int i = 0; i < counts[l]; i++) rain[l].Add(NewDrop(l, true));
            }
            int starCount = Theme == 3 || Theme == 4 ? 220 : 140;
            for (int i = 0; i < starCount; i++)
            {
                Star s; s.X = (float)rnd.NextDouble() * W;
                s.Y = (float)rnd.NextDouble() * H * (Theme == 2 ? 0.48f : 0.75f);
                s.Base = 0.25f + (float)rnd.NextDouble() * 0.75f; s.Phase = (float)(rnd.NextDouble() * Math.PI * 2);
                s.Spd = 0.4f + (float)rnd.NextDouble() * 1.8f; s.Sz = rnd.Next(2);
                stars.Add(s);
            }
            if (Theme == 1) { Snow = false; SnowTarget = false; }
            if (Theme == 2)
            {
                for (int i = 0; i < 130; i++) { var d = NewDrop(1, true); rain[1].Add(d); }
                Snow = true; SnowTarget = true; snowMix = 1f;
            }
            if (Theme == 3)
            {
                for (int i = 0; i < 6; i++)
                {
                    Glow gl; gl.X = (float)rnd.NextDouble() * W; gl.Y = (float)rnd.NextDouble() * H;
                    gl.R = 0.6f + (float)rnd.NextDouble() * 1.0f; gl.Ph = (float)(rnd.NextDouble() * Math.PI * 2);
                    gl.Spd = 2 + (float)rnd.NextDouble() * 5; gl.Ci = rnd.Next(3);
                    glows.Add(gl);
                }
            }
            if (Theme == 4)
            {
                bhX = W * 0.44f; bhY = H * 0.44f; bhR = H * 0.115f;
                for (int i = 0; i < 380; i++)
                {
                    BHP p;
                    p.Th = (float)(rnd.NextDouble() * Math.PI * 2);
                    double bias = Math.Pow(rnd.NextDouble(), 1.7);    // denser inside
                    p.D = bhR * (1.16f + (float)bias * 1.55f);
                    p.Spd = 1.05f * (float)Math.Pow((bhR * 1.55f) / p.D, 1.5);   // Keplerian: inner faster
                    p.Sz = 1.0f + (float)rnd.NextDouble() * 2.6f;
                    p.Ph = (float)(rnd.NextDouble() * Math.PI * 2);
                    p.Fl = 1.5f + (float)rnd.NextDouble() * 4.0f;
                    disk.Add(p);
                }
                Planet p1; p1.Th = (float)(rnd.NextDouble() * 6.28); p1.A = bhR * 4.4f; p1.B = bhR * 1.35f;
                p1.Sz = bhR * 0.16f; p1.Spd = 0.055f; p1.C = 0; planets.Add(p1);
                Planet p2; p2.Th = (float)(rnd.NextDouble() * 6.28); p2.A = bhR * 3.2f; p2.B = bhR * 1.0f;
                p2.Sz = bhR * 0.11f; p2.Spd = -0.085f; p2.C = 1; planets.Add(p2);
                tiltPhase = rnd.NextDouble() * 6.28;
            }
            int fogN = Theme == 1 ? 5 : (Theme == 4 ? 0 : 3);
            for (int i = 0; i < fogN; i++)
            {
                Fog f; f.X = (float)rnd.NextDouble() * W; f.Y = H * (0.35f + (float)rnd.NextDouble() * 0.55f);
                f.Sc = 1.2f + (float)rnd.NextDouble() * 2.2f; f.Spd = 6 + (float)rnd.NextDouble() * 20;
                f.Spr = rnd.Next(3); fogs.Add(f);
            }
            for (int i = 0; i < 3; i++)
            {
                band[i] = new float[] {
                    (float)(0.16 + i * 0.09), (float)(34 + rnd.NextDouble() * 26),
                    (float)(0.0035 + rnd.NextDouble() * 0.002), (float)(rnd.NextDouble() * 6.28),
                    (float)(0.10 + rnd.NextDouble() * 0.06), (float)(14 + rnd.NextDouble() * 18),
                    (float)(0.009 + rnd.NextDouble() * 0.004), (float)(rnd.NextDouble() * 6.28),
                    (float)(0.05 + rnd.NextDouble() * 0.05), (float)(150 + rnd.NextDouble() * 120) };
            }
        }

        Drop NewDrop(int layer, bool anywhere)
        {
            float[][] p = { new float[] { 6, 12, 300, 460, 40, 70 },
                            new float[] { 13, 22, 560, 760, 80, 125 },
                            new float[] { 26, 44, 860, 1120, 130, 175 } };
            Drop d;
            d.X = (float)(rnd.NextDouble() * 1.4 - 0.2) * W;
            d.Y = anywhere ? (float)rnd.NextDouble() * H : -(float)(30 + rnd.NextDouble() * 140);
            d.Len = p[layer][0] + (float)rnd.NextDouble() * (p[layer][1] - p[layer][0]);
            d.Spd = p[layer][2] + (float)rnd.NextDouble() * (p[layer][3] - p[layer][2]);
            d.Alpha = p[layer][4] + (float)rnd.NextDouble() * (p[layer][5] - p[layer][4]);
            d.Width = 1f;
            d.Jit = 0.6f + (float)rnd.NextDouble() * 0.75f;
            return d;
        }

        public void SetWeather(float rainAmt, bool snow, float windBase)
        {
            if (Theme == 1) { RainTarget = rainAmt; SnowTarget = false; }
            else if (Theme == 2) { RainTarget = Math.Max(0.25f, rainAmt); SnowTarget = true; }
            else { RainTarget = 0f; SnowTarget = false; }
            WindTarget = windBase;
        }
        public void Simulate(double sec) { for (double e = 0; e < sec; e += 1 / 30.0) Update(1 / 30f); }

        float WindNow()
        {
            double gust = 0;
            double gs = T - lastGustT;
            if (gs > 0 && gs < 1.8) gust = gustAmp * Math.Sin(Math.PI * gs / 1.8);
            if (T - lastGustT > 1.8 && T > nextGust)
            {
                nextGust = T + 6 + rnd.NextDouble() * 16;
                lastGustT = T; gustAmp = 0.4 + rnd.NextDouble() * 0.8;
            }
            return WindBase + (float)(0.3 * Math.Sin(T * 0.11) + 0.2 * Math.Sin(T * 0.023 + 2.1) + gust);
        }

        public void Update(float dt)
        {
            T += dt;
            Rain += (RainTarget - Rain) * Math.Min(1, dt * 0.25f);
            WindBase += (WindTarget - WindBase) * Math.Min(1, dt * 0.25f);
            if (Snow != SnowTarget)
            {
                snowMix += (SnowTarget ? 1 : -1) * dt * 0.4f;
                snowMix = Math.Max(0, Math.Min(1, snowMix));
                if (snowMix == (SnowTarget ? 1f : 0f)) Snow = SnowTarget;
            }
            float wind = WindNow();

            for (int l = 0; l < 3; l++)
            {
                List<Drop> list = rain[l];
                int active = (int)(list.Count * Math.Min(1f, 0.1f + Rain));
                for (int i = 0; i < list.Count; i++)
                {
                    if (i >= active) { Drop dead = list[i]; dead.Y = -9999; list[i] = dead; continue; }
                    Drop d = list[i];
                    if (d.Y < -9000) d = NewDrop(l, false);
                    if (snowMix < 0.5f) { d.X += wind * d.Spd * 0.55f * dt; d.Y += d.Spd * dt; }
                    else
                    {
                        d.X += ((float)Math.Sin(d.Len + T * 1.6) * 16 + wind * 46) * dt;
                        d.Y += d.Spd * 0.13f * dt;
                    }
                    if (d.X < -W * 0.25) d.X += W * 1.4f; else if (d.X > W * 1.15) d.X -= W * 1.4f;
                    if (d.Y > H - 4)
                    {
                        if (snowMix < 0.5f && Rain > 0.3f)
                        {
                            if (l == 2 && splashes.Count < 120 && rnd.NextDouble() < 0.7)
                            {
                                int n = 2 + rnd.Next(3);
                                for (int k = 0; k < n; k++)
                                {
                                    Splash s; s.X = d.X; s.Y = H - 6;
                                    s.VX = (float)(rnd.NextDouble() * 120 - 60);
                                    s.VY = -(float)(40 + rnd.NextDouble() * 90);
                                    s.T0 = (float)T;
                                    splashes.Add(s);
                                }
                            }
                            if (l >= 1 && ripples.Count < 80 && rnd.NextDouble() < 0.5)
                                ripples.Add(new Ripple { X = d.X, T0 = (float)T });
                        }
                        d = NewDrop(l, false);
                    }
                    list[i] = d;
                }
            }
            for (int i = splashes.Count - 1; i >= 0; i--)
            {
                Splash s = splashes[i];
                s.VY += 620 * dt; s.X += s.VX * dt; s.Y += s.VY * dt;
                splashes[i] = s;
                if (T - s.T0 > 0.5 || s.Y > H) splashes.RemoveAt(i);
            }
            for (int i = ripples.Count - 1; i >= 0; i--) if (T - ripples[i].T0 > 0.85) ripples.RemoveAt(i);

            for (int i = 0; i < fogs.Count; i++)
            {
                Fog f = fogs[i];
                f.X += (f.Spd + WindBase * 60) * dt;
                if (f.X - fogSpr[f.Spr].Width * f.Sc > W) f.X = -fogSpr[f.Spr].Width * f.Sc;
                fogs[i] = f;
            }
            for (int i = 0; i < glows.Count; i++) { Glow gl = glows[i]; gl.Ph += gl.Spd * dt; glows[i] = gl; }
            if (Theme == 4)
            {
                for (int i = 0; i < disk.Count; i++) { BHP p = disk[i]; p.Th += p.Spd * dt; disk[i] = p; }
                for (int i = 0; i < planets.Count; i++) { Planet p = planets[i]; p.Th += p.Spd * dt; planets[i] = p; }
                tiltPhase += dt * 0.05;
            }
            if (Theme == 1 && T > nextFlash)
            {
                flashT0 = T; boltVis = T;
                nextFlash = T + 9 + rnd.NextDouble() * 20;
                MakeBolt();
            }
            if (Theme >= 2 && T > nextShoot)
            {
                Shoot s;
                s.X = (float)(W * (0.15 + rnd.NextDouble() * 0.7));
                s.Y = (float)(H * (0.05 + rnd.NextDouble() * 0.2));
                float ang = (float)(0.35 + rnd.NextDouble() * 0.3);
                float spd = 900 + (float)rnd.NextDouble() * 500;
                s.VX = -(float)Math.Cos(ang) * spd; s.VY = (float)Math.Sin(ang) * spd;
                s.T0 = (float)T;
                shoots.Add(s);
                nextShoot = T + 8 + rnd.NextDouble() * 18;
            }
            for (int i = shoots.Count - 1; i >= 0; i--) if (T - shoots[i].T0 > 0.9) shoots.RemoveAt(i);
        }

        void MakeBolt()
        {
            bolt.Clear(); boltBranches.Clear();
            float x = (float)(W * (0.15 + rnd.NextDouble() * 0.7));
            float y = -10;
            bolt.Add(new PointF(x, y));
            List<PointF> branch = null;
            while (y < H * 0.5)
            {
                y += 26 + (float)rnd.NextDouble() * 34;
                x += (float)(rnd.NextDouble() * 56 - 28);
                bolt.Add(new PointF(x, y));
                if (branch == null && rnd.NextDouble() < 0.3 && y > H * 0.1)
                {
                    branch = new List<PointF>();
                    branch.Add(new PointF(x, y));
                }
                else if (branch != null && branch.Count < 6)
                {
                    float bx = branch[branch.Count - 1].X + (float)(rnd.NextDouble() * 50 - 35);
                    float by = branch[branch.Count - 1].Y + 22 + (float)rnd.NextDouble() * 26;
                    branch.Add(new PointF(bx, by));
                }
            }
            if (branch != null && branch.Count > 2) boltBranches.Add(branch);
        }

        void DrawAurora(Graphics g)
        {
            int bw = Math.Max(64, W / 3), bh = Math.Max(36, H / 3);
            if (auroraBuf == null) auroraBuf = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
            Color[][] cols = {
                new Color[] { Color.FromArgb(44, 55, 235, 150), Color.FromArgb(0, 55, 235, 150) },
                new Color[] { Color.FromArgb(36, 0, 205, 185), Color.FromArgb(0, 0, 205, 185) },
                new Color[] { Color.FromArgb(28, 130, 90, 240), Color.FromArgb(0, 130, 90, 240) } };
            using (Graphics ab = Graphics.FromImage(auroraBuf))
            {
                ab.Clear(Color.Transparent);
                ab.SmoothingMode = SmoothingMode.AntiAlias;
                float sc = bw / (float)W;
                for (int i = 0; i < 3; i++)
                {
                    float[] p = band[i];
                    float baseY = p[0] * H * sc;
                    float step = 10;
                    int n = (int)(bw / step) + 2;
                    PointF[] top = new PointF[n];
                    float minY = 1e9f, maxY = -1e9f;
                    for (int k = 0; k < n; k++)
                    {
                        float xx = k * step;
                        float mod1 = 0.75f + 0.25f * (float)Math.Sin(T * 0.07 + i * 2.1);
                        float yy = baseY
                            + p[1] * sc * mod1 * (float)Math.Sin(xx / sc * p[2] + p[3] + T * p[4])
                            + p[5] * sc * (float)Math.Sin(xx / sc * p[6] + p[7] + T * p[8]);
                        top[k] = new PointF(xx, yy);
                        if (yy < minY) minY = yy;
                        if (yy > maxY) maxY = yy;
                    }
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddLines(top);
                        for (int k = n - 1; k >= 0; k--)
                            path.AddLine(top[k].X, top[k].Y + p[9] * sc, top[Math.Max(0, k - 1)].X, top[Math.Max(0, k - 1)].Y + p[9] * sc);
                        path.CloseFigure();
                        using (LinearGradientBrush lb = new LinearGradientBrush(
                            new Rectangle(0, (int)minY, bw, (int)(maxY - minY + p[9] * sc + 4)),
                            cols[i][0], cols[i][1], 90f))
                        {
                            lb.SetBlendTriangularShape(0.3f);
                            ab.FillPath(lb, path);
                        }
                    }
                }
            }
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(auroraBuf, new Rectangle(0, 0, W, H));
            g.InterpolationMode = InterpolationMode.Default;
        }
        Bitmap auroraBuf;

        public void Draw(Graphics g)
        {
            double bg0 = Stopwatch.GetTimestamp();
            g.DrawImage(bg, 0, 0);
            ptBg += (Stopwatch.GetTimestamp() - bg0) * 1000.0 / Stopwatch.Frequency;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float wind = WindNow();

            if (Theme == 1)
            {
                for (int i = 0; i < fogs.Count; i++)
                {
                    Fog f = fogs[i];
                    Bitmap b = fogBaked[f.Spr][1];
                    int dw = (int)(b.Width * f.Sc), dh = (int)(b.Height * f.Sc);
                    g.DrawImage(b, new Rectangle((int)f.X, (int)f.Y - dh / 2, dw, dh));
                }
                for (int l = 0; l <= 1; l++) DrawRainLayer(g, l, wind);
                for (int i = 0; i < ripples.Count; i++)
                {
                    float p = (float)((T - ripples[i].T0) / 0.85);
                    float rad = 5 + 30 * p;
                    g.DrawEllipse(ripplePens[Math.Min(2, (int)(p * 3))],
                        ripples[i].X - rad, H - 8 - rad * 0.3f, rad * 2, rad * 0.6f);
                }
                for (int i = 0; i < splashes.Count; i++)
                {
                    float p = (float)((T - splashes[i].T0) / 0.5);
                    Brush b = splashBrush[Math.Min(2, (int)(p * 3))];
                    g.FillRectangle(b, splashes[i].X - 1, splashes[i].Y - 1, 2, 2);
                }
                DrawRainLayer(g, 2, wind);
                double age = T - boltVis;
                if (age >= 0 && age < 0.9)
                {
                    float a = 1f - (float)(age / 0.9);
                    if (bolt.Count > 1)
                    {
                        PointF[] pts = bolt.ToArray();
                        using (Pen glow = new Pen(Color.FromArgb((int)(60 * a), 140, 200, 255), 6f))
                            g.DrawLines(glow, pts);
                        using (Pen core = new Pen(Color.FromArgb((int)(230 * a), 235, 245, 255), 2.2f))
                            g.DrawLines(core, pts);
                        foreach (List<PointF> br in boltBranches)
                        {
                            PointF[] bp = br.ToArray();
                            using (Pen bp1 = new Pen(Color.FromArgb((int)(160 * a), 220, 235, 255), 1.6f))
                                g.DrawLines(bp1, bp);
                        }
                    }
                }
                double fa = T - flashT0;
                if (fa >= 0 && fa < 0.7 && Rain > 0.45f)
                {
                    double p = fa / 0.7;
                    int a = (int)(30 * Math.Sin(Math.PI * p));
                    if (a > 0)
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 185, 215, 255)))
                            g.FillRectangle(b, 0, 0, W, H);
                }
            }
            else if (Theme == 2)
            {
                DrawAurora(g);
                DrawStars(g);
                DrawShoots(g);
                DrawRainLayer(g, 1, wind);
                for (int i = 0; i < fogs.Count; i++)
                {
                    Fog f = fogs[i];
                    Bitmap b = fogBaked[f.Spr][0];
                    int dw = (int)(b.Width * f.Sc), dh = (int)(b.Height * f.Sc);
                    g.DrawImage(b, new Rectangle((int)f.X, (int)f.Y - dh / 2, dw, dh));
                }
            }
            else if (Theme == 4)
            {
                double q0 = Stopwatch.GetTimestamp();
                DrawStars(g);
                double q1 = Stopwatch.GetTimestamp();
                DrawBlackHole(g);
                double q2 = Stopwatch.GetTimestamp();
                DrawShoots(g);
                double q3 = Stopwatch.GetTimestamp();
                double f = 1000.0 / Stopwatch.Frequency;
                ptStars += (q1 - q0) * f; ptHole += (q2 - q1) * f; ptShoots += (q3 - q2) * f;
                if (++ptN >= 90)
                {
                    Program.Log(string.Format("[SCENE] bg={0:F1}ms stars={1:F1}ms hole={2:F1}ms shoots={3:F1}ms (avg of {4})",
                        ptBg / ptN, ptStars / ptN, ptHole / ptN, ptShoots / ptN, ptN));
                    ptN = 0; ptBg = 0; ptStars = 0; ptHole = 0; ptShoots = 0;
                }
            }
            else
            {
                double q0 = Stopwatch.GetTimestamp();
                // vector radial gradients instead of stretched 512px sprites: the
                // sprite DrawImage was ~5M software-blended pixels per frame (~115ms,
                // one core, and it convoyed the kernel GDI lock against the dock);
                // a transformed PathGradientBrush fill renders the identical falloff
                // for a fraction of the cost
                for (int i = 0; i < glows.Count; i++)
                {
                    Glow gl = glows[i];
                    float puls = 0.8f + 0.2f * (float)Math.Sin(gl.Ph);
                    float ds = 512f * gl.R * puls;
                    float ox = (float)Math.Sin(gl.Ph * 0.13) * 60, oy = (float)Math.Cos(gl.Ph * 0.1) * 40;
                    GraphicsState gs = g.Save();
                    g.TranslateTransform(gl.X + ox - ds / 2f, gl.Y + oy - ds / 2f);
                    g.ScaleTransform(ds, ds);
                    g.FillEllipse(glowPgb[gl.Ci], 0f, 0f, 1f, 1f);
                    g.Restore(gs);
                }
                double q1 = Stopwatch.GetTimestamp();
                DrawStars(g);
                double q2 = Stopwatch.GetTimestamp();
                DrawShoots(g);
                double q3 = Stopwatch.GetTimestamp();
                double f = 1000.0 / Stopwatch.Frequency;
                ptHole += (q1 - q0) * f; ptStars += (q2 - q1) * f; ptShoots += (q3 - q2) * f;
                if (++ptN >= 90)
                {
                    Program.Log(string.Format("[SCENE] bg={0:F1}ms glows={1:F1}ms stars={2:F1}ms shoots={3:F1}ms (avg of {4})",
                        ptBg / ptN, ptHole / ptN, ptStars / ptN, ptShoots / ptN, ptN));
                    ptN = 0; ptBg = 0; ptStars = 0; ptHole = 0; ptShoots = 0;
                }
            }
        }

        // ---- theme 4: rotating black hole with accretion disk & orbiting planets ----
        Pen[] trailPens;
        void MakeTrailPens()
        {
            // [temp 3][alpha 6] warm->core colors, round caps
            trailPens = new Pen[18];
            for (int t = 0; t < 3; t++)
                for (int a = 0; a < 6; a++)
                {
                    int alpha = 40 + a * 36;
                    int G = 225 - t * 70 - a * 4;
                    int B = 195 - t * 130 - a * 6;
                    if (G < 40) G = 40;
                    if (B < 20) B = 20;
                    Pen pn = new Pen(Color.FromArgb(alpha, 255, G, B), 1.1f + a * 0.5f);
                    pn.StartCap = LineCap.Round;
                    pn.EndCap = LineCap.Round;
                    trailPens[t * 6 + a] = pn;
                }
        }

        void DrawBlackHole(Graphics g)
        {
            if (trailPens == null) MakeTrailPens();
            float squash = 0.36f;
            float tiltDeg = -14f + 9f * (float)Math.Sin(tiltPhase);
            double tilt = tiltDeg * Math.PI / 180.0;
            float ct = (float)Math.Cos(tilt), st = (float)Math.Sin(tilt);

            Func<float, float, PointF> pos = delegate(float th, float d)
            {
                float lx = d * (float)Math.Cos(th);
                float ly = d * squash * (float)Math.Sin(th);
                return new PointF(bhX + lx * ct - ly * st, bhY + lx * st + ly * ct);
            };

            // darken the backdrop around the hole so it pops
            using (GraphicsPath dp = new GraphicsPath())
            {
                dp.AddEllipse(bhX - bhR * 4.6f, bhY - bhR * 4.6f, bhR * 9.2f, bhR * 9.2f);
                using (PathGradientBrush dg = new PathGradientBrush(dp))
                {
                    dg.CenterColor = Color.FromArgb(150, 0, 0, 0);
                    dg.SurroundColors = new Color[] { Color.FromArgb(0, 0, 0, 0) };
                    g.FillPath(dg, dp);
                }
            }

            // warm outer bloom
            using (GraphicsPath bp = new GraphicsPath())
            {
                bp.AddEllipse(bhX - bhR * 2.6f, bhY - bhR * 2.6f, bhR * 5.2f, bhR * 5.2f);
                using (PathGradientBrush bg = new PathGradientBrush(bp))
                {
                    bg.CenterColor = Color.FromArgb(84, 255, 170, 90);
                    bg.SurroundColors = new Color[] { Color.FromArgb(0, 255, 150, 70) };
                    g.FillPath(bg, bp);
                }
            }

            // continuous disk glow bands
            g.TranslateTransform(bhX, bhY);
            g.RotateTransform(tiltDeg);
            for (int i = 0; i < 6; i++)
            {
                float rr = bhR * (1.22f + i * 0.29f);
                int a = 24 - i * 3;
                using (LinearGradientBrush lb = new LinearGradientBrush(
                    new RectangleF(-rr, -rr * squash, rr * 2, rr * 2 * squash),
                    Color.FromArgb(a, 255, 195, 115), Color.FromArgb((int)(a * 0.35), 255, 120, 60), 0f))
                    g.FillEllipse(lb, -rr, -rr * squash, rr * 2, rr * 2 * squash);
            }
            g.ResetTransform();

            // back half of disk
            DrawDiskHalf(g, pos, true, ct, st);

            // gravitational lensing: hat arcs above/below the shadow
            g.TranslateTransform(bhX, bhY);
            using (Pen hat = new Pen(Color.FromArgb(150, 255, 225, 175), 3.6f))
                g.DrawArc(hat, -bhR * 1.52f, -bhR * 1.62f, bhR * 3.04f, bhR * 3.24f, 195, 150);
            using (Pen hat2 = new Pen(Color.FromArgb(70, 255, 200, 140), 2.2f))
                g.DrawArc(hat2, -bhR * 1.68f, -bhR * 1.78f, bhR * 3.36f, bhR * 3.56f, 200, 140);
            g.RotateTransform(tiltDeg);
            using (Pen vring = new Pen(Color.FromArgb(85, 255, 215, 150), 2.0f))
                g.DrawArc(vring, -bhR * 1.34f, -bhR * 1.34f, bhR * 2.68f, bhR * 2.68f, 0, 360);
            g.ResetTransform();

            // faint polar jets perpendicular to the disk
            double jang = tilt + Math.PI / 2;
            float jx = (float)Math.Cos(jang), jy = (float)Math.Sin(jang);
            for (int dir = 0; dir < 2; dir++)
            {
                float sgn = dir == 0 ? 1 : -1;
                PointF jt0 = new PointF(bhX + jx * bhR * 1.1f * sgn, bhY + jy * bhR * 1.1f * sgn);
                PointF jt1 = new PointF(bhX + jx * bhR * 3.4f * sgn, bhY + jy * bhR * 3.4f * sgn);
                using (Pen jp = new Pen(Color.FromArgb(30, 150, 200, 255), bhR * 0.16f))
                {
                    jp.StartCap = LineCap.Round;
                    jp.EndCap = LineCap.Round;
                    g.DrawLine(jp, jt0, jt1);
                }
            }

            // photon ring + shadow
            using (GraphicsPath hp = new GraphicsPath())
            {
                hp.AddEllipse(bhX - bhR * 1.9f, bhY - bhR * 1.9f, bhR * 3.8f, bhR * 3.8f);
                using (PathGradientBrush hb = new PathGradientBrush(hp))
                {
                    hb.CenterColor = Color.FromArgb(64, 130, 195, 255);
                    hb.SurroundColors = new Color[] { Color.FromArgb(0, 130, 195, 255) };
                    g.FillPath(hb, hp);
                }
            }
            using (SolidBrush blk = new SolidBrush(Color.FromArgb(255, 2, 3, 6)))
                g.FillEllipse(blk, bhX - bhR, bhY - bhR, bhR * 2, bhR * 2);
            using (Pen rim0 = new Pen(Color.FromArgb(235, 255, 248, 232), 2.6f))
                g.DrawEllipse(rim0, bhX - bhR * 1.07f, bhY - bhR * 1.07f, bhR * 2.14f, bhR * 2.14f);
            using (Pen rim1 = new Pen(Color.FromArgb(165, 255, 215, 140), 2.2f))
                g.DrawEllipse(rim1, bhX - bhR * 1.18f, bhY - bhR * 1.18f, bhR * 2.36f, bhR * 2.36f);
            using (Pen rim2 = new Pen(Color.FromArgb(80, 255, 170, 90), 1.8f))
                g.DrawEllipse(rim2, bhX - bhR * 1.30f, bhY - bhR * 1.30f, bhR * 2.60f, bhR * 2.60f);

            // front half of disk
            DrawDiskHalf(g, pos, false, ct, st);

            // orbiting planets
            for (int i = 0; i < planets.Count; i++)
            {
                Planet p = planets[i];
                float lx = p.A * (float)Math.Cos(p.Th);
                float ly = p.B * (float)Math.Sin(p.Th);
                float px = bhX + lx * ct - ly * st;
                float py = bhY + lx * st + ly * ct;
                g.TranslateTransform(bhX, bhY);
                g.RotateTransform(tiltDeg);
                using (Pen op = new Pen(Color.FromArgb(py > bhY ? 64 : 26, 115, 175, 235), 0.9f))
                    g.DrawEllipse(op, -p.A, -p.B, p.A * 2, p.B * 2);
                g.ResetTransform();
                if (py <= bhY) DrawPlanet(g, px, py, p.Sz, p.C);
            }
            for (int i = 0; i < planets.Count; i++)
            {
                Planet p = planets[i];
                float lx = p.A * (float)Math.Cos(p.Th);
                float ly = p.B * (float)Math.Sin(p.Th);
                float px = bhX + lx * ct - ly * st;
                float py = bhY + lx * st + ly * ct;
                if (py > bhY) DrawPlanet(g, px, py, p.Sz, p.C);
            }
        }

        void DrawDiskHalf(Graphics g, Func<float, float, PointF> pos, bool back, float ct, float st)
        {
            for (int i = 0; i < disk.Count; i++)
            {
                BHP p = disk[i];
                PointF pt = pos(p.Th, p.D);
                bool isBack = pt.Y < bhY;
                if (isBack != back) continue;
                float dAng = 0.055f + 0.075f * (1.2f * bhR / p.D);
                PointF pt2 = pos(p.Th + dAng, p.D);
                float t = (p.D - bhR * 1.16f) / (bhR * 1.55f);
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                float doppler = 1f + 0.62f * (float)Math.Cos(p.Th);
                if (doppler < 0.42f) doppler = 0.42f;
                float flick = 0.82f + 0.28f * (float)Math.Sin(T * p.Fl + p.Ph);
                float alphaF = (215 - 150 * t) * (back ? 0.60f : 1f) * doppler * flick;
                int aIdx = (int)(alphaF / 43f);
                if (aIdx < 0) aIdx = 0;
                if (aIdx > 5) aIdx = 5;
                int tIdx = (int)(t * 3f);
                if (tIdx > 2) tIdx = 2;
                g.DrawLine(trailPens[tIdx * 6 + aIdx], pt, pt2);
            }
        }

        void DrawPlanet(Graphics g, float x, float y, float r, int c)
        {
            using (GraphicsPath pp = new GraphicsPath())
            {
                pp.AddEllipse(x - r, y - r, r * 2, r * 2);
                using (PathGradientBrush pb = new PathGradientBrush(pp))
                {
                    float ang = (float)Math.Atan2(bhY - y, bhX - x);
                    pb.CenterPoint = new PointF(x + (float)Math.Cos(ang) * r * 0.7f, y + (float)Math.Sin(ang) * r * 0.7f);
                    if (c == 0) { pb.CenterColor = Color.FromArgb(255, 150, 190, 235); pb.SurroundColors = new Color[] { Color.FromArgb(255, 20, 34, 62) }; }
                    else { pb.CenterColor = Color.FromArgb(255, 235, 195, 160); pb.SurroundColors = new Color[] { Color.FromArgb(255, 48, 30, 26) }; }
                    g.FillPath(pb, pp);
                }
            }
        }

        void DrawRainLayer(Graphics g, int l, float wind)
        {
            List<Drop> list = rain[l];
            int active = (int)(list.Count * Math.Min(1f, 0.1f + Rain));
            for (int i = 0; i < active; i++)
            {
                Drop d = list[i];
                if (d.Y < -9000 || d.Y > H) continue;
                if (snowMix < 0.5f)
                {
                    Pen pn = rainPens[l][Math.Max(0, Math.Min(3, (int)((d.Alpha * d.Jit - 40) / 34)))];
                    float len = d.Len * d.Jit;
                    g.DrawLine(pn, d.X, d.Y, d.X - wind * len * 0.16f, d.Y - len);
                }
                if (snowMix > 0.02f)
                    g.FillEllipse(snowBrush, d.X - 1.5f, d.Y - 1.5f, 3f, 3f);
            }
        }

        void DrawStars(Graphics g)
        {
            for (int i = 0; i < stars.Count; i++)
            {
                Star s = stars[i];
                float tw = s.Base * (0.55f + 0.45f * (float)Math.Sin(T * s.Spd + s.Phase));
                int idx = Math.Max(0, Math.Min(11, (int)(tw * 11)));
                g.FillRectangle(starBrushes[idx], s.X, s.Y, 1 + s.Sz, 1 + s.Sz);
            }
        }

        void DrawShoots(Graphics g)
        {
            for (int i = 0; i < shoots.Count; i++)
            {
                Shoot s = shoots[i];
                float age = (float)(T - s.T0);
                float a = Math.Max(0, 1 - age / 0.9f);
                float tail = 0.09f;
                for (int k = 0; k < 5; k++)
                {
                    float f1 = k / 5f, f2 = (k + 1) / 5f;
                    PointF p1 = new PointF(s.X + s.VX * age - s.VX * tail * f1, s.Y + s.VY * age - s.VY * tail * f1);
                    PointF p2 = new PointF(s.X + s.VX * age - s.VX * tail * f2, s.Y + s.VY * age - s.VY * tail * f2);
                    using (Pen pn = new Pen(Color.FromArgb((int)(190 * a * (1 - f1)), 220, 240, 255), 1.6f))
                        g.DrawLine(pn, p1, p2);
                }
            }
        }

        public void SaveFrame(string path)
        {
            using (Graphics g = Graphics.FromImage(frame)) Draw(g);
            frame.Save(path, ImageFormat.Png);
        }
    }

    // ============ wallpaper window + desktop widgets host ============
    class WallpaperForm : Form
    {
        Scene scene;
        Timer attachTimer = new Timer();
        Timer fsTimer = new Timer();
        // the scene (hundreds of GDI+ particles over the full screen, 10-20ms per
        // frame) used to Update+Draw inside OnPaint on the UI thread - the same
        // thread the dock's spring animation lives on, so every wallpaper frame
        // stalled the dock and its springs stuttered. A dedicated render thread
        // draws into a DIB section (top-down 32bpp) and presents with BitBlt -
        // a 1080p GDI+ DrawImage to the window DC cost ~37ms and convoyed the
        // kernel GDI lock against the dock; the BitBlt is ~5ms.
        IntPtr dibDC = IntPtr.Zero, dibBmp = IntPtr.Zero, dibOld = IntPtr.Zero;
        Graphics dibG;
        Thread renderThread;
        volatile bool renderRun;
        bool staticPublished;
        int wallN;
        double wallDrawSum, wallPresentSum, wallPresentMax, wallUpdSum;
        int wallCycle = 50;   // adaptive wallpaper frame cycle (weak machines stretch it)
        Stopwatch sw = Stopwatch.StartNew();
        IntPtr parentHwnd = IntPtr.Zero;
        bool fullscreenPause = false;
        DateTime lastHostSwitch = DateTime.MinValue;
        DateTime healCooldown = DateTime.MinValue;
        int healFails = 0;
        Bitmap lastGrab;   // previous downscaled screen sample for the motion check
        int paintCount = 0;

        public WallpaperForm()
        {
            AppShell.WallRef = this;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Rectangle b = Screen.PrimaryScreen.Bounds;
            Bounds = b;
            DoubleBuffered = true;
            scene = new Scene(b.Width, b.Height, Program.ReadTheme());
            AppShell.SceneRef = scene;

            IntPtr sdc = Program.GetDC(IntPtr.Zero);
            Program.BITMAPINFO bi = new Program.BITMAPINFO();
            bi.biWidth = b.Width;
            bi.biHeight = -b.Height;   // top-down
            IntPtr bits;
            dibBmp = Program.CreateDIBSection(sdc, bi, 0, out bits, IntPtr.Zero, 0);
            dibDC = Program.CreateCompatibleDC(sdc);
            Program.ReleaseDC(IntPtr.Zero, sdc);
            if (dibDC != IntPtr.Zero && dibBmp != IntPtr.Zero)
            {
                dibOld = Program.SelectObject(dibDC, dibBmp);
                dibG = Graphics.FromHdc(dibDC);
                dibG.SmoothingMode = SmoothingMode.AntiAlias;
            }
            renderRun = true;
            renderThread = new Thread(RenderLoop);
            renderThread.IsBackground = true;
            renderThread.Start();

            attachTimer.Interval = 4000;
            attachTimer.Tick += delegate
            {
                if (parentHwnd == IntPtr.Zero) TryAttach();
                else if (!Program.IsWindow(parentHwnd))
                {
                    parentHwnd = IntPtr.Zero;
                    Hide();
                }
            };
            attachTimer.Start();

            fsTimer.Interval = 250;   // fast: pause the wallpaper the moment a window covers the screen
            fsTimer.Tick += delegate
            {
                IntPtr fg;
                bool fs = Program.ForegroundIsFullscreen(Handle, out fg);
                if (fs != fullscreenPause)
                    Program.Log(string.Format("[WALL] fullscreenPause {0} -> {1} (fg {2})", fullscreenPause, fs, fg));
                fullscreenPause = fs;
                // self-heal: we paint 30fps but the screen never changes -> our host
                // WorkerW lost the DWM composition race (EP stacks two and their
                // order flips) - switch to another host. Hysteresis: at most one
                // switch per 30s. Stands down entirely while one of our own
                // full-screen panels is in the foreground: the sampler would only
                // read the panel's static pixels and the churn would kill it.
                if (!AppShell.Paused && !fullscreenPause && parentHwnd != IntPtr.Zero
                    && Program.LogCounter % 5 == 0
                    && !Program.ForegroundBelongsToUs())
                {
                    bool changing = ScreenIsChanging();
                    if (changing) healFails = 0;
                    else if (DateTime.Now > healCooldown && (DateTime.Now - lastHostSwitch).TotalSeconds > 30)
                    {
                        List<IntPtr> hosts = Program.WallpaperHosts();
                        bool switched = false;
                        foreach (IntPtr hst in hosts)
                        {
                            if (hst == parentHwnd) continue;
                            Program.Log("[WALL] screen static while animating -> switching host to " + hst);
                            AttachTo(hst);
                            switched = true;
                            break;
                        }
                        if (!switched)
                        {
                            // every host exhausted and still static: the attachment itself
                            // went stale (observed after wallpaper SPI changes) - detach
                            // fully and let attachTimer build it again from scratch,
                            // which is exactly what a process restart does
                            Program.Log("[WALL] all hosts static -> hard re-attach");
                            try { Program.SetParent(Handle, IntPtr.Zero); } catch { }
                            Hide();
                            parentHwnd = IntPtr.Zero;
                        }
                        lastHostSwitch = DateTime.Now;
                        healFails++;
                        if (healFails >= 3)
                        {
                            // the heal is not converging (e.g. a DWM composition
                            // stall that survives window recreation): stop hiding
                            // the wallpaper every 30s and just wait it out
                            Program.Log("[WALL] heal not converging -> cooldown 10 min");
                            healCooldown = DateTime.Now.AddMinutes(10);
                            healFails = 0;
                        }
                    }
                }
                if (Program.LogCounter++ % 30 == 0)
                    Program.Log(string.Format("[WALL] heartbeat paused={0} fs={1} parent={2} vis={3} paints={4}",
                        AppShell.Paused, fullscreenPause, parentHwnd, Visible, paintCount));
            };
            fsTimer.Start();

            AppShell.ThemeChanged += OnThemeChanged;
            AppShell.UIChanged += OnUIChanged;
        }

        void OnThemeChanged(int t)
        {
            // build heavy scene on a worker thread; never block or crash the UI thread
            Rectangle b = Screen.PrimaryScreen.Bounds;
            int themeNew = t;
            Thread th = new Thread(delegate()
            {
                Scene ns = null;
                string tmp = null;
                try
                {
                    ns = new Scene(b.Width, b.Height, themeNew);
                    ns.Simulate(3.0);
                    tmp = Path.Combine(Program.BaseDir, "static_" + themeNew + ".png");
                    ns.SaveFrame(tmp);
                }
                catch { }
                if (ns == null) return;
                try { Program.SetStaticWallpaper(tmp); } catch { }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        lock (AppShell.SceneLock) { scene = ns; }
                        AppShell.SceneRef = ns;
                        Invalidate();
                    });
                }
                catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        void OnUIChanged() { Invalidate(); }

        public void AttachTo(IntPtr wall)
        {
            // styles FIRST, while the window is still ours: once SetParent puts it
            // under another process's window, USER32 silently blocks style writes
            // (SetWindowLongPtrW returns 0 with no error) - click-through and the
            // layered alpha then never apply and the desktop eats every click
            try
            {
                // ONLY WS_EX_TRANSPARENT (click-through) here - deliberately NOT
                // WS_EX_LAYERED: a SetLayeredWindowAttributes-layered child of
                // Progman does not get composited by DWM (paints run, screen
                // never changes) and the wallpaper appears frozen
                IntPtr ex = Program.GetWindowLongPtr(Handle, -20);
                IntPtr setRes = Program.SetWindowLongPtr(Handle, -20, (IntPtr)((long)ex | 0x20));
                IntPtr now = Program.GetWindowLongPtr(Handle, -20);
                Program.Log(string.Format("[WALL] attach styles(pre-parent): wall={0} exBefore={1} setRes={2} exNow={3}",
                    wall, ex, setRes, now));
            }
            catch (Exception ex) { Program.Log("[WALL] attach styles FAILED: " + ex.Message); }
            Program.SetParent(Handle, wall);
            Program.MoveWindow(Handle, 0, 0, Width, Height, true);
            Show();
            ApplyLayerOrder();
            parentHwnd = wall;
        }

        // z-order between the live wallpaper and explorer's icon layer. The EP
        // desktop keeps DefView directly under Progman with an OPAQUE background
        // (it paints the static SPI wallpaper image), so exactly one of the two
        // can be visible: animation above icons, or icons above a frozen frame.
        // The wallpaper has WS_EX_TRANSPARENT, so icon clicks keep working even
        // when the animation is on top.
        public void ApplyLayerOrder()
        {
            if (parentHwnd == IntPtr.Zero || !Program.IsWindow(Handle)) return;
            if (AppShell.IconsOverWallpaper)
            {
                IntPtr def = Program.FindWindowEx(parentHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (def != IntPtr.Zero)
                    Program.SetWindowPos(def, (IntPtr)0, 0, 0, 0, 0, 0x0001 | 0x0002);
            }
            else
                Program.SetWindowPos(Handle, (IntPtr)0, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }

        void TryAttach()
        {
            IntPtr wall = Program.FindWallpaper();
            if (wall != IntPtr.Zero) AttachTo(wall);
        }

        // is anything moving in the WALLPAPER's own band of the screen? Diffs a
        // downscaled grab against the previous fs-tick sample (~1.25s apart, no
        // sleeping). Widget/dock regions are excluded - the volume widget pulses
        // with any audio and would read "changing" forever, blinding the heal.
        // (The old 32x32 center probe false-flagged slow themes whose dead zone
        // sits dead-center and made the heal kick a healthy attachment forever.)
        bool ScreenIsChanging()
        {
            try
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                Rectangle r = Rectangle.FromLTRB(200, 100, Math.Min(b.Width - 150, 1400), Math.Min(b.Height - 230, 850));
                bool changing;
                using (Bitmap full = new Bitmap(r.Width, r.Height))
                {
                    using (Graphics g = Graphics.FromImage(full))
                        g.CopyFromScreen(r.X, r.Y, 0, 0, full.Size);
                    using (Bitmap s = new Bitmap(full, 48, 30))
                    {
                        if (lastGrab == null) changing = true;
                        else
                        {
                            double sum = 0; int maxd = 0;
                            for (int y = 0; y < 30; y++)
                                for (int x = 0; x < 48; x++)
                                {
                                    Color c1 = lastGrab.GetPixel(x, y), c2 = s.GetPixel(x, y);
                                    int dd = Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
                                    sum += dd;
                                    if (dd > maxd) maxd = dd;
                                }
                            changing = sum / (48 * 30) > 0.15 || maxd > 60;
                        }
                        lastGrab.Dispose();
                        lastGrab = (Bitmap)s.Clone();
                    }
                }
                return changing;
            }
            catch { return true; }
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000;
                return cp;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; }
            base.WndProc(ref m);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        void RenderLoop()
        {
            var rsw = Stopwatch.StartNew();
            double last = 0;
            while (renderRun)
            {
                double now = rsw.Elapsed.TotalMilliseconds;
                float dt = (float)((now - last) / 1000.0);
                last = now;
                if (dt <= 0 || dt > 0.25f) dt = 0.033f;
                Scene s;
                lock (AppShell.SceneLock) { s = scene; }
                bool debugWallPause = File.Exists(Path.Combine(Program.BaseDir, "wallpause.txt"));
                // paused (user or fullscreen/maximized foreground): stop painting
                // entirely - the wallpaper keeps its last frame at zero CPU cost
                if (s != null && !AppShell.Paused && !fullscreenPause && !debugWallPause && dibG != null)
                {
                    // FIRST-RUN guarantee: after a couple of live frames, publish the
                    // current scene as the system static wallpaper. A fresh machine
                    // never had static_<theme>.png, and if the WorkerW attach loses
                    // the DWM race there, the user still sees the theme wallpaper
                    // instead of their old one. One-shot per launch, missing-file only.
                    if (!staticPublished && rsw.Elapsed.TotalMilliseconds > 2500)
                    {
                        staticPublished = true;
                        PublishStatic(s);
                    }
                    double t0 = rsw.Elapsed.TotalMilliseconds;
                    double updMs = 0;
                    try
                    {
                        lock (AppShell.SceneLock) s.Update(dt);
                        updMs = rsw.Elapsed.TotalMilliseconds - t0;
                        lock (AppShell.SceneLock) s.Draw(dibG);
                    }
                    catch { }
                    double t1 = rsw.Elapsed.TotalMilliseconds;
                    PresentBitBlt();
                    double t2 = rsw.Elapsed.TotalMilliseconds;
                    wallN++; wallDrawSum += t1 - t0; wallPresentSum += t2 - t1; wallUpdSum += updMs;
                    if (t2 - t1 > wallPresentMax) wallPresentMax = t2 - t1;
                    if (wallN >= 90)
                    {
                        double wallFrame = (wallDrawSum + wallPresentSum) / wallN;
                        // weak-machine adaptation: stretch the wallpaper cycle so its
                        // GDI duty cycle stays ~<=50% no matter how slow the renderer
                        if (wallFrame > 30) wallCycle = 80;       // ~12fps
                        else if (wallFrame > 20) wallCycle = 65;  // ~15fps
                        else wallCycle = 50;                      // 20fps
                        Program.Log(string.Format("[WALLR] n={0} updAvg={1:F1}ms drawOnly={2:F1}ms presentAvg={3:F1}ms presentMax={4:F1}ms cycle={5}ms",
                            wallN, wallUpdSum / wallN, (wallDrawSum - wallUpdSum) / wallN, wallPresentSum / wallN, wallPresentMax, wallCycle));
                        wallN = 0; wallDrawSum = 0; wallUpdSum = 0; wallPresentSum = 0; wallPresentMax = 0;
                    }
                }
                // ADAPT for the target machine: if a full frame (draw+present) is
                // expensive, stretch the cycle so the GDI lock gets air for the dock
                int wait = (int)(wallCycle - (rsw.Elapsed.TotalMilliseconds - now));   // ~20fps cap: leaves GDI headroom for the dock
                Thread.Sleep(wait < 1 ? 1 : wait);
            }
        }

        // draw the live scene once into a fresh bitmap and set it as the system
        // static wallpaper - the first-run safety net (called ~2.5s after start,
        // once, only when static_<theme>.png doesn't exist yet)
        void PublishStatic(Scene s)
        {
            try
            {
                string tmp = Path.Combine(Program.BaseDir, "static_" + s.Theme + ".png");
                if (File.Exists(tmp)) return;
                using (Bitmap snap = new Bitmap(s.W, s.H, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics sg = Graphics.FromImage(snap))
                    {
                        lock (AppShell.SceneLock) s.Draw(sg);
                    }
                    snap.Save(tmp, ImageFormat.Png);
                }
                Program.SetStaticWallpaper(tmp);
                Program.Log("[WALL] first-run static published: " + tmp);
            }
            catch (Exception ex) { Program.Log("[WALL] first-run static failed: " + ex.Message); }
        }

        // BitBlt the finished DIB frame onto the window. A GDI+ DrawImage to the
        // window DC cost ~37ms of GDI-lock hold per frame; the blit is ~5ms.
        // Concurrent UI-thread painting of this window is only OnPaint (expose
        // repair), so worst case is one torn frame, never a crash.
        void PresentBitBlt()
        {
            try
            {
                if (!IsHandleCreated || dibDC == IntPtr.Zero) return;
                IntPtr dc = Program.GetDC(Handle);
                if (dc == IntPtr.Zero) return;
                try { Program.BitBlt(dc, 0, 0, Width, Height, dibDC, 0, 0, 0x00CC0020); }
                finally { Program.ReleaseDC(Handle, dc); }
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            paintCount++;
            // expose repair: blit the current DIB frame (worker may be mid-draw;
            // worst case is a torn frame, never a crash)
            try
            {
                if (dibDC != IntPtr.Zero)
                {
                    IntPtr dc = e.Graphics.GetHdc();
                    try { Program.BitBlt(dc, 0, 0, Width, Height, dibDC, 0, 0, 0x00CC0020); }
                    finally { e.Graphics.ReleaseHdc(dc); }
                }
            }
            catch { }
        }

        public void Cleanup()
        {
            renderRun = false;
            attachTimer.Stop(); fsTimer.Stop();
            try { if (renderThread != null) renderThread.Join(600); } catch { }
            try
            {
                if (dibG != null) { dibG.Dispose(); dibG = null; }
                if (dibDC != IntPtr.Zero && dibOld != IntPtr.Zero) Program.SelectObject(dibDC, dibOld);
                if (dibBmp != IntPtr.Zero) Program.DeleteObject(dibBmp);
                if (dibDC != IntPtr.Zero) Program.DeleteDC(dibDC);
                dibDC = IntPtr.Zero; dibBmp = IntPtr.Zero; dibOld = IntPtr.Zero;
            }
            catch { }
            AppShell.ThemeChanged -= OnThemeChanged;
            AppShell.UIChanged -= OnUIChanged;
            if (AppShell.WallRef == this) AppShell.WallRef = null;
            try { if (lastGrab != null) { lastGrab.Dispose(); lastGrab = null; } } catch { }
        }
    }

    // ============ shared widget drawing helpers ============
    static class WidgetPaint
    {
        public static readonly Color Key = Color.FromArgb(255, 255, 0, 255);
        public static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddArc(x, y, r * 2, r * 2, 180, 90);
            p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
        // widgets float directly on the wallpaper now (the dark rounded panel read
        // as a plaster patch), so all colors come from a per-theme palette and
        // every glyph gets a soft dark halo to survive bright spots (snow, disk glow)
        public struct Palette
        {
            public Color Primary, Secondary, Muted, Accent, Line, Outline;
        }

        public static Palette PaletteFor(int theme)
        {
            Palette p = new Palette();
            switch (theme)
            {
                case 2:  // aurora snow night -> mint accent
                    p.Accent = Color.FromArgb(255, 122, 255, 196);
                    p.Primary = Color.FromArgb(242, 236, 255, 246);
                    p.Secondary = Color.FromArgb(250, 230, 246, 238);
                    p.Muted = Color.FromArgb(246, 222, 240, 226);
                    p.Line = Color.FromArgb(48, 132, 255, 205);
                    p.Outline = Color.FromArgb(200, 5, 14, 12);
                    break;
                case 3:  // deep nebula -> violet accent
                    p.Accent = Color.FromArgb(255, 188, 152, 255);
                    p.Primary = Color.FromArgb(246, 242, 248, 255);
                    p.Secondary = Color.FromArgb(250, 232, 228, 250);
                    p.Muted = Color.FromArgb(246, 226, 222, 240);
                    p.Line = Color.FromArgb(46, 188, 158, 255);
                    p.Outline = Color.FromArgb(195, 6, 8, 20);
                    break;
                case 4:  // black hole -> warm amber accent
                    p.Accent = Color.FromArgb(255, 255, 182, 112);
                    p.Primary = Color.FromArgb(248, 247, 242, 236);
                    p.Secondary = Color.FromArgb(250, 250, 240, 228);
                    p.Muted = Color.FromArgb(246, 240, 224, 208);
                    p.Line = Color.FromArgb(54, 255, 192, 128);
                    p.Outline = Color.FromArgb(210, 12, 7, 3);
                    break;
                default:  // 1 rainy neon city -> neon cyan
                    p.Accent = Color.FromArgb(255, 105, 214, 255);
                    p.Primary = Color.FromArgb(246, 240, 247, 255);
                    p.Secondary = Color.FromArgb(250, 226, 236, 250);
                    p.Muted = Color.FromArgb(246, 216, 230, 244);
                    p.Line = Color.FromArgb(42, 96, 215, 255);
                    p.Outline = Color.FromArgb(195, 4, 8, 18);
                    break;
            }
            return p;
        }

        // subtitle-style closed outline: a dark ring (8 dirs at full radius + 4 at
        // half radius to close the gaps) under every glyph, so thin small text keeps
        // its contour on any wallpaper - offset drop shadows alone wash out
        public static void Text(Graphics g, string s, Font f, Brush b, float x, float y, Palette pal)
        {
            float r = Math.Min(2.6f, Math.Max(1.5f, f.Size / 14f));
            using (SolidBrush so = new SolidBrush(pal.Outline))
            {
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4.0;
                    g.DrawString(s, f, so, x + (float)Math.Cos(a) * r, y + (float)Math.Sin(a) * r);
                }
                for (int i = 0; i < 4; i++)
                {
                    double a = i * Math.PI / 2.0 + Math.PI / 4.0;
                    g.DrawString(s, f, so, x + (float)Math.Cos(a) * r * 0.5f, y + (float)Math.Sin(a) * r * 0.5f);
                }
            }
            g.DrawString(s, f, b, x, y);
        }

        public static void Text(Graphics g, string s, Font f, Brush b, RectangleF layout, StringFormat sf, Palette pal)
        {
            float r = Math.Min(2.6f, Math.Max(1.5f, f.Size / 14f));
            using (SolidBrush so = new SolidBrush(pal.Outline))
            {
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4.0;
                    RectangleF lo = new RectangleF(layout.X + (float)Math.Cos(a) * r, layout.Y + (float)Math.Sin(a) * r, layout.Width, layout.Height);
                    g.DrawString(s, f, so, lo, sf);
                }
                for (int i = 0; i < 4; i++)
                {
                    double a = i * Math.PI / 2.0 + Math.PI / 4.0;
                    RectangleF ln = new RectangleF(layout.X + (float)Math.Cos(a) * r * 0.5f, layout.Y + (float)Math.Sin(a) * r * 0.5f, layout.Width, layout.Height);
                    g.DrawString(s, f, so, ln, sf);
                }
            }
            g.DrawString(s, f, b, layout, sf);
        }
    }

    // ============ per-pixel alpha window presenter (UpdateLayeredWindow) ============
    static class LayeredPainter
    {
        static Dictionary<long, byte[]> pxbufs = new Dictionary<long, byte[]>();

        public static void Present(Form f, Bitmap bmp) { Present(f, bmp, 255); }

        // alpha = whole-window opacity knob (SourceConstantAlpha) - the dock
        // fades its summons/hides through this instead of popping in/out
        public static void Present(Form f, Bitmap bmp, int alpha)
        {
            try
            {
                Premultiply(f.Handle.ToInt64(), bmp);
                IntPtr screenDc = Program.GetDC(IntPtr.Zero);
                IntPtr memDc = Program.CreateCompatibleDC(screenDc);
                IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
                try
                {
                    hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                    old = Program.SelectObject(memDc, hBmp);
                    Point src = new Point(0, 0);
                    Point dst = new Point(f.Left, f.Top);
                    Size sz = new Size(f.Width, f.Height);
                    Program.BLENDFUNCTION bf = new Program.BLENDFUNCTION();
                    bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = (byte)alpha; bf.AlphaFormat = 1;
                    Program.UpdateLayeredWindow(f.Handle, screenDc, ref dst, ref sz, memDc, ref src, 0, ref bf, 2);
                }
                finally
                {
                    if (old != IntPtr.Zero) Program.SelectObject(memDc, old);
                    if (hBmp != IntPtr.Zero) Program.DeleteObject(hBmp);
                    Program.DeleteDC(memDc);
                    Program.ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
            catch { }
        }

        static void Premultiply(long key, Bitmap b)
        {
            try
            {
                Rectangle r = new Rectangle(0, 0, b.Width, b.Height);
                BitmapData d = b.LockBits(r, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int bytes = d.Stride * d.Height;
                byte[] px;
                if (!pxbufs.TryGetValue(key, out px) || px.Length != bytes)
                {
                    px = new byte[bytes];
                    pxbufs[key] = px;
                }
                Marshal.Copy(d.Scan0, px, 0, bytes);
                for (int i = 0; i < bytes; i += 4)
                {
                    int a = px[i + 3];
                    if (a == 255 || a == 0) continue;
                    px[i] = (byte)(px[i] * a / 255);
                    px[i + 1] = (byte)(px[i + 1] * a / 255);
                    px[i + 2] = (byte)(px[i + 2] * a / 255);
                }
                Marshal.Copy(px, 0, d.Scan0, bytes);
                b.UnlockBits(d);
            }
            catch { }
        }
    }

    // ============ weather + clock widget (per-pixel alpha, click-through) ============
    class WeatherWidgetForm : Form
    {
        Timer t = new Timer();
        Timer watch = new Timer();
        Bitmap buf;
        public WeatherWidgetForm(int x, int y)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(x, y);
            ClientSize = new Size(360, 244);
            buf = new Bitmap(360, 244, PixelFormat.Format32bppArgb);
            t.Interval = 1000;
            t.Tick += delegate { Render(); };
            t.Start();
            watch.Interval = 2500;
            watch.Tick += delegate
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                if (!Visible) Show();
                Render();
            };
            watch.Start();
            IntPtr hh = Handle;   // force handle, then paint surface immediately
            Render();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000 | 0x20 | 0x80000;  // TOOLWINDOW|NOACTIVATE|TRANSPARENT|LAYERED
                return cp;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0112 && (m.WParam.ToInt64() & 0xFFF0) == 0xF020) return;  // never minimize
            base.WndProc(ref m);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        void Render()
        {
            using (Graphics g = Graphics.FromImage(buf))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                WidgetPaint.Palette pal = WidgetPaint.PaletteFor(AppShell.CurrentTheme);
                string zh = "Microsoft YaHei UI";
                DateTime now = DateTime.Now;
                string[] wd = { "日", "一", "二", "三", "四", "五", "六" };
                using (Brush gray = new SolidBrush(pal.Muted))
                using (Brush mid = new SolidBrush(pal.Secondary))
                using (Brush white = new SolidBrush(pal.Primary))
                using (Brush cyan = new SolidBrush(pal.Accent))
                using (Pen line = new Pen(pal.Line, 1f))
                {
                    // location pin glyph
                    g.FillEllipse(cyan, 22, 21, 8, 8);
                    using (SolidBrush pinCore = new SolidBrush(Color.FromArgb(255, 15, 25, 45)))
                        g.FillEllipse(pinCore, 24.5f, 23.5f, 3, 3);
                    g.FillPolygon(cyan, new PointF[] { new PointF(23, 28), new PointF(29, 28), new PointF(26, 34) });
                    // clock measured first: city is laid out in the space left of it
                    float clockW = 0;
                    string hm = now.ToString("HH:mm");
                    using (Font f = new Font("Segoe UI", 22, FontStyle.Bold)) clockW = g.MeasureString(hm, f).Width;
                    using (Font f = new Font("Segoe UI", 22, FontStyle.Bold))
                        WidgetPaint.Text(g, hm, f, white, 338 - clockW, 14, pal);
                    using (Font f = new Font(zh, 10.5f))
                    {
                        string ds = now.ToString("M月d日") + " 周" + wd[(int)now.DayOfWeek];
                        SizeF sz = g.MeasureString(ds, f);
                        WidgetPaint.Text(g, ds, f, gray, 338 - sz.Width, 48, pal);
                    }
                    // city + region, clipped to the space left of the clock
                    string place = WeatherState.City;
                    if (WeatherState.Region.Length > 0) place += " · " + WeatherState.Region;
                    float maxCityW = (338 - clockW - 14) - 40;
                    using (Font f = new Font(zh, 13f, FontStyle.Bold))
                    {
                        SizeF pw = g.MeasureString(place, f);
                        if (pw.Width > maxCityW && WeatherState.Region.Length > 0)
                        {
                            place = WeatherState.City;
                            pw = g.MeasureString(place, f);
                        }
                        RectangleF cityRect = new RectangleF(40, 15, Math.Max(40, maxCityW), 26);
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Trimming = StringTrimming.EllipsisCharacter;
                            sf.FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.NoWrap;
                            WidgetPaint.Text(g, place, f, white, cityRect, sf, pal);
                        }
                    }
                    // big temperature
                    using (Font f = new Font("Segoe UI", 46, FontStyle.Bold))
                        WidgetPaint.Text(g, WeatherState.TempC + "°", f, cyan, 20, 52, pal);
                    // condition + wind + humidity
                    using (Font f = new Font(zh, 15.5f, FontStyle.Bold)) WidgetPaint.Text(g, WeatherState.Desc, f, white, 158, 66, pal);
                    using (Font f = new Font(zh, 11.5f))
                        WidgetPaint.Text(g, "风 " + WeatherState.Wind + "    湿度 " + WeatherState.Humidity, f, mid, 158, 98, pal);
                    // divider
                    g.DrawLine(line, 20, 146, 340, 146);
                    // 3-day forecast columns
                    for (int i = 0; i < 3; i++)
                    {
                        int cx = 22 + i * 112;
                        if (i > 0) g.DrawLine(line, cx - 14, 158, cx - 14, 222);
                        using (Font f = new Font(zh, 11.5f)) WidgetPaint.Text(g, WeatherState.DayWeek[i], f, gray, cx, 156, pal);
                        using (Font f = new Font("Segoe UI", 11.5f, FontStyle.Bold)) WidgetPaint.Text(g, WeatherState.DayTemp[i], f, white, cx, 180, pal);
                        // clip to the column so a long desc can't bleed into the next one
                        using (Font f = new Font(zh, 10.5f))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Trimming = StringTrimming.EllipsisCharacter;
                            sf.FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.NoWrap;
                            WidgetPaint.Text(g, WeatherState.DayDesc[i], f, mid, new RectangleF(cx, 204, 100, 20), sf, pal);
                        }
                    }
                    if (WeatherState.Updated != DateTime.MinValue)
                    {
                        using (Font f = new Font(zh, 9f))
                        {
                            string upd = "更新 " + WeatherState.Updated.ToString("HH:mm");
                            SizeF sz = g.MeasureString(upd, f);
                            WidgetPaint.Text(g, upd, f, gray, 340 - sz.Width, 228, pal);
                        }
                    }
                }
            }
            LayeredPainter.Present(this, buf);
        }
    }

    // ============ audio VU bars widget (per-pixel alpha, click-through) ============
    class VolumeWidgetForm : Form
    {
        Timer t = new Timer();
        Timer watch = new Timer();
        const int BARS = 22;
        float[] vals = new float[BARS];
        float[] ph = new float[BARS];
        float[] spd = new float[BARS];
        object meter;
        double lastMeterTry;
        double T;
        Bitmap buf;

        public VolumeWidgetForm(int x, int y)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(x, y);
            ClientSize = new Size(280, 88);
            buf = new Bitmap(280, 88, PixelFormat.Format32bppArgb);
            Random r = new Random(3);
            for (int i = 0; i < BARS; i++)
            {
                ph[i] = (float)(r.NextDouble() * Math.PI * 2);
                spd[i] = 1.2f + (float)r.NextDouble() * 3.2f;
            }
            t.Interval = 33;
            t.Tick += delegate { Render(); };
            t.Start();
            watch.Interval = 2500;
            watch.Tick += delegate
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                if (!Visible) Show();
            };
            watch.Start();
            IntPtr hh = Handle;
            Render();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000 | 0x20 | 0x80000;
                return cp;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0112 && (m.WParam.ToInt64() & 0xFFF0) == 0xF020) return;  // never minimize
            base.WndProc(ref m);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        float GetPeak()
        {
            try
            {
                if (meter == null)
                {
                    if (T - lastMeterTry < 5) return 0f;
                    lastMeterTry = T;
                    meter = CreateMeter();
                }
                IAudioMeterInformation m = (IAudioMeterInformation)meter;
                // GetPeakValue is a CROSS-PROCESS COM call into the audio service -
                // when it lags it stalls the whole UI thread (dock springs included)
                long q0 = Stopwatch.GetTimestamp();
                float p; m.GetPeakValue(out p);
                double ms = (Stopwatch.GetTimestamp() - q0) * 1000.0 / Stopwatch.Frequency;
                if (ms > peakMaxMs) peakMaxMs = ms;
                peakN++; peakSumMs += ms;
                // NEW-MACHINE self-heal: a meter created against a stale/early default
                // endpoint (device ready after autostart, user switches device) reads
                // zero forever. ~10s of straight zeros -> rebuild against the current
                // default device. Between songs this costs one cheap COM activation.
                if (p <= 0.0001f) { zeroStreak++; } else { zeroStreak = 0; }
                if (zeroStreak > 300)
                {
                    meter = null; zeroStreak = 0;
                    Program.Log("[VU] meter read zero for ~10s -> recreated (endpoint may have changed)");
                }
                if (peakN >= 120)
                {
                    if (peakMaxMs > 5)
                        Program.Log(string.Format("[VU] GetPeakValue n={0} avg={1:F1}ms max={2:F1}ms",
                            peakN, peakSumMs / peakN, peakMaxMs));
                    peakN = 0; peakSumMs = 0; peakMaxMs = 0;
                }
                return p;
            }
            catch { meter = null; return 0f; }
        }
        int peakN, zeroStreak;
        double peakSumMs, peakMaxMs;

        static object CreateMeter()
        {
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice dev;
            en.GetDefaultAudioEndpoint(0, 0, out dev);
            Guid iid = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
            object o;
            dev.Activate(ref iid, 23, IntPtr.Zero, out o);
            return o;
        }

        void Render()
        {
            T += 0.033;
            using (Graphics g = Graphics.FromImage(buf))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                WidgetPaint.Palette pal = WidgetPaint.PaletteFor(AppShell.CurrentTheme);
                float peak = GetPeak();
                float baseX = 12, baseY = 66, maxH = 46, bw = 7, gap = 4.4f;
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(85, 5, 8, 16)))
                {
                    for (int i = 0; i < BARS; i++)
                    {
                        float target;
                        if (peak > 0.012f)
                            target = (float)Math.Min(1, peak * (0.95 + 1.6 * Math.Abs(Math.Sin(T * spd[i] + ph[i]))));
                        else target = 0.06f + 0.07f * (float)Math.Abs(Math.Sin(T * 1.9 + ph[i] * 0.35f));   // idle: gentle breathing wave, never looks broken
                        vals[i] += (target - vals[i]) * (target > vals[i] ? 0.5f : 0.16f);
                        float h = Math.Max(3, vals[i] * maxH);
                        float x = baseX + i * (bw + gap);
                        int alpha = 150 + (int)(105 * vals[i]);
                        g.FillRectangle(shadow, x + 1.5f, baseY - h + 1.5f, bw, h);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, pal.Accent.R, pal.Accent.G, pal.Accent.B)))
                            g.FillRectangle(b, x, baseY - h, bw, h);
                        // cap = accent pulled 55% toward white, keeps the lit-top look in every theme
                        Color ac = pal.Accent;
                        using (SolidBrush b2 = new SolidBrush(Color.FromArgb(Math.Min(255, alpha + 50),
                            (ac.R * 45 + 255 * 55) / 100, (ac.G * 45 + 255 * 55) / 100, (ac.B * 45 + 255 * 55) / 100)))
                            g.FillRectangle(b2, x, baseY - h, bw, 2.5f);
                    }
                }
                using (Font f = new Font("Segoe UI", 9.5f))
                using (SolidBrush gray = new SolidBrush(pal.Muted))
                    WidgetPaint.Text(g, "AUDIO", f, gray, 12, 70, pal);
            }
            LayeredPainter.Present(this, buf);
        }

        // ---- WASAPI metering COM interop ----
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorCom { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr collection);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            int GetDevice(string id, out IMMDevice device);
            int RegisterEndpointNotificationCallback(IntPtr client);
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            int OpenPropertyStore(int access, out IntPtr props);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            int GetState(out int state);
        }
        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            int GetPeakValue(out float peak);
            int GetMeteringChannelCount(out int count);
            int GetChannelsPeakValues(int count, IntPtr peaks);
            int QueryHardwareSupport(out int support);
        }
    }

    // ============ app launcher v3: acrylic fullscreen + owner-drawn canvas + recents ============
    class LauncherForm : Form
    {
        TextBox box;
        Panel scroll;
        TileCanvas canvas;
        Label status;
        DateTime shownAt = DateTime.Now;
        System.Threading.Timer backdropTimer;

        // the focus handoff after Show() races with Deactivate events - closing on
        // the first one made the panel flash and die instantly. Grace: ignore
        // deactivation during the first 700ms, then confirm loss after 350ms.
        void OnMaybeLostFocus()
        {
            Program.Log("[PANEL] deactivate fired (grace window)");
            if ((DateTime.Now - shownAt).TotalMilliseconds < 700) return;
            Timer grace = new Timer();
            grace.Interval = 350;
            grace.Tick += delegate
            {
                grace.Stop(); grace.Dispose();
                try
                {
                    // OS foreground is the truth - WinForms Focused can be false
                    // while the window IS foreground (quirk that flash-killed panels)
                    if (!IsDisposed && Program.GetForegroundWindow() != Handle) Close();
                }
                catch { }
            };
            grace.Start();
        }

        // ---------- recent/frequent tracking (shared with dock cold launches) ----------
        static object recLock = new object();
        static Dictionary<string, long[]> recents;   // pathLower -> { lastTicks, count }
        static string RecentFile { get { return Path.Combine(Program.BaseDir, "zpapaer_recent.txt"); } }
        static void LoadRecents()
        {
            if (recents != null) return;
            recents = new Dictionary<string, long[]>();
            try
            {
                foreach (string line in File.ReadAllLines(RecentFile))
                {
                    string[] p = line.Split('|');
                    if (p.Length != 3) continue;
                    long t, c;
                    if (long.TryParse(p[0], out t) && long.TryParse(p[1], out c))
                        recents[p[2].ToLower()] = new long[] { t, c };
                }
            }
            catch { }
        }
        public static void RecordRecent(string path)
        {
            if (path == null || path.Length == 0) return;
            try
            {
                lock (recLock)
                {
                    LoadRecents();
                    string k = path.ToLower();
                    long[] e;
                    if (!recents.TryGetValue(k, out e)) e = new long[] { 0, 0 };
                    e[0] = DateTime.Now.Ticks; e[1]++;
                    recents[k] = e;
                    StringBuilder sb = new StringBuilder();
                    foreach (KeyValuePair<string, long[]> kv in recents)
                        sb.AppendLine(kv.Value[0] + "|" + kv.Value[1] + "|" + kv.Key);
                    File.WriteAllText(RecentFile, sb.ToString());
                }
            }
            catch { }
        }

        public static void ShowLauncher()
        {
            try
            {
                LauncherForm f = new LauncherForm();
                f.Show();
                f.Activate();
            }
            catch (Exception ex) { Program.Log("[LAUNCH] open fail " + ex.Message); }
        }

        public LauncherForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            Rectangle sc = Screen.PrimaryScreen.Bounds;
            int m = Math.Max(8, sc.Height / 40);            // near-fullscreen with slim margin
            Bounds = new Rectangle(m, m, sc.Width - 2 * m, sc.Height - 2 * m);
            BackColor = Color.FromArgb(16, 21, 36);
            DoubleBuffered = true;
            // capture the desktop behind us BEFORE the panel becomes visible -
            // this is the acrylic backdrop the whole UI then floats on
            LiveBackdrop.Prime(this);

            Label title = new Label();
            title.Text = "搜索应用";
            title.BackColor = Color.Transparent;
            title.ForeColor = Color.FromArgb(165, 150, 180, 215);
            title.Font = new Font("Microsoft YaHei UI", 11f);
            title.AutoSize = true;
            title.Location = new Point(34, 26);
            Controls.Add(title);

            Label close = new Label();
            close.Text = "✕  关闭";
            close.BackColor = Color.Transparent;
            close.ForeColor = Color.FromArgb(175, 155, 170, 195);
            close.Font = new Font("Microsoft YaHei UI", 10f);
            close.AutoSize = true;
            close.Location = new Point(Width - 116, 26);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Close(); };
            Controls.Add(close);

            box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Color.FromArgb(255, 28, 35, 56);
            box.ForeColor = Color.White;
            box.Font = new Font("Microsoft YaHei UI", 16f);
            box.Bounds = new Rectangle(Width / 2 - 360, 60, 720, 48);
            box.TextChanged += delegate { ApplyFilter(); };
            Controls.Add(box);

            status = new Label();
            status.Text = "";
            status.BackColor = Color.Transparent;
            status.ForeColor = Color.FromArgb(140, 135, 155, 185);
            status.Font = new Font("Microsoft YaHei UI", 9f);
            status.AutoSize = true;
            status.Location = new Point(Width / 2 - 360, 116);
            Controls.Add(status);

            scroll = new BufferedPanel();
            scroll.BackColor = Color.Transparent;   // let the acrylic backdrop show through
            scroll.Bounds = new Rectangle(28, 142, Width - 56, Height - 168);
            scroll.AutoScroll = true;
            Controls.Add(scroll);

            canvas = new TileCanvas();
            canvas.Location = new Point(0, 0);
            scroll.Controls.Add(canvas);
            canvas.ItemClicked += delegate(TileCanvas.TTile t) { Launch(t.Path, null); };

            Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Color.FromArgb(80, 96, 210, 255), 1.3f))
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                using (Pen p2 = new Pen(Color.FromArgb(60, 96, 150, 210), 1f))
                    e.Graphics.DrawRectangle(p2, box.Left - 12, box.Top - 9, box.Width + 24, box.Height + 16);
            };
            Deactivate += delegate { OnMaybeLostFocus(); };   // outside click closes (with grace)
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.Enter)
                {
                    TileCanvas.TTile t = canvas.FirstVisible();
                    if (t != null) Launch(t.Path, null);
                    e.Handled = true;
                }
            };

            Shown += delegate
            {
                shownAt = DateTime.Now;
                // Prime() already put a real screen blur behind the panel; keep it
                // breathing with the animated wallpaper
                backdropTimer = LiveBackdrop.Start(this, this);
                // Deactivate can be missed entirely around Show()/focus games - poll
                // as a safety net so the panel can never linger unfocused forever
                Timer closeWatch = new Timer();
                closeWatch.Interval = 400;
                closeWatch.Tick += delegate
                {
                    if ((DateTime.Now - shownAt).TotalMilliseconds < 1200) return;
                    if (IsDisposed) { closeWatch.Stop(); return; }
                    // the OS foreground is the truth here: WinForms Focused can be
                    // false while the window IS foreground (focus lands on no
                    // control), which flash-killed perfectly usable panels
                    if (Program.GetForegroundWindow() != Handle)
                    {
                        Program.Log("[PANEL] watchdog close: focused=" + Focused
                            + " fgIsSelf=" + (Program.GetForegroundWindow() == Handle));
                        closeWatch.Stop(); closeWatch.Dispose(); Close();
                    }
                };
                closeWatch.Start();
                box.Focus();
                status.Text = " 正在准备…";
                ThreadPool.QueueUserWorkItem(delegate { Populate(); });
            };
        }

        // compose the whole window (form + transparent children) as one buffered
        // surface - kills the flicker while the tile list scrolls
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; }
        }

        internal static void BoxBlur(Bitmap b)
        {
            try
            {
                Rectangle r = new Rectangle(0, 0, b.Width, b.Height);
                BitmapData d = b.LockBits(r, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int w = b.Width, h = b.Height, stride = d.Stride;
                byte[] px = new byte[stride * h];
                Marshal.Copy(d.Scan0, px, 0, px.Length);
                byte[] tmp = new byte[px.Length];
                for (int pass = 0; pass < 2; pass++)
                {
                    bool horiz = pass == 0;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int a = 0, rr = 0, gg = 0, bb = 0, n = 0;
                            for (int k = -1; k <= 1; k++)
                            {
                                int xx = horiz ? x + k : x, yy = horiz ? y : y + k;
                                if (xx < 0 || xx >= w || yy < 0 || yy >= h) continue;
                                int i = yy * stride + xx * 4;
                                a += px[i]; rr += px[i + 1]; gg += px[i + 2]; bb += px[i + 3]; n++;
                            }
                            int o = y * stride + x * 4;
                            tmp[o] = (byte)(a / n); tmp[o + 1] = (byte)(rr / n);
                            tmp[o + 2] = (byte)(gg / n); tmp[o + 3] = (byte)(bb / n);
                        }
                    byte[] swap = px; px = tmp; tmp = swap;
                }
                Marshal.Copy(px, 0, d.Scan0, px.Length);
                b.UnlockBits(d);
            }
            catch { }
        }

        void Launch(string path, string arg)
        {
            RecordRecent(path);
            try
            {
                if (arg != null) Process.Start(path, arg);
                else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { Program.Log("[LAUNCH] start fail " + ex.Message); }
            Close();
        }

        void ApplyFilter()
        {
            if (canvas == null) return;
            canvas.Refilter(scroll.ClientSize.Width, box.Text);
            int n = canvas.VisibleCount;
            status.Text = (box.Text == null || box.Text.Trim().Length == 0) ? "" : (" " + n + " 个匹配 - 回车启动第一个");
        }

        void SafeInvoke(MethodInvoker d)
        {
            try { if (!IsDisposed && !Disposing) BeginInvoke(d); }
            catch { }
        }

        void Populate()
        {
            try
            {
                List<AppCache.CacheTile> cached = null;
                for (int wait = 0; wait < 30; wait++)
                {
                    cached = AppCache.Snapshot();
                    if (cached != null) break;
                    Thread.Sleep(300);
                }
                if (cached == null) { SafeInvoke(delegate { status.Text = " 索引不可用"; }); return; }

                // recents first
                List<AppCache.CacheTile> rec = new List<AppCache.CacheTile>();
                lock (recLock)
                {
                    LoadRecents();
                    List<KeyValuePair<string, long[]>> order = new List<KeyValuePair<string, long[]>>(recents);
                    order.Sort(delegate(KeyValuePair<string, long[]> a, KeyValuePair<string, long[]> b)
                    {
                        return Score(b.Value).CompareTo(Score(a.Value));
                    });
                    foreach (KeyValuePair<string, long[]> kv in order)
                    {
                        if (rec.Count >= 12) break;
                        foreach (AppCache.CacheTile c in cached)
                            if (c.Path.ToLower() == kv.Key) { rec.Add(c); break; }
                    }
                }
                Dictionary<string, List<AppCache.CacheTile>> cats = new Dictionary<string, List<AppCache.CacheTile>>();
                foreach (AppCache.CacheTile c in cached)
                {
                    if (rec.Contains(c)) continue;
                    List<AppCache.CacheTile> l;
                    if (!cats.TryGetValue(c.Cat, out l)) { l = new List<AppCache.CacheTile>(); cats[c.Cat] = l; }
                    l.Add(c);
                }
                List<string> keys = new List<string>(cats.Keys);
                keys.Sort(delegate(string a, string b)
                {
                    if (a == "其他" && b != "其他") return 1;
                    if (b == "其他" && a != "其他") return -1;
                    return string.Compare(a, b, StringComparison.CurrentCulture);
                });

                SafeInvoke(delegate
                {
                    if (IsDisposed) return;
                    try
                    {
                        canvas.SuspendLayout();
                        canvas.Sections.Clear();
                        if (rec.Count > 0)
                        {
                            TileCanvas.TSec s = canvas.NewSection("最近使用", Color.FromArgb(255, 110, 205, 255));
                            foreach (AppCache.CacheTile c in rec) s.Items.Add(TileCanvas.Mk(c.Name, c.Path, c.Icon, null));
                        }
                        foreach (string k in keys)
                        {
                            TileCanvas.TSec s = canvas.NewSection(k, Color.FromArgb(200, 150, 175, 215));
                            foreach (AppCache.CacheTile c in cats[k]) s.Items.Add(TileCanvas.Mk(c.Name, c.Path, c.Icon, null));
                        }
                        canvas.ResumeLayout(false);
                        canvas.Finish(scroll.ClientSize.Width);
                        status.Text = "";
                        ApplyFilter();
                    }
                    catch (Exception ex) { Program.Log("[LAUNCH] build fail " + ex.Message); }
                });
            }
            catch (Exception ex) { Program.Log("[LAUNCH] populate fail " + ex.Message); }
        }

        static double Score(long[] e)
        {
            double hours = (DateTime.Now.Ticks - e[0]) / 36000000000.0;
            return e[1] * 2.0 - hours * 0.05;
        }
    }

    static class AppCache
    {
        public class CacheTile { public string Name; public string Path; public string Cat; public Bitmap Icon; }
        static object lk = new object();
        static List<CacheTile> tiles;
        static string sig = "";
        static object iconLock = new object();
        static Dictionary<string, Bitmap> iconByPath = new Dictionary<string, Bitmap>();
        static System.Threading.Timer tick;

        public static string[] StartMenuRoots()
        {
            return new string[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Microsoft\Windows\Start Menu\Programs",
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Microsoft\Windows\Start Menu\Programs" };
        }

        public static void Start()
        {
            if (tick != null) return;
            tick = new System.Threading.Timer(delegate { try { Refresh(); } catch { } }, null, 500, 60000);
        }

        // cheap signature: file count + total size + newest write time over both roots;
        // unchanged signature = installed software unchanged = keep serving the cache
        static string Signature()
        {
            long count = 0, size = 0, newest = 0;
            foreach (string root in StartMenuRoots())
            {
                try
                {
                    foreach (string f in Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        count++;
                        try
                        {
                            FileInfo fi = new FileInfo(f);
                            size += fi.Length;
                            if (fi.LastWriteTimeUtc.Ticks > newest) newest = fi.LastWriteTimeUtc.Ticks;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return count + ":" + size + ":" + newest;
        }

        public static void Refresh()
        {
            try
            {
                string s = Signature();
                lock (lk) { if (s == sig && tiles != null) return; }
                HashSet<string> seen = new HashSet<string>();
                List<CacheTile> list = new List<CacheTile>();
                foreach (string root in StartMenuRoots())
                {
                    try
                    {
                        foreach (string lnk in Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories))
                        {
                            string nm = Path.GetFileNameWithoutExtension(lnk);
                            string low = nm.ToLower();
                            if (low.Contains("uninstall") || nm.Contains("卸载") || low.Contains("readme") || low.Contains("website") || low.Contains("visit") || low.Contains("error") || low.Contains("crash")) continue;
                            if (!seen.Add(low)) continue;
                            string cat = Path.GetFileName(Path.GetDirectoryName(lnk));
                            if (cat.ToLower() == "programs" || cat.Length == 0) cat = "其他";
                            CacheTile t = new CacheTile();
                            t.Name = nm; t.Path = lnk; t.Cat = cat; t.Icon = IconFor(lnk);
                            list.Add(t);
                        }
                    }
                    catch { }
                }
                list.Sort(delegate(CacheTile a, CacheTile b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
                lock (lk) { tiles = list; sig = s; }
                Program.Log("[CACHE] app index refreshed: " + list.Count + " entries");
            }
            catch (Exception ex) { Program.Log("[CACHE] refresh fail " + ex.Message); }
        }

        public static List<CacheTile> Snapshot()
        {
            lock (lk) { return tiles == null ? null : new List<CacheTile>(tiles); }
        }

        // icon cache keyed by path - extraction happens once per path, ever
        public static Bitmap IconFor(string path)
        {
            if (path == null || path.Length == 0) return null;
            lock (iconLock)
            {
                Bitmap c;
                if (iconByPath.TryGetValue(path, out c)) return c;
                Bitmap raw = null;
                try { raw = DockForm.LoadBest(path); } catch { }
                Bitmap sized = null;
                if (raw != null) { try { sized = new Bitmap(raw, 48, 48); } catch { } raw.Dispose(); }
                iconByPath[path] = sized;
                return sized;
            }
        }
    }

    // ============ 桌面 panel: This PC + Recycle Bin + archived desktop + live desktop ============
    // ============ live acrylic backdrop: our own animated scene, blurred ============
    // The DWM acrylic API is refused on this desktop (ExplorerPatcher), so we render
    // the wallpaper scene ourselves into the panel background, blurred and tinted.
    // Visually identical to real acrylic over the animated wallpaper, and LIVE.
    static class LiveBackdrop
    {
        // acrylic tint over the blur: dark enough to keep white text readable,
        // light enough that the wallpaper behind stays clearly visible
        static readonly Color Tint = Color.FromArgb(158, 14, 18, 34);

        static void SwapImage(Form form, Image next)
        {
            Image old = form.BackgroundImage;
            form.BackgroundImage = next;
            if (old != null) old.Dispose();
            form.Invalidate();
        }

        static Image Finish(Bitmap small, int w, int h)
        {
            using (Bitmap bg = new Bitmap(small, w, h))
            using (Graphics g2 = Graphics.FromImage(bg))
            {
                using (SolidBrush tint = new SolidBrush(Tint))
                    g2.FillRectangle(tint, 0, 0, w, h);
                return (Image)bg.Clone();
            }
        }

        static Bitmap Blur3(Bitmap small)
        {
            LauncherForm.BoxBlur(small);
            LauncherForm.BoxBlur(small);
            LauncherForm.BoxBlur(small);
            return small;
        }

        // one-shot backdrop captured from the real screen while the form is still
        // invisible, so the panel opens already showing a blur of what is behind
        // it (after Show it would capture the panel itself). UI thread, pre-Show.
        public static void Prime(Form form)
        {
            try
            {
                int w = form.Width, h = form.Height;
                int sw = Math.Max(2, w / 10), sh = Math.Max(2, h / 10);
                using (Bitmap raw = new Bitmap(w, h))
                {
                    using (Graphics g = Graphics.FromImage(raw))
                        g.CopyFromScreen(form.Left, form.Top, 0, 0, raw.Size);
                    using (Bitmap small = new Bitmap(raw, sw, sh))
                        SwapImage(form, Finish(Blur3(small), w, h));
                }
                Program.Log("[BACKDROP] primed " + w + "x" + h);
            }
            catch (Exception ex) { Program.Log("[BACKDROP] prime fail " + ex.Message); }
        }

        public static System.Threading.Timer Start(Form form, Control invokeTarget)
        {
            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer(delegate
            {
                try
                {
                    if (form.IsDisposed || form.Disposing) { if (timer != null) timer.Dispose(); return; }
                    Scene sc = AppShell.SceneRef;
                    if (sc == null) return;
                    int w = form.Width, h = form.Height;
                    int sw = Math.Max(2, w / 10), sh = Math.Max(2, h / 10);
                    Image result = null;
                    using (Bitmap small = new Bitmap(sw, sh))
                    {
                        using (Graphics g = Graphics.FromImage(small))
                        {
                            lock (AppShell.SceneLock)
                            {
                                g.ScaleTransform(sw / (float)sc.W, sh / (float)sc.H);
                                sc.Draw(g);
                            }
                        }
                        result = Finish(Blur3(small), w, h);
                    }
                    Image res = result;
                    try
                    {
                        invokeTarget.BeginInvoke((MethodInvoker)delegate
                        {
                            try
                            {
                                if (form.IsDisposed) { res.Dispose(); return; }
                                SwapImage(form, res);
                            }
                            catch { try { res.Dispose(); } catch { } }
                        });
                    }
                    catch { try { res.Dispose(); } catch { } }
                }
                catch { }
            }, null, 100, 700);
            return timer;
        }
    }

    // ============ owner-drawn tile canvas: hundreds of tiles, ZERO child controls ============
    // (per-tile Panels made the launcher freeze and crash with 190 apps - one paint
    // pass over a hit-tested canvas is instant and cannot storm the layout engine)
    // scroll host that composes itself and its transparent children in one
    // buffered unit - without WS_EX_COMPOSITED the tile canvas flickers on
    // every wheel step (background erase and tile paint hit the screen apart)
    class BufferedPanel : Panel
    {
        public BufferedPanel() { DoubleBuffered = true; }
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; }
        }
    }
    class TileCanvas : Control
    {
        public class TTile { public string Name; public string Path; public Bitmap Icon; public object Tag; public Rectangle Rect; public bool Vis = true; }
        public class TSec { public string Title; public Color Color; public List<TTile> Items = new List<TTile>(); public Rectangle HeadRect; public bool AnyVis; }
        public List<TSec> Sections = new List<TSec>();
        public string FilterText = "";
        public event Action<TTile> ItemClicked;

        const int TILE_W = 102, TILE_H = 110, PAD_X = 10, HEAD_H = 36, SEC_GAP = 12;
        Font headFont, nameFont;
        TTile hover;
        int viewW = 800;

        public TileCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            headFont = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            nameFont = new Font("Microsoft YaHei UI", 8.5f);
        }

        public TSec NewSection(string title, Color c)
        {
            TSec s = new TSec();
            s.Title = title; s.Color = c;
            Sections.Add(s);
            return s;
        }

        public static TTile Mk(string name, string path, Bitmap icon, object tag)
        {
            TTile t = new TTile();
            t.Name = name; t.Path = path; t.Icon = icon; t.Tag = tag;
            return t;
        }

        public void Finish(int width)
        {
            viewW = width;
            Measure();
            Invalidate();
        }

        public void Refilter(int width, string q)
        {
            FilterText = q == null ? "" : q.Trim().ToLower();
            viewW = width;
            Measure();
            Invalidate();
        }

        public int VisibleCount
        {
            get
            {
                int n = 0;
                foreach (TSec s in Sections) foreach (TTile t in s.Items) if (t.Vis) n++;
                return n;
            }
        }

        public TTile FirstVisible()
        {
            foreach (TSec s in Sections) foreach (TTile t in s.Items) if (t.Vis) return t;
            return null;
        }

        void Measure()
        {
            string q = FilterText;
            int y = 8;
            int inner = Math.Max(200, viewW - 26);
            foreach (TSec s in Sections)
            {
                s.AnyVis = false;
                foreach (TTile t in s.Items)
                {
                    t.Vis = q.Length == 0 || t.Name.ToLower().Contains(q) || (t.Path != null && t.Path.ToLower().Contains(q));
                    if (t.Vis) s.AnyVis = true;
                }
                if (!s.AnyVis) { s.HeadRect = Rectangle.Empty; continue; }
                s.HeadRect = new Rectangle(8, y, inner, 26);
                y += HEAD_H;
                int x = 6;
                foreach (TTile t in s.Items)
                {
                    if (!t.Vis) { t.Rect = Rectangle.Empty; continue; }
                    if (x + TILE_W > inner) { x = 6; y += TILE_H; }
                    t.Rect = new Rectangle(x, y, TILE_W, TILE_H - 8);
                    x += TILE_W + PAD_X;
                }
                y += TILE_H + SEC_GAP;
            }
            Size = new Size(viewW, y + 12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                Graphics g = e.Graphics;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                foreach (TSec s in Sections)
                {
                    if (!s.AnyVis) continue;
                    using (SolidBrush hb = new SolidBrush(s.Color))
                        g.DrawString(s.Title, headFont, hb, s.HeadRect.Left + 2, s.HeadRect.Top);
                    foreach (TTile t in s.Items)
                    {
                        if (!t.Vis) continue;
                        if (t == hover)
                        {
                            using (GraphicsPath hp = WidgetPaint.RoundRect(t.Rect.X + 4, t.Rect.Y + 2, TILE_W - 8, TILE_H - 14, 10))
                            using (SolidBrush hbr = new SolidBrush(Color.FromArgb(60, 90, 150, 220)))
                                g.FillPath(hbr, hp);
                        }
                        int icx = t.Rect.X + t.Rect.Width / 2;
                        if (t.Icon != null)
                            g.DrawImage(t.Icon, new Rectangle(icx - 24, t.Rect.Y + 2, 48, 48));
                        else
                        {
                            using (SolidBrush db = new SolidBrush(Color.FromArgb(160, 90, 160, 235)))
                                g.FillEllipse(db, icx - 20, t.Rect.Y + 6, 40, 40);
                        }
                        // name: up to 2 centered lines with ellipsis
                        DrawTwoLines(g, t.Name, nameFont, t.Rect.X + 1, t.Rect.Y + 56, t.Rect.Width - 2);
                    }
                }
            }
            catch { }
        }

        void DrawTwoLines(Graphics g, string text, Font f, int cx, int y, int w)
        {
            try
            {
                string l1 = text, l2 = null;
                SizeF sz = g.MeasureString(text, f);
                if (sz.Width > w)
                {
                    // split roughly in half at a char boundary
                    int cut = text.Length / 2;
                    while (cut > 1 && g.MeasureString(text.Substring(0, cut), f).Width > w) cut--;
                    l1 = text.Substring(0, cut);
                    l2 = text.Substring(cut);
                    while (l2.Length > 1 && g.MeasureString(l2, f).Width > w) l2 = l2.Substring(0, l2.Length - 1);
                    if (text.Length > l1.Length + l2.Length) l2 += "…";
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 230, 236, 246)))
                {
                    SizeF s1 = g.MeasureString(l1, f);
                    g.DrawString(l1, f, b, cx + (w - s1.Width) / 2f, y);
                    if (l2 != null)
                    {
                        SizeF s2 = g.MeasureString(l2, f);
                        g.DrawString(l2, f, b, cx + (w - s2.Width) / 2f, y + s1.Height - 3);
                    }
                }
            }
            catch { }
        }

        TTile HitTest(Point p)
        {
            foreach (TSec s in Sections)
                foreach (TTile t in s.Items)
                    if (t.Vis && t.Rect.Contains(p)) return t;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            TTile t = HitTest(e.Location);
            if (t != hover) { hover = t; Invalidate(); }
            Cursor = t != null ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hover != null) { hover = null; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            TTile t = HitTest(e.Location);
            if (t != null && ItemClicked != null) ItemClicked(t);
            base.OnMouseClick(e);
        }
    }

    // ============ 桌面 panel: the Windows desktop, organized ============
    class DesktopForm : Form
    {
        TextBox box;
        Panel scroll;
        TileCanvas canvas;
        Label status;
        DateTime shownAt = DateTime.Now;
        System.Threading.Timer backdropTimer;

        void OnMaybeLostFocus()
        {
            Program.Log("[PANEL] deactivate fired (grace window)");
            if ((DateTime.Now - shownAt).TotalMilliseconds < 700) return;
            Timer grace = new Timer();
            grace.Interval = 350;
            grace.Tick += delegate
            {
                grace.Stop(); grace.Dispose();
                try
                {
                    // OS foreground is the truth - WinForms Focused can be false
                    // while the window IS foreground (quirk that flash-killed panels)
                    if (!IsDisposed && Program.GetForegroundWindow() != Handle) Close();
                }
                catch { }
            };
            grace.Start();
        }

        public static void ShowDesk()
        {
            try
            {
                DesktopForm f = new DesktopForm();
                f.Show();
                f.Activate();
            }
            catch (Exception ex) { Program.Log("[DESK] open fail " + ex.Message); }
        }

        public DesktopForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            Rectangle sc = Screen.PrimaryScreen.Bounds;
            int m = Math.Max(8, sc.Height / 40);
            Bounds = new Rectangle(m, m, sc.Width - 2 * m, sc.Height - 2 * m);
            BackColor = Color.FromArgb(16, 21, 36);
            DoubleBuffered = true;
            // capture the desktop behind us BEFORE the panel becomes visible -
            // this is the acrylic backdrop the whole UI then floats on
            LiveBackdrop.Prime(this);

            Label title = new Label();
            title.Text = "桌面";
            title.BackColor = Color.Transparent;
            title.ForeColor = Color.FromArgb(165, 150, 180, 215);
            title.Font = new Font("Microsoft YaHei UI", 11f);
            title.AutoSize = true;
            title.Location = new Point(34, 26);
            Controls.Add(title);

            Label close = new Label();
            close.Text = "✕  关闭";
            close.BackColor = Color.Transparent;
            close.ForeColor = Color.FromArgb(175, 155, 170, 195);
            close.Font = new Font("Microsoft YaHei UI", 10f);
            close.AutoSize = true;
            close.Location = new Point(Width - 116, 26);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Close(); };
            Controls.Add(close);

            box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Color.FromArgb(255, 28, 35, 56);
            box.ForeColor = Color.White;
            box.Font = new Font("Microsoft YaHei UI", 15f);
            box.Bounds = new Rectangle(Width / 2 - 340, 58, 680, 46);
            box.TextChanged += delegate { ApplyFilter(); };
            Controls.Add(box);

            status = new Label();
            status.Text = " 正在读取桌面内容…";
            status.BackColor = Color.Transparent;
            status.ForeColor = Color.FromArgb(140, 135, 155, 185);
            status.Font = new Font("Microsoft YaHei UI", 9f);
            status.AutoSize = true;
            status.Location = new Point(Width / 2 - 340, 112);
            Controls.Add(status);

            scroll = new BufferedPanel();
            scroll.BackColor = Color.Transparent;   // let the acrylic backdrop show through
            scroll.Bounds = new Rectangle(28, 138, Width - 56, Height - 164);
            scroll.AutoScroll = true;
            Controls.Add(scroll);

            canvas = new TileCanvas();
            canvas.Location = new Point(0, 0);
            scroll.Controls.Add(canvas);
            canvas.ItemClicked += delegate(TileCanvas.TTile t)
            {
                try
                {
                    string arg = t.Tag as string;
                    if (arg != null) Process.Start("explorer.exe", arg);   // shell namespace
                    else Process.Start(new ProcessStartInfo(t.Path) { UseShellExecute = true });
                }
                catch (Exception ex) { Program.Log("[DESK] open item fail " + ex.Message); }
                Close();
            };

            Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Color.FromArgb(80, 96, 210, 255), 1.3f))
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                using (Pen p2 = new Pen(Color.FromArgb(60, 96, 150, 210), 1f))
                    e.Graphics.DrawRectangle(p2, box.Left - 12, box.Top - 9, box.Width + 24, box.Height + 16);
            };
            Deactivate += delegate { OnMaybeLostFocus(); };
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };

            Shown += delegate
            {
                shownAt = DateTime.Now;
                // Prime() already put a real screen blur behind the panel; keep it
                // breathing with the animated wallpaper
                backdropTimer = LiveBackdrop.Start(this, this);
                // Deactivate can be missed entirely around Show()/focus games - poll
                // as a safety net so the panel can never linger unfocused forever
                Timer closeWatch = new Timer();
                closeWatch.Interval = 400;
                closeWatch.Tick += delegate
                {
                    if ((DateTime.Now - shownAt).TotalMilliseconds < 1200) return;
                    if (IsDisposed) { closeWatch.Stop(); return; }
                    // the OS foreground is the truth here: WinForms Focused can be
                    // false while the window IS foreground (focus lands on no
                    // control), which flash-killed perfectly usable panels
                    if (Program.GetForegroundWindow() != Handle)
                    {
                        Program.Log("[PANEL] watchdog close: focused=" + Focused
                            + " fgIsSelf=" + (Program.GetForegroundWindow() == Handle));
                        closeWatch.Stop(); closeWatch.Dispose(); Close();
                    }
                };
                closeWatch.Start();
                box.Focus();
                ThreadPool.QueueUserWorkItem(delegate { Populate(); });
            };
        }

        // compose the whole window (form + transparent children) as one buffered
        // surface - kills the flicker while the tile list scrolls
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; }
        }

        void SafeInvoke(MethodInvoker d)
        {
            try { if (!IsDisposed && !Disposing) BeginInvoke(d); }
            catch { }
        }

        void ApplyFilter()
        {
            if (canvas == null) return;
            canvas.Refilter(scroll.ClientSize.Width, box.Text);
        }

        void Populate()
        {
            try
            {
                List<TileCanvas.TTile> sys = new List<TileCanvas.TTile>();
                sys.Add(MkSys("此电脑", "shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0));
                sys.Add(MkSys("回收站", "shell:::{645FF040-5081-101B-9F08-00AA002F954E}", 1));

                HashSet<string> names = new HashSet<string>();
                names.Add("此电脑"); names.Add("回收站");
                List<DeskEntry> entries = new List<DeskEntry>();
                try { CollectDir(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), entries, names); } catch { }
                try { CollectDir(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), entries, names); } catch { }

                // bucket by kind (worker thread - pure data work); most recently
                // touched first inside 最近使用, alphabetical inside the buckets
                List<DeskEntry> dirs = new List<DeskEntry>(), docs = new List<DeskEntry>(), imgs = new List<DeskEntry>();
                List<DeskEntry> archs = new List<DeskEntry>(), lnks = new List<DeskEntry>(), code = new List<DeskEntry>(), other = new List<DeskEntry>();
                foreach (DeskEntry e in entries)
                {
                    string ext = "";
                    try { ext = Path.GetExtension(e.Tile.Path ?? "").ToLower(); } catch { }
                    if (e.IsDir) dirs.Add(e);
                    else if (ext == ".lnk" || ext == ".url") lnks.Add(e);
                    else if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz" || ext == ".iso") archs.Add(e);
                    else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp" || ext == ".ico" || ext == ".svg") imgs.Add(e);
                    else if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".ppt" || ext == ".pptx" || ext == ".xls" || ext == ".xlsx"
                          || ext == ".txt" || ext == ".md" || ext == ".tex" || ext == ".bib" || ext == ".epub") docs.Add(e);
                    else if (ext == ".py" || ext == ".ipynb" || ext == ".js" || ext == ".ts" || ext == ".c" || ext == ".cpp" || ext == ".h" || ext == ".hpp"
                          || ext == ".java" || ext == ".cs" || ext == ".m" || ext == ".r" || ext == ".sh" || ext == ".bat" || ext == ".ps1"
                          || ext == ".yaml" || ext == ".yml" || ext == ".json" || ext == ".xml" || ext == ".toml" || ext == ".sql" || ext == ".vesta") code.Add(e);
                    else other.Add(e);
                }
                List<DeskEntry> recent = new List<DeskEntry>(entries);
                recent.Sort(delegate(DeskEntry a, DeskEntry b) { return b.Modified.CompareTo(a.Modified); });
                if (recent.Count > 12) recent.RemoveRange(12, recent.Count - 12);

                SafeInvoke(delegate
                {
                    if (IsDisposed) return;
                    try
                    {
                        canvas.Sections.Clear();
                        TileCanvas.TSec s1 = canvas.NewSection("系统", Color.FromArgb(255, 110, 205, 255));
                        s1.Items.AddRange(sys.ToArray());
                        AddBucket(canvas, "最近使用", Color.FromArgb(255, 110, 205, 255), recent, true);
                        AddBucket(canvas, "文件夹", Color.FromArgb(200, 150, 175, 215), dirs);
                        AddBucket(canvas, "文档", Color.FromArgb(200, 150, 175, 215), docs);
                        AddBucket(canvas, "图片", Color.FromArgb(200, 150, 175, 215), imgs);
                        AddBucket(canvas, "压缩包", Color.FromArgb(200, 150, 175, 215), archs);
                        AddBucket(canvas, "快捷方式", Color.FromArgb(200, 150, 175, 215), lnks);
                        AddBucket(canvas, "代码", Color.FromArgb(200, 150, 175, 215), code);
                        AddBucket(canvas, "其他", Color.FromArgb(200, 150, 175, 215), other);
                        canvas.Finish(scroll.ClientSize.Width);
                        status.Text = "";
                        ApplyFilter();
                    }
                    catch (Exception ex) { Program.Log("[DESK] build fail " + ex.Message); }
                });
            }
            catch (Exception ex) { Program.Log("[DESK] populate fail " + ex.Message); }
        }

        // empty buckets are skipped, named ones show their item count. 最近使用
        // shares TTile objects with the type buckets - those MUST be cloned, or
        // Measure() re-assigns their Rect and the row renders empty
        static void AddBucket(TileCanvas canvas, string title, Color c, List<DeskEntry> list, bool clone = false)
        {
            if (list == null || list.Count == 0) return;
            TileCanvas.TSec s = canvas.NewSection(title + " " + list.Count, c);
            foreach (DeskEntry e in list)
            {
                if (clone) s.Items.Add(TileCanvas.Mk(e.Tile.Name, e.Tile.Path, e.Tile.Icon, null));
                else s.Items.Add(e.Tile);
            }
        }

        TileCanvas.TTile MkSys(string name, string shellArg, int glyph)
        {
            TileCanvas.TTile t = TileCanvas.Mk(name, "explorer.exe", DrawGlyph(glyph), shellArg);
            return t;
        }

        // desktop item + the metadata the bucketing needs (kind, last write time)
        class DeskEntry { public TileCanvas.TTile Tile; public bool IsDir; public DateTime Modified; }

        void CollectDir(string dir, List<DeskEntry> into, HashSet<string> names)
        {
            foreach (string p in Directory.GetFileSystemEntries(dir))
            {
                string nm = Path.GetFileName(p);
                if (nm.ToLower().EndsWith(".ini") || nm.StartsWith(".")) continue;
                if (!names.Add(nm.ToLower())) continue;
                Bitmap ic = AppCache.IconFor(p);
                bool isDir = false;
                try { isDir = Directory.Exists(p); } catch { }
                if (ic == null && isDir) ic = DrawGlyph(2);
                DeskEntry e = new DeskEntry();
                e.Tile = TileCanvas.Mk(nm, p, ic, null);
                e.IsDir = isDir;
                try { e.Modified = File.GetLastWriteTime(p); } catch { e.Modified = DateTime.MinValue; }
                into.Add(e);
            }
            into.Sort(delegate(DeskEntry a, DeskEntry b)
            {
                return string.Compare(a.Tile.Name, b.Tile.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        // 0=monitor(此电脑) 1=bin(回收站) 2=folder
        static Bitmap DrawGlyph(int kind)
        {
            Bitmap b = new Bitmap(48, 48);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (kind == 0)
                {
                    using (Pen p = new Pen(Color.FromArgb(235, 225, 238, 252), 2.6f))
                    {
                        g.DrawRectangle(p, 8, 8, 32, 22);
                        g.DrawLine(p, 24, 30, 24, 36);
                        g.DrawLine(p, 15, 38, 33, 38);
                    }
                }
                else if (kind == 1)
                {
                    using (Pen p = new Pen(Color.FromArgb(160, 215, 235, 252), 2.6f))
                    {
                        g.DrawRectangle(p, 13, 12, 22, 5);
                        g.DrawLine(p, 20, 9, 28, 9);
                        g.DrawRectangle(p, 16, 17, 16, 22);
                        g.DrawLine(p, 22, 21, 22, 34);
                        g.DrawLine(p, 26, 21, 26, 34);
                    }
                }
                else
                {
                    using (SolidBrush fb = new SolidBrush(Color.FromArgb(220, 90, 160, 235)))
                        g.FillRectangle(fb, 7, 12, 34, 27);
                    using (SolidBrush fb2 = new SolidBrush(Color.FromArgb(235, 120, 185, 250)))
                        g.FillRectangle(fb2, 7, 8, 18, 8);
                }
            }
            return b;
        }
    }

    // ============ audio endpoint volume (default output device) ============
    // Interop shape proven by tools/audprobe.exe (get + write + restore).
    // The default endpoint can change at runtime (USB docks etc.), so the cached
    // interface is dropped on any failure and re-activated on the next call.
    static class AudioCtl
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorCom { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr collection);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            int GetDevice(string id, out IMMDevice device);
            int RegisterEndpointNotificationCallback(IntPtr client);
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            int OpenPropertyStore(int access, out IntPtr props);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            int GetState(out int state);
        }
        // full vtable in header order - the rcw maps by position only
        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr notify);
            int UnregisterControlChangeNotify(IntPtr notify);
            int GetChannelCount(out uint count);
            int SetMasterVolumeLevel(float level, ref Guid ctx);
            int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
            int GetMasterVolumeLevel(out float level);
            int GetMasterVolumeLevelScalar(out float level);
            int SetChannelVolumeLevel(uint ch, float level, ref Guid ctx);
            int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
            int GetChannelVolumeLevel(uint ch, out float level);
            int GetChannelVolumeLevelScalar(uint ch, out float level);
            int SetMute(bool mute, ref Guid ctx);
            int GetMute(out bool mute);
            int VolumeStepUp(ref Guid ctx);
            int VolumeStepDown(ref Guid ctx);
            int QueryHardwareSupport(out uint support);
            int GetVolumeRange(out float min, out float max, out float step);
        }

        static IAudioEndpointVolume cached;
        static Guid Ctx = Guid.Empty;

        static IAudioEndpointVolume Vol()
        {
            if (cached != null) return cached;
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice dev;
            en.GetDefaultAudioEndpoint(0, 1, out dev);          // eRender, eMultimedia
            object o;
            Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
            dev.Activate(ref iid, 23, IntPtr.Zero, out o);      // CLSCTX_ALL
            cached = (IAudioEndpointVolume)o;
            return cached;
        }

        // false = no usable endpoint (no device / COM failure); callers must cope
        public static bool GetVolume(out int percent, out bool muted)
        {
            percent = 0; muted = false;
            try
            {
                float lv; Vol().GetMasterVolumeLevelScalar(out lv);
                bool mu; Vol().GetMute(out mu);
                percent = (int)Math.Round(lv * 100f);
                muted = mu;
                return true;
            }
            catch { cached = null; return false; }
        }

        public static bool SetVolume(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            try { Vol().SetMasterVolumeLevelScalar(percent / 100f, ref Ctx); return true; }
            catch { cached = null; return false; }
        }

        public static bool SetMute(bool mute)
        {
            try { Vol().SetMute(mute, ref Ctx); return true; }
            catch { cached = null; return false; }
        }
    }

    // ============ network state: wired status + WLAN enumerate/connect ============
    // Interop shapes proven by tools/netprobe.exe against the live machine
    // (entry size 628, iface info 532; flags 1=connected 2=has-profile).
    // Only per-user profiles are created (flags=2) so no elevation is needed.
    static class NetCtl
    {
        [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint clientVer, IntPtr reserved, out uint negotiated, out IntPtr handle);
        [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanGetAvailableNetworkList(IntPtr handle, ref Guid iface, uint flags, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr handle, ref Guid iface, int opcode, IntPtr reserved, out uint dataSize, ref IntPtr data, IntPtr opcodeType);
        [DllImport("wlanapi.dll")] static extern int WlanScan(IntPtr handle, ref Guid iface, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern int WlanConnect(IntPtr handle, ref Guid iface, ref WLAN_CONNECTION_PARAMETERS p, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern int WlanSetProfile(IntPtr handle, ref Guid iface, uint flags, [MarshalAs(UnmanagedType.LPWStr)] string xml, IntPtr allUserProfileSecurity, int overwrite, IntPtr reserved, out uint reason);
        [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Desc;
            public int State;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DOT11_SSID { public uint Length; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Ssid; }

        [StructLayout(LayoutKind.Sequential)]
        struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID Ssid;
            public int BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Bssid;
            public int PhyType;
            public uint PhyIndex;
            public uint SignalQuality;
            public uint RxRate;
            public uint TxRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WLAN_SECURITY_ATTRIBUTES
        {
            public int SecurityEnabled;
            public int AuthAlgo;
            public int CipherAlgo;
        }

        // layout hand-checked against wlanapi.h (profileName starts at offset 156)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WLAN_CONNECTION_ATTRIBUTES
        {
            public int State;
            public int ConnectionMode;
            public DOT11_SSID Ssid;
            public int BssType;
            public int SecurityEnabled;
            public int AuthAlgo;
            public int CipherAlgo;
            public uint Flags;
            public uint KeyIndex;
            public WLAN_ASSOCIATION_ATTRIBUTES Assoc;
            public WLAN_SECURITY_ATTRIBUTES Sec;
            public uint ReasonCode;
            public uint ProfileFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WLAN_AVAILABLE_NETWORK
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
            public DOT11_SSID Ssid;
            public int BssType;
            public uint NumberOfBssids;
            public int Connectable;
            public uint NotConnectableReason;
            public uint NumberOfPhyTypes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public int[] PhyTypes;
            public int MorePhyTypes;
            public uint SignalQuality;
            public int SecurityEnabled;
            public int DefaultAuthAlgorithm;
            public int DefaultCipherAlgorithm;
            public uint Flags;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WLAN_CONNECTION_PARAMETERS
        {
            public int Mode;                                // 0 = wlan_connection_mode_profile
            [MarshalAs(UnmanagedType.LPWStr)] public string Profile;
            public IntPtr Ssid;                             // unused in profile mode
            public IntPtr DesiredBssidList;
            public int BssType;
            public uint Flags;
        }

        public class WifiNet
        {
            public string Ssid, Profile;
            public uint Signal;
            public bool Secured, HasProfile, Connected, Connectable;
            public int Auth, Cipher, BssType;
            public bool NeedsPassword { get { return Secured && !HasProfile; } }
        }

        public class Snapshot
        {
            public bool WlanOk;
            public bool WifiConnected;
            public string Ssid = "";
            public uint Signal;
            public List<WifiNet> Nets = new List<WifiNet>();
        }

        const uint FLAG_CONNECTED = 1, FLAG_HAS_PROFILE = 2;

        static string SsidStr(DOT11_SSID s)
        {
            if (s.Ssid == null || s.Length == 0 || s.Length > 32) return "";
            return Encoding.UTF8.GetString(s.Ssid, 0, (int)s.Length);
        }

        // best UP ethernet adapter, human-readable; every failure stays a string
        public static string EthernetStatus()
        {
            try
            {
                ulong bestSpeed = 0;
                foreach (System.Net.NetworkInformation.NetworkInterface ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Ethernet) continue;
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if ((ulong)ni.Speed > bestSpeed) bestSpeed = (ulong)ni.Speed;
                }
                if (bestSpeed == 0) return "未连接";
                double mbps = bestSpeed / 1000000.0;
                if (mbps >= 1000) return "已连接（" + (mbps / 1000.0).ToString("F0") + " Gbps）";
                if (mbps >= 1) return "已连接（" + mbps.ToString("F0") + " Mbps）";
                return "已连接";
            }
            catch { return "未知"; }
        }

        // one snapshot for the whole submenu: current connection + merged network
        // list (same SSID shows up once per band / per profile - keep the
        // strongest signal and OR the profile/connected flags)
        public static Snapshot WifiSnapshot()
        {
            Snapshot s = new Snapshot();
            IntPtr h; uint ver;
            if (WlanOpenHandle(2, IntPtr.Zero, out ver, out h) != 0) return s;
            try
            {
                IntPtr il;
                if (WlanEnumInterfaces(h, IntPtr.Zero, out il) != 0) return s;
                try
                {
                    uint n = (uint)Marshal.ReadInt32(il);
                    int infoSz = Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO));
                    Dictionary<string, WifiNet> bySsid = new Dictionary<string, WifiNet>();
                    for (int i = 0; i < n; i++)
                    {
                        WLAN_INTERFACE_INFO ifo = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(
                            new IntPtr(il.ToInt64() + 8 + i * infoSz), typeof(WLAN_INTERFACE_INFO));
                        IntPtr data = IntPtr.Zero; uint size;
                        if (WlanQueryInterface(h, ref ifo.InterfaceGuid, 7, IntPtr.Zero, out size, ref data, IntPtr.Zero) == 0 && data != IntPtr.Zero)
                        {
                            try
                            {
                                WLAN_CONNECTION_ATTRIBUTES cc = (WLAN_CONNECTION_ATTRIBUTES)Marshal.PtrToStructure(data, typeof(WLAN_CONNECTION_ATTRIBUTES));
                                if (cc.State == 1)  // wlan_interface_state_connected
                                {
                                    string ssid = SsidStr(cc.Ssid);
                                    if (ssid.Length > 0) { s.WifiConnected = true; s.Ssid = ssid; s.Signal = cc.Assoc.SignalQuality; }
                                }
                            }
                            finally { WlanFreeMemory(data); }
                        }
                        IntPtr al;
                        if (WlanGetAvailableNetworkList(h, ref ifo.InterfaceGuid, 2, IntPtr.Zero, out al) != 0) continue;
                        try
                        {
                            uint cnt = (uint)Marshal.ReadInt32(al);
                            int netSz = Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK));
                            for (int k = 0; k < cnt; k++)
                            {
                                WLAN_AVAILABLE_NETWORK an = (WLAN_AVAILABLE_NETWORK)Marshal.PtrToStructure(
                                    new IntPtr(al.ToInt64() + 8 + k * netSz), typeof(WLAN_AVAILABLE_NETWORK));
                                string ssid = SsidStr(an.Ssid);
                                if (ssid.Length == 0) continue;     // hidden nets: not selectable here
                                WifiNet cur;
                                if (!bySsid.TryGetValue(ssid, out cur))
                                {
                                    cur = new WifiNet();
                                    cur.Ssid = ssid;
                                    bySsid[ssid] = cur;
                                }
                                if (an.SignalQuality > cur.Signal) cur.Signal = an.SignalQuality;
                                if ((an.Flags & FLAG_CONNECTED) != 0) cur.Connected = true;
                                if ((an.Flags & FLAG_HAS_PROFILE) != 0 && an.ProfileName.Length > 0)
                                {
                                    cur.HasProfile = true;
                                    cur.Profile = an.ProfileName;
                                }
                                if (an.SecurityEnabled != 0)
                                {
                                    cur.Secured = true;
                                    cur.Auth = an.DefaultAuthAlgorithm;
                                    cur.Cipher = an.DefaultCipherAlgorithm;
                                }
                                if (an.Connectable != 0) cur.Connectable = true;
                                if (an.BssType == 2) cur.BssType = 2;
                            }
                        }
                        finally { WlanFreeMemory(al); }
                    }
                    s.WlanOk = true;
                    s.Nets.AddRange(bySsid.Values);
                    s.Nets.Sort(delegate (WifiNet a, WifiNet b)
                    {
                        if (a.Connected != b.Connected) return a.Connected ? -1 : 1;
                        int c = b.Signal.CompareTo(a.Signal);
                        return c != 0 ? c : string.Compare(a.Ssid, b.Ssid);
                    });
                }
                finally { WlanFreeMemory(il); }
            }
            finally { WlanCloseHandle(h, IntPtr.Zero); }
            return s;
        }

        // fire-and-forget: results only show up in the NEXT snapshot
        public static void TriggerScan()
        {
            try
            {
                IntPtr h; uint ver;
                if (WlanOpenHandle(2, IntPtr.Zero, out ver, out h) != 0) return;
                try
                {
                    IntPtr il;
                    if (WlanEnumInterfaces(h, IntPtr.Zero, out il) != 0) return;
                    try
                    {
                        uint n = (uint)Marshal.ReadInt32(il);
                        int infoSz = Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO));
                        for (int i = 0; i < n; i++)
                        {
                            WLAN_INTERFACE_INFO ifo = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(
                                new IntPtr(il.ToInt64() + 8 + i * infoSz), typeof(WLAN_INTERFACE_INFO));
                            WlanScan(h, ref ifo.InterfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                    finally { WlanFreeMemory(il); }
                }
                finally { WlanCloseHandle(h, IntPtr.Zero); }
            }
            catch { }
        }

        // connect to ssid. password null + no profile = open network, a per-user
        // profile is created for it too so both paths share the profile connect.
        // Returns null when the async connect was ACCEPTED (poll WaitConnected
        // for the outcome), a user-readable error string otherwise.
        public static string Connect(string ssid, string password)
        {
            try
            {
                IntPtr h; uint ver;
                if (WlanOpenHandle(2, IntPtr.Zero, out ver, out h) != 0) return "WLAN 服务不可用";
                try
                {
                    IntPtr il;
                    if (WlanEnumInterfaces(h, IntPtr.Zero, out il) != 0) return "WLAN 接口枚举失败";
                    try
                    {
                        uint n = (uint)Marshal.ReadInt32(il);
                        if (n < 1) return "没有 WLAN 接口";
                        WLAN_INTERFACE_INFO ifo = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(
                            new IntPtr(il.ToInt64() + 8), typeof(WLAN_INTERFACE_INFO));

                        string profile = null;
                        int auth = 7, cipher = 4, bss = 1;   // WPA2PSK/AES defaults
                        IntPtr al;
                        if (WlanGetAvailableNetworkList(h, ref ifo.InterfaceGuid, 2, IntPtr.Zero, out al) == 0)
                        {
                            try
                            {
                                uint cnt = (uint)Marshal.ReadInt32(al);
                                int netSz = Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK));
                                for (int k = 0; k < cnt; k++)
                                {
                                    WLAN_AVAILABLE_NETWORK an = (WLAN_AVAILABLE_NETWORK)Marshal.PtrToStructure(
                                        new IntPtr(al.ToInt64() + 8 + k * netSz), typeof(WLAN_AVAILABLE_NETWORK));
                                    if (SsidStr(an.Ssid) != ssid) continue;
                                    if ((an.Flags & FLAG_HAS_PROFILE) != 0 && an.ProfileName.Length > 0 && profile == null)
                                        profile = an.ProfileName;
                                    if (an.SecurityEnabled != 0) { auth = an.DefaultAuthAlgorithm; cipher = an.DefaultCipherAlgorithm; }
                                    if (an.BssType == 2) bss = 2;
                                }
                            }
                            finally { WlanFreeMemory(al); }
                        }

                        if (profile == null)
                        {
                            uint reason;
                            int hr = WlanSetProfile(h, ref ifo.InterfaceGuid, 2 /*per-user, no elevation*/,
                                ProfileXml(ssid, password, auth, cipher), IntPtr.Zero, 1 /*overwrite*/, IntPtr.Zero, out reason);
                            if (hr != 0) return "保存网络配置失败（0x" + hr.ToString("X") + "）";
                            profile = ssid;
                        }

                        WLAN_CONNECTION_PARAMETERS p = new WLAN_CONNECTION_PARAMETERS();
                        p.Mode = 0;
                        p.Profile = profile;
                        p.Ssid = IntPtr.Zero;
                        p.DesiredBssidList = IntPtr.Zero;
                        p.BssType = bss;
                        p.Flags = 0;
                        if (WlanConnect(h, ref ifo.InterfaceGuid, ref p, IntPtr.Zero) != 0)
                            return "连接命令被拒绝";
                        return null;
                    }
                    finally { WlanFreeMemory(il); }
                }
                finally { WlanCloseHandle(h, IntPtr.Zero); }
            }
            catch (Exception ex) { return ex.Message; }
        }

        // WlanConnect is async: poll until this ssid is the connected one
        public static bool WaitConnected(string ssid, int timeoutMs)
        {
            DateTime until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < until)
            {
                Thread.Sleep(500);
                try
                {
                    Snapshot s = WifiSnapshot();
                    if (s.WifiConnected && s.Ssid == ssid) return true;
                }
                catch { }
            }
            return false;
        }

        static string ProfileXml(string ssid, string password, int auth, int cipher)
        {
            bool psk = password != null;
            string authTxt = psk ? (auth == 4 ? "WPAPSK" : "WPA2PSK") : "open";
            string encTxt = psk ? (cipher == 2 ? "TKIP" : "AES") : "none";
            string name = XmlEsc(ssid);
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?>");
            sb.Append("<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">");
            sb.Append("<name>").Append(name).Append("</name>");
            sb.Append("<SSIDConfig><SSID><name>").Append(name).Append("</name></SSID></SSIDConfig>");
            sb.Append("<connectionType>ESS</connectionType><connectionMode>manual</connectionMode>");
            sb.Append("<MSM><security><authEncryption>");
            sb.Append("<authentication>").Append(authTxt).Append("</authentication>");
            sb.Append("<encryption>").Append(encTxt).Append("</encryption>");
            sb.Append("<useOneX>false</useOneX></authEncryption>");
            if (psk)
                sb.Append("<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>")
                  .Append(XmlEsc(password)).Append("</keyMaterial></sharedKey>");
            sb.Append("</security></MSM></WLANProfile>");
            return sb.ToString();
        }

        static string XmlEsc(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }

    // ============ volume slider popup (layered window, dock panel language) ============
    class VolumeSliderForm : Form
    {
        static VolumeSliderForm cur;
        Bitmap buf;
        Timer poll;
        DateTime shownAt = DateTime.Now;
        int vol = -1;                   // -1 = endpoint missing
        bool muted, dragging;
        Rectangle rSpk, rTrack;         // track row geometry (knob x derives from vol)
        const int FW = 272, FH = 116, TRACKH = 8, KNOB = 20;

        public static void ShowAt(int screenX, int panelTopY)
        {
            try
            {
                if (cur != null) { try { cur.Close(); } catch { } cur = null; }
                VolumeSliderForm f = new VolumeSliderForm();
                Rectangle sc = Screen.PrimaryScreen.Bounds;
                int x = screenX - FW / 2;
                if (x < 8) x = 8;
                if (x + FW > sc.Width - 8) x = sc.Width - 8 - FW;
                int y = panelTopY - FH - 10;
                if (y < 8) y = 8;
                f.Location = new Point(x, y);
                f.Render();
                f.Show();
                f.Activate();
            }
            catch (Exception ex) { Program.Log("[VOL] open fail " + ex.Message); }
        }

        public VolumeSliderForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            ClientSize = new Size(FW, FH);
            buf = new Bitmap(FW, FH, PixelFormat.Format32bppArgb);
            AudioCtl.GetVolume(out vol, out muted);
            // single row above the panel bottom: [speaker 44px][track][pct 74px]
            int cy = 66;
            rSpk = new Rectangle(16, cy - 18, 44, 36);
            rTrack = new Rectangle(70, cy - TRACKH / 2, FW - 70 - 82, TRACKH);
            poll = new Timer();
            poll.Interval = 350;
            poll.Tick += delegate
            {
                if (dragging) return;               // never fight the user's hand
                int v; bool m;
                if (AudioCtl.GetVolume(out v, out m) && (v != vol || m != muted))
                {
                    vol = v; muted = m; Render();
                }
            };
            poll.Start();
        }

        protected override bool ShowWithoutActivation { get { return false; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x80000;       // TOOLWINDOW | LAYERED
                return cp;
            }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        int KnobX()
        {
            if (vol < 0) vol = 0;
            return rTrack.X + 3 + (rTrack.Width - 6) * vol / 100;
        }

        void Render()
        {
            try
            {
                using (Graphics g = Graphics.FromImage(buf))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);
                    WidgetPaint.Palette pal = WidgetPaint.PaletteFor(AppShell.CurrentTheme);

                    using (GraphicsPath panel = WidgetPaint.RoundRect(0, 0, FW, FH, 18))
                    {
                        using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 0, FW, FH),
                            Color.FromArgb(216, 42, 50, 74), Color.FromArgb(236, 11, 15, 29), 90f))
                            g.FillPath(lgb, panel);
                        using (Pen pn = new Pen(Color.FromArgb(42, 105, 160, 255), 1.1f))
                            g.DrawPath(pn, panel);
                        using (Pen hl = new Pen(Color.FromArgb(34, 165, 205, 255), 1f))
                            g.DrawLine(hl, 16, 1.6f, FW - 16, 1.6f);
                    }

                    using (Font cap = new Font("Microsoft YaHei UI", 9f))
                    using (SolidBrush cb = new SolidBrush(Color.FromArgb(190, 208, 220, 244)))
                        WidgetPaint.Text(g, vol < 0 ? "音频输出不可用" : (muted ? "输出音量（已静音）" : "输出音量"), cap, cb, 18, 12, pal);

                    DrawSpeaker(g, muted, pal);

                    // trough
                    using (GraphicsPath tr = WidgetPaint.RoundRect(rTrack.X, rTrack.Y, rTrack.Width, TRACKH, TRACKH / 2))
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(110, 4, 7, 16)))
                        g.FillPath(tb, tr);
                    if (vol > 0)
                    {
                        int fx = KnobX();
                        int w = fx - rTrack.X;
                        if (w > TRACKH / 2)
                            using (GraphicsPath fill = WidgetPaint.RoundRect(rTrack.X, rTrack.Y, w, TRACKH, TRACKH / 2))
                            using (LinearGradientBrush fb = new LinearGradientBrush(
                                new Rectangle(rTrack.X, rTrack.Y, w, TRACKH),
                                Color.FromArgb(235, pal.Accent), Color.FromArgb(175, pal.Accent), 0f))
                                g.FillPath(fb, fill);
                    }
                    if (vol >= 0)
                    {
                        int kx = KnobX(), ky = rTrack.Y + TRACKH / 2;
                        g.FillEllipse(Brushes.White, kx - KNOB / 2, ky - KNOB / 2, KNOB, KNOB);
                        using (Pen kb = new Pen(Color.FromArgb(120, 8, 12, 24), 1.4f))
                            g.DrawEllipse(kb, kx - KNOB / 2, ky - KNOB / 2, KNOB, KNOB);
                    }

                    string pct = vol < 0 ? "--" : vol.ToString();
                    using (Font pf = new Font("Microsoft YaHei UI", 13.5f, FontStyle.Bold))
                    using (SolidBrush pb = new SolidBrush(Color.FromArgb(240, 244, 248, 255)))
                    {
                        SizeF ts = g.MeasureString(pct, pf);
                        WidgetPaint.Text(g, pct, pf, pb, FW - 34 - ts.Width, 66 - ts.Height / 2f, pal);
                    }
                    using (Font sf = new Font("Microsoft YaHei UI", 8.5f))
                    using (SolidBrush sb2 = new SolidBrush(Color.FromArgb(170, 190, 205, 235)))
                        WidgetPaint.Text(g, "%", sf, sb2, FW - 30, 62, pal);
                }
                LayeredPainter.Present(this, buf);
            }
            catch { }
        }

        void DrawSpeaker(Graphics g, bool m, WidgetPaint.Palette pal)
        {
            float cy = rSpk.Top + rSpk.Height / 2f;
            float x0 = rSpk.Left + 6;
            using (SolidBrush br = new SolidBrush(Color.FromArgb(238, 238, 246)))
            {
                PointF[] cone = {
                    new PointF(x0, cy - 5), new PointF(x0 + 6, cy - 5),
                    new PointF(x0 + 13, cy - 11), new PointF(x0 + 13, cy + 11),
                    new PointF(x0 + 6, cy + 5), new PointF(x0, cy + 5) };
                g.FillPolygon(br, cone);
            }
            if (m)
            {
                using (Pen p = new Pen(Color.FromArgb(240, 255, 118, 118), 2.2f))
                {
                    g.DrawLine(p, x0 + 17, cy - 7, x0 + 27, cy + 7);
                    g.DrawLine(p, x0 + 27, cy - 7, x0 + 17, cy + 7);
                }
            }
            else
            {
                using (Pen p = new Pen(pal.Accent, 2f))
                {
                    g.DrawArc(p, x0 + 14, cy - 7, 10, 14, -55, 110);
                    g.DrawArc(p, x0 + 19, cy - 11, 14, 22, -55, 110);
                }
            }
        }

        void ApplyFromX(int x)
        {
            if (vol < 0) return;
            int pct = (int)Math.Round((x - rTrack.X - 3) * 100f / (rTrack.Width - 6));
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            if (pct != vol) { vol = pct; AudioCtl.SetVolume(pct); }
            if (muted && pct > 0) { muted = false; AudioCtl.SetMute(false); }
            Render();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && vol >= 0)
            {
                if (e.X >= rSpk.Left - 6 && e.X <= rSpk.Right + 6)
                {
                    muted = !muted;
                    AudioCtl.SetMute(muted);
                    Render();
                    return;
                }
                if (e.X >= rTrack.X - 16 && e.X <= rTrack.Right + 16)
                {
                    dragging = true;
                    Capture = true;
                    ApplyFromX(e.X);
                    return;
                }
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) ApplyFromX(e.X);
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging) { dragging = false; Capture = false; }
            base.OnMouseUp(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (vol >= 0 && !dragging)
            {
                int v = vol + (e.Delta > 0 ? 5 : -5);
                if (v < 0) v = 0;
                if (v > 100) v = 100;
                vol = v;
                AudioCtl.SetVolume(v);
                if (muted && v > 0) { muted = false; AudioCtl.SetMute(false); }
                Render();
            }
            base.OnMouseWheel(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // same focus-race pattern as the launcher: ignore deactivation in the
            // first moments after Show, then confirm real loss before closing
            Timer t = new Timer();
            t.Interval = 260;
            t.Tick += delegate
            {
                t.Stop(); t.Dispose();
                try { if (!IsDisposed && !dragging && !Focused && (DateTime.Now - shownAt).TotalMilliseconds > 400) Close(); }
                catch { }
            };
            t.Start();
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            poll.Stop();
            poll.Dispose();
            if (buf != null) buf.Dispose();
            if (cur == this) cur = null;
            base.OnFormClosed(e);
        }
    }

    // ============ wifi password prompt (dark, enter-to-connect) ============
    class WifiPasswordForm : Form
    {
        string ssid;
        TextBox box;
        Label status;
        Button ok, cancel;
        bool busy;

        public static void ShowFor(string ssid, Point anchorScreen)
        {
            try
            {
                WifiPasswordForm f = new WifiPasswordForm(ssid);
                Rectangle sc = Screen.PrimaryScreen.Bounds;
                int x = anchorScreen.X - f.Width / 2;
                if (x < 8) x = 8;
                if (x + f.Width > sc.Width - 8) x = sc.Width - 8 - f.Width;
                int y = anchorScreen.Y;
                if (y + f.Height > sc.Height - 8) y = sc.Height - 8 - f.Height;
                if (y < 8) y = 8;
                f.Location = new Point(x, y);
                f.ShowDialog();
            }
            catch (Exception ex) { Program.Log("[NET] password dialog fail " + ex.Message); }
        }

        public WifiPasswordForm(string ssid)
        {
            this.ssid = ssid;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            ClientSize = new Size(316, 178);
            BackColor = Color.FromArgb(13, 17, 32);
            Region = new Region(WidgetPaint.RoundRect(0, 0, 316, 178, 16));
            int W = 316;

            Label title = new Label();
            title.Text = "连接到「" + ssid + "」";
            title.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(242, 244, 252);
            title.Location = new Point(20, 14);
            title.AutoSize = true;
            Controls.Add(title);

            Label hint = new Label();
            hint.Text = "输入网络密码（将保存为本机网络配置）";
            hint.Font = new Font("Microsoft YaHei UI", 8.5f);
            hint.ForeColor = Color.FromArgb(168, 182, 210);
            hint.Location = new Point(20, 46);
            hint.AutoSize = true;
            Controls.Add(hint);

            box = new TextBox();
            box.Font = new Font("Microsoft YaHei UI", 11f);
            box.BackColor = Color.FromArgb(24, 30, 52);
            box.ForeColor = Color.White;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.UseSystemPasswordChar = true;
            box.Location = new Point(20, 72);
            box.Size = new Size(W - 40, 30);
            Controls.Add(box);

            status = new Label();
            status.Font = new Font("Microsoft YaHei UI", 8.5f);
            status.ForeColor = Color.FromArgb(255, 140, 140);
            status.Location = new Point(20, 108);
            status.AutoSize = true;
            Controls.Add(status);

            cancel = new Button();
            cancel.Text = "取消";
            cancel.Font = new Font("Microsoft YaHei UI", 9f);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 150);
            cancel.FlatAppearance.BorderSize = 1;
            cancel.BackColor = Color.FromArgb(22, 28, 48);
            cancel.ForeColor = Color.FromArgb(225, 232, 248);
            cancel.Location = new Point(W - 202, 134);
            cancel.Size = new Size(88, 30);
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            ok = new Button();
            ok.Text = "连接";
            ok.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            ok.FlatStyle = FlatStyle.Flat;
            ok.FlatAppearance.BorderSize = 0;
            ok.BackColor = WidgetPaint.PaletteFor(AppShell.CurrentTheme).Accent;
            ok.ForeColor = Color.FromArgb(16, 20, 34);
            ok.Location = new Point(W - 106, 134);
            ok.Size = new Size(86, 30);
            ok.Click += delegate { DoConnect(); };
            Controls.Add(ok);

            AcceptButton = ok;
            CancelButton = cancel;
            ActiveControl = box;
        }

        protected override bool ProcessDialogKey(Keys k)   // Esc closes even with CancelButton focus quirks
        {
            if (k == Keys.Escape) { Close(); return true; }
            return base.ProcessDialogKey(k);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush lgb = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(238, 30, 38, 62), Color.FromArgb(248, 12, 15, 30), 90f))
                g.FillPath(lgb, WidgetPaint.RoundRect(0, 0, Width, Height, 16));
            using (Pen pn = new Pen(Color.FromArgb(42, 105, 160, 255), 1.1f))
                g.DrawPath(pn, WidgetPaint.RoundRect(0, 0, Width - 1, Height - 1, 16));
            base.OnPaint(e);
        }

        void DoConnect()
        {
            if (busy) return;
            string pwd = box.Text;
            if (pwd.Length < 8) { status.ForeColor = Color.FromArgb(255, 170, 120); status.Text = "密码至少 8 位"; return; }
            busy = true;
            ok.Enabled = cancel.Enabled = box.Enabled = false;
            status.ForeColor = Color.FromArgb(190, 208, 224);
            status.Text = "正在连接「" + ssid + "」…";
            Program.Log("[NET] connect+password " + ssid);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = NetCtl.Connect(ssid, pwd);
                bool okR = err == null && NetCtl.WaitConnected(ssid, 20000);
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed) return;
                        if (okR)
                        {
                            Program.Log("[NET] connect+password " + ssid + " -> OK");
                            Close();
                        }
                        else
                        {
                            Program.Log("[NET] connect+password " + ssid + " -> FAIL " + (err ?? "timeout"));
                            busy = false;
                            ok.Enabled = cancel.Enabled = box.Enabled = true;
                            ActiveControl = box;
                            box.SelectAll();
                            status.ForeColor = Color.FromArgb(255, 140, 140);
                            status.Text = "连接失败：密码可能不对，或信号太弱";
                        }
                    });
                }
                catch { }   // dialog dismissed while the connect was in flight
            });
        }
    }

    // ============ Mac-style dock (v5: running apps, hard taskbar hide, 60fps spring) ============
    class DockForm : Form
    {
        class DockItem
        {
            public string Label; public string Path; public string ExeName;
            public Bitmap Icon;            // 128px master
            public bool IsLink; public bool Running;
            public bool IsExtra;           // auto-detected running app (not pinned)
            public bool IsGear;            // the settings entry pinned at the end
            public bool IsSearch;          // the app-search entry next to the gear
            public bool IsDesk;            // the 桌面 (desktop) entry at the front
            public bool IsTray;            // discovered via the notification area (tray-only resident)
            public bool IsBuiltin;         // hardcoded pinned entry (文件资源管理器 / Edge)
            public int LaunchPid;
            public IntPtr Win;             // main window for extras
            public float Scale = 1f, Vel = 0f;
            public float Cx;               // current center x in form coords
            public float RestCx;           // resting center x (stable magnification reference)
            public double BounceT0 = -99;
        }

        [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IImageList
        {
            void Add(); void ReplaceIcon(); void SetOverlayImage(); void Replace();
            void AddMasked(); void Draw_(); void Remove();
            void GetIcon(int i, int flags, out IntPtr hIcon);
        }

        // common app alias: dock label -> possible process names (Chinese launchers vs real exe names)
        static readonly Dictionary<string, string[]> Alias = new Dictionary<string, string[]>
        {
            { "微信", new[] { "WeChat", "Weixin", "WeChatAppEx" } },
            { "QQ", new[] { "QQ" } },
            { "网易云音乐", new[] { "cloudmusic" } },
            { "腾讯会议", new[] { "wemeetapp" } },
            { "Typora", new[] { "Typora" } },
            { "Notion", new[] { "Notion" } },
            { "百度网盘同步空间", new[] { "BaiduNetdisk", "BaiduNetdiskHost" } },
            { "ChatWise", new[] { "ChatWise" } },
            { "雷神加速器", new[] { "leigod", "ThunderAccelerator" } },
            { "文件资源管理器", new[] { "explorer" } }
        };
        static readonly HashSet<string> RunBlacklist = new HashSet<string>
        {
            "explorer", "applicationframehost", "msedgewebview2", "searchhost", "textinputhost",
            "startmenuexperiencehost", "shellexperiencehost", "systemsettings", "techrainwallpaper", "zpapaer",
            "dwm", "sihost", "ctfmon", "chsime", "widgetservice", "widgets"
        };

        // ══════════════════════════════════════════════════════════════════
        // USER DIRECTIVE (2026-09-24, 用户两次强调，最高优先级设计原则):
        //   dock 图标唤醒 = 能直连恢复的绝不走托盘舞步！
        //   托盘路径慢（数秒级）且 bug 多。托盘路径只允许两类：
        //     a) TrayDoctrine - 外部恢复会变输入死寂僵尸的 Electron/Qt 壳
        //        （QQ/微信/ZCode，实测结论，见 dock-tray-app-activation-fixes）
        //     b) 未来实测确认"直连必然产生死窗"的新应用，须逐个加入并注明证据
        //   其余一律直连恢复隐藏窗口。禁止扩大托盘路径的适用范围。
        // ══════════════════════════════════════════════════════════════════
        // The tray-button dance when fully tray-hidden is REQUIRED for apps whose
        // input pipeline dies on external restore (QQ/WeChat/ZCode - all Electron/
        // Qt shells that resurface as input-dead ghosts). Other apps restore fine
        // directly and skip the dance (it costs seconds of surfacing lag).
        static readonly HashSet<string> TrayDoctrine = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "QQ", "微信", "WeChat", "Weixin", "ZCode"
        };

        // force-direct list: beats TrayResidents/IsTray. 网易云音乐 was flipped back
        // to the tray dance by the tray-resident override once already (4s+ surfacing,
        // 2026-09-24) - its hidden windows restore perfectly directly.
        // Chrome added 2026-09-25 (user directive): same treatment.
        static readonly HashSet<string> DirectRestore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "网易云音乐", "cloudmusic",
            "Google Chrome", "Chrome", "chrome", "Google"
        };

        List<DockItem> pinned = new List<DockItem>();
        List<DockItem> extras = new List<DockItem>();
        List<DockItem> display = new List<DockItem>();

        // exe names (lowercase) the user chose to keep out of the dock's auto list;
        // dockApps/dockHidden.txt, one per line, '#' comments - survives restarts
        static HashSet<string> HiddenNames = new HashSet<string>();
        // exe names currently holding a notification-area icon (updated per scan).
        // Pinned copies of these apps also wake through the tray-click path: a
        // tray-resident app that was hidden by ITS OWN hide-to-tray must be woken
        // by clicking its tray icon, exactly like the doctrine apps.
        static HashSet<string> TrayResidents = new HashSet<string>();
        // exe names (lowercase) owning at least one titled top-level window, ANY
        // visibility - refreshed per scan. The running-dot rule uses this: a
        // background-mode process with no windows (chrome) must not light the dot.
        static HashSet<string> TitledExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void LoadHidden()
        {
            try
            {
                HashSet<string> hs = new HashSet<string>();
                foreach (string ln in File.ReadAllLines(Path.Combine(dockDirStatic, "dockHidden.txt")))
                {
                    string n = ln.Trim().ToLower();
                    if (n.Length > 0 && !n.StartsWith("#")) hs.Add(n);
                }
                HiddenNames = hs;
            }
            catch { }
        }

        static void HideApp(string exeName)
        {
            try
            {
                string n = (exeName ?? "").Trim().ToLower();
                if (n.Length == 0 || HiddenNames.Contains(n)) return;   // no duplicate lines from repeated clicks
                File.AppendAllText(Path.Combine(dockDirStatic, "dockHidden.txt"), n + "\r\n");
                LoadHidden();
                Program.Log("[DOCK] hidden: " + n);
            }
            catch { }
        }

        // labels of built-in pinned entries (文件资源管理器 / Edge 浏览器) the user
        // removed; dockApps/dockRemoved.txt, one per line. Built-ins have no .lnk
        // to delete, so removal is remembered here instead (delete the line to restore)
        static HashSet<string> RemovedBuiltins = new HashSet<string>();

        static void LoadRemoved()
        {
            try
            {
                HashSet<string> hs = new HashSet<string>();
                foreach (string ln in File.ReadAllLines(Path.Combine(dockDirStatic, "dockRemoved.txt")))
                {
                    string n = ln.Trim();
                    if (n.Length > 0 && !n.StartsWith("#")) hs.Add(n);
                }
                RemovedBuiltins = hs;
            }
            catch { }
        }

        static void RemoveBuiltin(string label)
        {
            try
            {
                string n = (label ?? "").Trim();
                if (n.Length == 0 || RemovedBuiltins.Contains(n)) return;
                File.AppendAllText(Path.Combine(dockDirStatic, "dockRemoved.txt"), n + "\r\n");
                LoadRemoved();
                Program.Log("[DOCK] removed builtin: " + n);
            }
            catch { }
        }

        // ---------- drag-to-reorder ----------
        // icon order persisted in dockApps/dockOrder.txt: pinned section by icon
        // label, running section by exe name; apps not listed keep their natural
        // order after the listed ones. Rewritten on every drop.
        static List<string> DockOrder = new List<string>();

        static void LoadOrder()
        {
            try
            {
                List<string> L = new List<string>();
                foreach (string ln in File.ReadAllLines(Path.Combine(dockDirStatic, "dockOrder.txt")))
                {
                    string n = ln.Trim().ToLower();
                    if (n.Length > 0 && !n.StartsWith("#")) L.Add(n);
                }
                DockOrder = L;
            }
            catch { }
        }

        static int OrderIndex(string name)
        {
            string n = (name ?? "").Trim().ToLower();
            for (int i = 0; i < DockOrder.Count; i++) if (DockOrder[i] == n) return i;
            return 1 << 30;
        }

        // stable sort by (dockOrder position, natural index) - decorate-sort-undecorate,
        // List.Sort alone is unstable and would shuffle unlisted items on every reload
        static List<DockItem> SortByOrder(List<DockItem> items)
        {
            List<KeyValuePair<DockItem, int>> decorated = new List<KeyValuePair<DockItem, int>>();
            for (int i = 0; i < items.Count; i++) decorated.Add(new KeyValuePair<DockItem, int>(items[i], i));
            decorated.Sort(delegate(KeyValuePair<DockItem, int> a, KeyValuePair<DockItem, int> b)
            {
                int oa = OrderIndex(a.Key.Label), ob = OrderIndex(b.Key.Label);
                if (oa != ob) return oa.CompareTo(ob);
                return a.Value.CompareTo(b.Value);
            });
            List<DockItem> r = new List<DockItem>();
            foreach (KeyValuePair<DockItem, int> kv in decorated) r.Add(kv.Key);
            return r;
        }

        void SaveOrder()
        {
            try
            {
                List<string> L = new List<string>();
                foreach (DockItem p in pinned) L.Add(p.Label.ToLower());
                foreach (DockItem e in extras) L.Add(e.ExeName.ToLower());
                DockOrder = L;
                List<string> lines = new List<string>();
                lines.Add("# dock 图标顺序（拖动图标自动维护；每行一个名称，# 为注释）");
                lines.Add("# 前段=钉住区按图标名，后段=运行区按 exe 名；未列出的按默认顺序排后");
                foreach (string s in L) lines.Add(s);
                File.WriteAllLines(Path.Combine(dockDirStatic, "dockOrder.txt"), lines.ToArray());
                Program.Log("[DOCK] order saved: " + string.Join(";", L.ToArray()));
            }
            catch { }
        }

        // drag state: arm (button down on an icon) -> drag (moved past threshold).
        // The dragged item is tracked by REFERENCE, not index: a scan rebuild can
        // shift display[] between arm and drop (the index is re-resolved per frame)
        bool dragArm;
        bool dragging;
        DockItem dragItem;
        int dragIdx = -1;        // display index of the dragged item (per-frame re-resolved)
        int dragInsert = -1;     // display slot it will occupy if dropped now
        int dragStartX;
        bool dragConsumedClick;  // swallow the click that ends a drag

        static Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();
        float mouseX = -9999;
        // animation heartbeat: WinForms Timer = WM_TIMER, the lowest-priority queue
        // event - it starves the moment the foreground app renders and the springs
        // visibly stutter under the mouse. A winmm periodic timer posts a real
        // queued message (WM_APP+7) that WndProc renders from; posted messages keep
        // their cadence under load.
        [DllImport("winmm.dll")] static extern int timeSetEvent(uint delayMs, uint res, TimeProc proc, IntPtr user, uint flags);
        [DllImport("winmm.dll")] static extern int timeKillEvent(int id);
        delegate void TimeProc(uint id, uint msg, IntPtr user, IntPtr dw1, IntPtr dw2);
        const int WM_APP_RENDER = 0x8000 + 7;
        TimeProc animProc;
        int animMmId;
        int renderInFlight;
        int hbTick;                     // heartbeat tick counter (parity for slow mode)
        volatile int dockSlowDiv = 1;   // 1 = full rate, 2 = half rate (weak machine)
        int profN;
        double profDrawSum, profPresentSum, profPresentMax, profWinStart;
        Timer housekeeping = new Timer();
        Timer fsWatch = new Timer();      // fast fullscreen watch (250ms reaction)
        Stopwatch sw = Stopwatch.StartNew();
        double lastT, activeUntil;
        int hoverIdx = -1;
        string dockDir;
        string lastFileSig = "";
        static bool taskbarHidden = false;
        Bitmap shadowSpr;
        Bitmap buf;
        int sepIndex = -1;              // separator position inside display (-1 none)

        const int MASTER = 128;
        const int BASE = 40;              // resting icon size (smaller, per user preference)
        const int GAP = 11;
        const int PANELH = 58;
        const int FORMH = 140;
        const int BOTTOMM = 8;
        const float AMP = 0.78f;
        const float SIGMA = 56f;
        const float STIFF = 190f;       // omega ~ 13.8 rad/s
        const float DAMP = 16f;         // zeta ~ 0.58: clearly visible overshoot bounce
        volatile bool scanning;         // app-scan worker busy flag
        DateTime edgeSince = DateTime.MinValue;
        bool edgeSummoned;              // dock summoned by the bottom-edge hug while fullscreen
        int dockAlpha = 255;            // 0-255 whole-window opacity (fade knob)
        Timer fadeTimer;
        bool fadeToVisible;

        // summon/hide with a short fade instead of popping: the fade timer just
        // moves dockAlpha and re-presents; Present() applies it per frame
        void FadeIn()
        {
            if (Visible && dockAlpha >= 255) return;
            fadeToVisible = true;
            if (!Visible) { dockAlpha = 0; Render(); Show(); }
            StartFade();
        }

        void FadeOut()
        {
            if (!Visible) return;
            fadeToVisible = false;
            StartFade();
        }

        void StartFade()
        {
            if (fadeTimer == null)
            {
                fadeTimer = new Timer();
                fadeTimer.Interval = 30;
                fadeTimer.Tick += delegate
                {
                    int target = fadeToVisible ? 255 : 0;
                    if (dockAlpha < target) dockAlpha = Math.Min(target, dockAlpha + 38);
                    else if (dockAlpha > target) dockAlpha = Math.Max(target, dockAlpha - 38);
                    try { Render(); } catch { }
                    if (dockAlpha == target)
                    {
                        fadeTimer.Stop();
                        if (!fadeToVisible) Hide();
                    }
                };
            }
            fadeTimer.Start();
        }

        // fullscreen hides the dock; hugging the screen's very bottom edge for
        // 0.5s summons it, and moving the mouse off the dock band hides it again
        void EdgeSummonTick()
        {
            Rectangle b = Screen.PrimaryScreen.Bounds;
            if (!Visible)
            {
                if (Cursor.Position.Y < b.Bottom - 3) { edgeSince = DateTime.MinValue; return; }
                if (edgeSince == DateTime.MinValue) edgeSince = DateTime.Now;
                if ((DateTime.Now - edgeSince).TotalMilliseconds < 500) return;
                edgeSince = DateTime.MinValue;
                edgeSummoned = true;
                activeUntil = sw.Elapsed.TotalSeconds + 1.4;   // full-rate rendering right away
                FadeIn();
                return;
            }
            // summoned: mouse leaves the dock band -> fade out (well within 1s)
            if (edgeSummoned && Cursor.Position.Y < b.Bottom - (FORMH + BOTTOMM + 30))
            {
                edgeSummoned = false;
                FadeOut();
            }
        }

        public DockForm()
        {
            Ui = this;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            MakeShadow();
            dockDir = Path.Combine(Program.BaseDir, "dockApps");
            dockDirStatic = dockDir;
            try { Directory.CreateDirectory(dockDir); } catch { }
            ReloadItems();
            RebuildDisplay();
            Relayout();
            IntPtr hh = Handle;
            Render();

            animProc = new TimeProc(AnimHeartbeat);
            animMmId = timeSetEvent(15, 1, animProc, IntPtr.Zero, 1);   // TIME_PERIODIC ~66fps

            AppCache.Start();   // background app index for the launcher (60s incremental)

            // fast reaction (<=0.5s): hide the dock the moment a window covers the
            // screen (maximized counts) and bring it back the moment it does not
            fsWatch.Interval = 250;
            fsWatch.Tick += delegate
            {
                try
                {
                    IntPtr fg;
                    bool fs = Program.ForegroundIsFullscreen(Handle, out fg);
                    if (fs)
                    {
                        if (Visible && !edgeSummoned) FadeOut();
                        EdgeSummonTick();
                    }
                    else
                    {
                        edgeSummoned = false;
                        edgeSince = DateTime.MinValue;
                        if (!Visible || dockAlpha < 255) FadeIn();
                    }
                }
                catch { }
            };
            fsWatch.Start();

            housekeeping.Interval = 2000;
            housekeeping.Tick += delegate
            {
                // app scan + icon loads run on a worker so the UI never blocks
                if (!scanning)
                {
                    scanning = true;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try { RefreshAppsWorker(); }
                        catch { }
                        finally { scanning = false; }
                    });
                }
                if (FileSig() != lastFileSig && !dragging) { ReloadItems(); RebuildDisplay(); Relayout(); }
                if (!AppShell.TempShowTaskbar) ReassertTaskbar();
                activeUntil = Math.Max(activeUntil, sw.Elapsed.TotalSeconds + 0.4);  // poll wake
            };
            housekeeping.Start();

            HideTaskbar();
        }

        void MakeShadow()
        {
            int w = 96, h = 26;
            shadowSpr = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(shadowSpr))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(0, 0, w, h);
                    using (PathGradientBrush pg = new PathGradientBrush(p))
                    {
                        pg.CenterColor = Color.FromArgb(120, 0, 0, 0);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 0, 0, 0) };
                        g.FillPath(pg, p);
                    }
                }
            }
        }

        string FileSig()
        {
            try
            {
                string[] f = Directory.GetFiles(dockDir, "*.lnk");
                Array.Sort(f);
                return string.Join("|", f);
            }
            catch { return ""; }
        }

        void ReloadItems()
        {
            LoadHidden();
            LoadRemoved();
            LoadOrder();
            pinned.Clear();
            Action<string, string> addBuiltin = delegate(string exe, string label)
            {
                if (RemovedBuiltins.Contains(label)) return;   // user removed this built-in
                if (File.Exists(exe)) pinned.Add(new DockItem { Label = label, Path = exe, ExeName = Path.GetFileNameWithoutExtension(exe), Icon = LoadBest(exe), IsLink = false, IsBuiltin = true });
            };
            addBuiltin(Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\explorer.exe", "文件资源管理器");
            addBuiltin("C:\\Windows\\explorer.exe", "文件资源管理器");   // real home - the System32 path never exists
            addBuiltin("C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", "Edge 浏览器");
            try
            {
                string[] files = Directory.GetFiles(dockDir, "*.lnk");
                Array.Sort(files);
                foreach (string f in files)
                {
                    string label = Path.GetFileNameWithoutExtension(f);
                    string target = ResolveLnk(f);
                    // NEW-USER fallback: dockApps lnks ship with the dev machine's
                    // absolute targets. On a fresh machine they are dead → no icon,
                    // no launch. Re-resolve by label from the Start Menu / App Paths
                    // and self-heal the shipped lnk so later starts resolve directly.
                    if (target.Length == 0 || !File.Exists(target))
                    {
                        string re = ResolveDeadLnk(label);
                        if (re != null)
                        {
                            target = re;
                            try
                            {
                                Type t2 = Type.GetTypeFromProgID("WScript.Shell");
                                object ws2 = Activator.CreateInstance(t2);
                                object sc2 = t2.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, ws2, new object[] { f });
                                sc2.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc2, new object[] { target });
                                sc2.GetType().InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc2, new object[] { Path.GetDirectoryName(target) });
                                sc2.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc2, null);
                            }
                            catch { }
                        }
                    }
                    Bitmap ic = null;
                    bool targetAlive = target.Length > 0 && File.Exists(target);
                    if (targetAlive) ic = LoadBest(target);
                    // a DEAD lnk's shell icon is a generic blank-file glyph that reads
                    // as "unrendered" at dock size - try the START MENU lnk's own icon
                    // (the real app icon) before falling back to the letter tile
                    if (ic == null && targetAlive) ic = LoadBest(f);
                    if (ic == null)
                    {
                        string smlnk = FindStartMenuLnk(label) ?? FindStartMenuLnkFuzzy(label);
                        if (smlnk != null) ic = LoadBest(smlnk);
                        Program.Log(string.Format("[DOCK] lnk '{0}': target='{1}' alive={2} startmenu={3} icon={4}",
                            label, target, targetAlive, smlnk ?? "none", ic != null ? "resolved" : "letter-tile"));
                    }
                    if (ic == null) ic = LetterTile(label);   // never render a blank tile
                    pinned.Add(new DockItem
                    {
                        Label = label,
                        Path = f,
                        ExeName = target.Length > 0 ? Path.GetFileNameWithoutExtension(target) : "",
                        Icon = ic,
                        IsLink = true
                    });
                }
            }
            catch { }
            pinned = SortByOrder(pinned);   // user drag-order from dockOrder.txt
            lastFileSig = FileSig();
        }

        // merge pinned + auto-detected running apps into the render list
        void RebuildDisplay()
        {
            // NOTE: must NOT touch Running here — running state is owned by the worker
            // scan; resetting it here made clicks fall into the "launch new instance"
            // branch and pop up login dialogs.
            display.Clear();
            if (deskItem == null)
                deskItem = new DockItem { Label = "桌面", IsDesk = true, ExeName = "" };
            deskItem.Scale = 1; deskItem.Vel = 0;
            display.Add(deskItem);
            foreach (DockItem p in pinned)
            {
                p.Scale = 1; p.Vel = 0;
                display.Add(p);
            }
            sepIndex = -1;
            if (extras.Count > 0)
            {
                sepIndex = display.Count;   // separator occupies a slot
                foreach (DockItem e in extras) { e.Scale = 1; e.Vel = 0; display.Add(e); }
            }
            // app-search entry next to the gear, settings gear always at the end
            if (searchItem == null)
                searchItem = new DockItem { Label = "搜索应用", IsSearch = true, ExeName = "" };
            searchItem.Scale = 1; searchItem.Vel = 0;
            display.Add(searchItem);
            if (gearItem == null)
                gearItem = new DockItem { Label = "设置菜单", IsGear = true, ExeName = "" };
            gearItem.Scale = 1; gearItem.Vel = 0;
            display.Add(gearItem);
        }
        static DockForm Ui;    // for BeginInvoke/chooser from the static activation path
        DockItem gearItem;
        DockItem searchItem;
        DockItem deskItem;

        // ---------- auto-detect running apps (worker thread; UI parts marshalled back) ----------
        void RefreshApps()   // fire-and-forget from UI
        {
            if (scanning) return;
            scanning = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { RefreshAppsWorker(); }
                catch { }
                finally { scanning = false; }
            });
        }

        int scanCount = 0;
        string lastTraySig = "";
        void RefreshAppsWorker()
        {
            try
            {
                int dbg = RefreshAppsScan();
                if (scanCount < 3 || scanCount % 30 == 0) Program.Log("[DOCK] scan#" + scanCount + " found=" + dbg);
                scanCount++;
            }
            catch (Exception ex) { Program.Log("[DOCK] scan EXCEPTION " + ex); }
        }

        int RefreshAppsScan()
        {
            Dictionary<string, DockItem> found = new Dictionary<string, DockItem>();
            uint selfPid;
            Program.GetWindowThreadProcessId(Handle, out selfPid);
            int cloakErrLog = 0;
            int fVis = 0, fTitle = 0, fTool = 0, fNoact = 0, fCloak = 0, fSelf = 0, fExe = 0, fBl = 0;
            Dictionary<string, bool> titled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Program.GLEnumProc cb = delegate(IntPtr h, IntPtr l)
            {
                try
                {
                // record titled windows across ALL visibilities first - the
                // running-dot rule needs to know about tray-minimized mains too.
                // SKIP IME/system ghost windows (Default IME, MSCTFIME UI, HintWnd,
                // *_TSF_UI - they attach to every app's threads, carry titles, and
                // would light the dot for apps with no real window) and cloaked
                // (DWM-hidden) surfaces.
                uint wpid0;
                Program.GetWindowThreadProcessId(h, out wpid0);
                if (wpid0 != selfPid && wpid0 != 0 && Program.GetWindowTextLength(h) > 0)
                {
                    StringBuilder wclsb = new StringBuilder(64);
                    Program.GetClassName(h, wclsb, 64);
                    string wcls = wclsb.ToString();
                    StringBuilder wttlb = new StringBuilder(128);
                    Program.GetWindowText(h, wttlb, 128);
                    string wtitle = wttlb.ToString();
                    // class OR title must both be checked: chrome's "HintWnd" helper
                    // carries chrome's own class but a ghost title
                    bool ghost = wcls == "Default IME" || wcls == "MSCTFIME UI" || wcls.EndsWith("TSF_UI") || wcls.EndsWith("IME")
                              || wtitle == "Default IME" || wtitle == "MSCTFIME UI" || wtitle == "HintWnd" || wtitle.EndsWith("TSF_UI");
                    if (!ghost)
                    {
                        int wcloak = 0;
                        try { Program.DwmGetWindowAttribute(h, 14, out wcloak, 4); } catch { }
                        if (wcloak == 0)
                        {
                            string wexe = ExeOfPid(wpid0);
                            if (wexe != null && wexe.Length > 0)
                                titled[Path.GetFileNameWithoutExtension(wexe).ToLower()] = true;
                        }
                    }
                }
                if (!Program.IsWindowVisible(h)) return true;
                    fVis++;
                    if (Program.GetWindowTextLength(h) == 0) return true;
                    fTitle++;
                    long ex = (long)Program.GetWindowLongPtr(h, -20);
                    if ((ex & 0x80) != 0) return true;                 // WS_EX_TOOLWINDOW
                    fTool++;
                    if ((ex & 0x08000000) != 0 && (ex & 0x00010000) == 0) return true; // skip noactivate shell bits
                    fNoact++;
                    int cloaked;
                    try
                    {
                        if (Program.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
                    }
                    catch (Exception dex)
                    {
                        if (cloakErrLog++ == 0) Program.Log("[DOCK] cloak ERR " + dex.GetType().Name + ": " + dex.Message);
                    }
                    fCloak++;
                    uint pid;
                    Program.GetWindowThreadProcessId(h, out pid);
                    if (pid == selfPid || pid == 0) return true;
                    fSelf++;
                    string exe = ExeOfPid(pid);
                    if (exe == null || exe.Length == 0) return true;
                    fExe++;
                    string name = Path.GetFileNameWithoutExtension(exe).ToLower();
                    if (RunBlacklist.Contains(name)) return true;
                    if (HiddenNames.Contains(name)) return true;   // user-hidden apps never enter the auto list
                    fBl++;
                    if (!found.ContainsKey(name))
                    {
                        DockItem it = new DockItem();
                        it.ExeName = Path.GetFileNameWithoutExtension(exe);
                        it.Label = it.ExeName;
                        it.Path = exe;
                        it.Running = true;
                        it.IsExtra = true;
                        it.Win = h;
                        it.Icon = CachedIcon(exe);
                        found[name] = it;
                    }
                    else
                    {
                        DockItem it = found[name];
                        Program.RECT r; Program.RECT r2;
                        if (Program.GetWindowRect(h, out r) && Program.GetWindowRect(it.Win, out r2))
                            if ((r.Right - r.Left) * (r.Bottom - r.Top) > (r2.Right - r2.Left) * (r2.Bottom - r2.Top)) it.Win = h;
                    }
                }
                catch { }
                return true;
            };
            Program.EnumWindows(cb, IntPtr.Zero);
            if (scanCount < 3)
                Program.Log(string.Format("[DOCK] funnel vis={0} titled={1} notool={2} nonoact={3} nocloak={4} notself={5} hasexe={6} notbl={7} found={8}",
                    fVis, fTitle, fTool, fNoact, fCloak, fSelf, fExe, fBl, found.Count));
            // tray-only residents (ToDesk/ZCode-style): they live purely in the
            // notification area, the visible-window scan above can never see them
            MergeTrayApps(found);
            TitledExes = new HashSet<string>(titled.Keys, StringComparer.OrdinalIgnoreCase);
            // mark pinned items running. USER DIRECTIVE 2026-09-25: the dot means
            // the app OWNS A REAL WINDOW - process existence alone must not light
            // it (chrome background mode keeps processes but no windows and the
            // dot lied). Doctrine/tray-resident apps are the exception: the tray
            // IS their home, so process presence = running.
            foreach (DockItem p in pinned)
            {
                string[] names;
                if (!Alias.TryGetValue(p.Label, out names)) names = new[] { p.ExeName };
                bool run = false;
                foreach (string n0 in names)
                {
                    string n = (n0 ?? "").ToLower();
                    if (n.Length == 0) continue;
                    if (found.ContainsKey(n) || TitledExes.Contains(n)) { run = true; break; }
                    bool resident = TrayDoctrine.Contains(p.Label) || TrayDoctrine.Contains(n0)
                                 || TrayResidents.Contains(n);
                    if (resident)
                    {
                        try { if (Process.GetProcessesByName(n0).Length > 0) { run = true; break; } }
                        catch { }
                    }
                }
                p.Running = run;
            }
            // extras = running apps that are not pinned
            List<DockItem> newExtras = new List<DockItem>();
            foreach (DockItem e in found.Values)
            {
                bool isPinned = false;
                foreach (DockItem p in pinned)
                {
                    string[] names;
                    if (!Alias.TryGetValue(p.Label, out names)) names = new[] { p.ExeName.ToLower() };
                    bool hit = false;
                    foreach (string n0 in names)
                    {
                        if ((n0 ?? "").ToLower() == e.ExeName.ToLower()) { hit = true; break; }
                    }
                    if (hit) { isPinned = true; break; }
                }
                if (!isPinned) newExtras.Add(e);
            }
            // tray-hidden residents must STAY in the dock: an extra whose visible
            // windows are gone (closed-to-tray, e.g. ZCode/Telegram) is kept while
            // its process still lives and its remembered window handle still exists
            List<DockItem> mergedExtras = new List<DockItem>(newExtras);
            foreach (DockItem old in extras)
            {
                // user-hidden apps must NOT be resurrected by this retention pass
                // even though their process still lives - that was the "点击不显示
                // 没反应" bug: the hidden filter dropped them, retention re-added
                if (HiddenNames.Contains(old.ExeName.ToLower())) continue;
                bool covered = false;
                foreach (DockItem n in newExtras) if (n.ExeName.ToLower() == old.ExeName.ToLower()) { covered = true; break; }
                if (covered) continue;
                bool alive = false;
                try { alive = old.Win != IntPtr.Zero && Program.IsWindow(old.Win) && Process.GetProcessesByName(old.ExeName).Length > 0; }
                catch { }
                if (alive) mergedExtras.Add(old);
            }
            newExtras = mergedExtras;
            // change detection must be z-order-independent: compare the SORTED set of
            // extra exe names (EnumWindows order shifts whenever window focus changes)
            List<string> extraNames = new List<string>();
            foreach (DockItem e in newExtras) extraNames.Add(e.ExeName.ToLower());
            extraNames.Sort();
            string extraSig = string.Join(";", extraNames.ToArray());
            List<string> curNames = new List<string>();
            foreach (DockItem e in extras) curNames.Add(e.ExeName.ToLower());
            curNames.Sort();
            string curSig = string.Join(";", curNames.ToArray());
            if (extraSig == curSig) return found.Count;
            Program.Log("[DOCK] extras now: " + extraSig);
            newExtras = SortByOrder(newExtras);   // user drag-order survives scans
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (dragging) return;   // mid-drag rebuild would yank the item; next scan reapplies
                    extras = newExtras;
                    RebuildDisplay();
                    Relayout();
                });
            }
            catch { }
            return found.Count;
        }

        // notification-area toolbars: overflow first, then the pinned strip.
        // Read-only: hidden toolbars still answer TB_* enumeration, so unlike
        // TrayIconClick this needs no show/hide dance.
        static List<IntPtr> TrayToolbars()
        {
            List<IntPtr> tbs = new List<IntPtr>();
            IntPtr ovWnd = Program.FindWindow("NotifyIconOverflowWindow", null);
            if (ovWnd != IntPtr.Zero)
            {
                // EP-classic overflow has the toolbar directly; stock Win10 via SysPager
                IntPtr t = Program.FindWindowEx(ovWnd, IntPtr.Zero, "ToolbarWindow32", null);
                if (t == IntPtr.Zero)
                {
                    IntPtr pg = Program.FindWindowEx(ovWnd, IntPtr.Zero, "SysPager", null);
                    if (pg != IntPtr.Zero) t = Program.FindWindowEx(pg, IntPtr.Zero, "ToolbarWindow32", null);
                }
                if (t != IntPtr.Zero) tbs.Add(t);
            }
            IntPtr tbwnd = Program.FindWindow("Shell_TrayWnd", null);
            if (tbwnd != IntPtr.Zero)
            {
                IntPtr tn = Program.FindWindowEx(tbwnd, IntPtr.Zero, "TrayNotifyWnd", null);
                if (tn != IntPtr.Zero)
                {
                    IntPtr pg = Program.FindWindowEx(tn, IntPtr.Zero, "SysPager", null);
                    IntPtr t = pg != IntPtr.Zero ? Program.FindWindowEx(pg, IntPtr.Zero, "ToolbarWindow32", null) : IntPtr.Zero;
                    if (t == IntPtr.Zero) t = Program.FindWindowEx(tn, IntPtr.Zero, "ToolbarWindow32", null);
                    if (t != IntPtr.Zero) tbs.Add(t);
                }
            }
            return tbs;
        }

        // apps living purely in the notification area (ToDesk, ZCode, ...) have no
        // visible window, so the EnumWindows scan never finds them. Enumerate the
        // tray toolbars and merge their owner processes into `found` instead. Owner
        // is read ONLY from offset 0 of the button's dwData record (the callback
        // HWND) - deeper slot scans match stale pointers and misattribute apps.
        void MergeTrayApps(Dictionary<string, DockItem> found)
        {
            List<IntPtr> tbs = TrayToolbars();
            List<string> names = new List<string>();
            int tErr = 0;
            int dbg = 0;   // first-scans per-button trace: localized the zcode drop
            foreach (IntPtr tb in tbs)
            {
                uint expPid;
                Program.GetWindowThreadProcessId(tb, out expPid);
                IntPtr proc = Program.OpenProcess(0x1F0FFF, false, expPid);
                if (proc == IntPtr.Zero) continue;
                try
                {
                    IntPtr remote = Program.VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x1000, 0x40);
                    if (remote == IntPtr.Zero) continue;
                    try
                    {
                        int count = (int)Program.SendMessage(tb, 0x400 + 24, IntPtr.Zero, IntPtr.Zero);        // TB_BUTTONCOUNT
                        byte[] buf = new byte[64]; IntPtr dummy;
                        for (int i = 0; i < count; i++)
                        {
                            try
                            {
                                if (Program.SendMessage(tb, 0x400 + 23, (IntPtr)i, remote) == IntPtr.Zero) continue;  // TB_GETBUTTON
                                if (!Program.ReadProcessMemory(proc, remote, buf, (UIntPtr)32, out dummy)) continue;
                                IntPtr dwData = (IntPtr)BitConverter.ToInt64(buf, 16);
                                if (dwData == IntPtr.Zero) continue;
                                byte[] dbuf = new byte[64];
                                if (!Program.ReadProcessMemory(proc, dwData, dbuf, (UIntPtr)64, out dummy)) continue;
                                IntPtr cbw = (IntPtr)BitConverter.ToInt64(dbuf, 0);   // callback HWND, offset 0 ONLY
                                if (cbw == IntPtr.Zero) continue;
                                uint wp;
                                Program.GetWindowThreadProcessId(cbw, out wp);
                                if (wp == 0 || wp == expPid) continue;   // battery/network icons belong to explorer itself
                                string exe = ExeOfPid(wp);
                                if (exe == null || exe.Length == 0) continue;   // denied = protected process, skip rather than misattribute
                                string baseName = Path.GetFileNameWithoutExtension(exe);
                                string name = baseName.ToLower();
                                if (dbg < 3)
                                    Program.Log("[TRAYDBG] btn#" + i + " hwnd=" + cbw + " pid=" + wp + " exe=" + baseName
                                        + " bl=" + RunBlacklist.Contains(name) + " hid=" + HiddenNames.Contains(name)
                                        + " inFound=" + found.ContainsKey(name));
                                if (name.Length == 0 || RunBlacklist.Contains(name) || HiddenNames.Contains(name) || found.ContainsKey(name) || names.Contains(name)) continue;
                                // a tray button whose app is PINNED must not merge as a
                                // tray extra: the pinned item already represents it, and
                                // the merge forced Running=true forever (chrome background
                                // mode kept its dot lit with zero windows - user bug report)
                                bool pinnedApp = false;
                                foreach (DockItem pp in pinned)
                                {
                                    if (string.Equals(pp.ExeName, baseName, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(pp.Label, baseName, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals((pp.ExeName ?? "").ToLower(), name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        pinnedApp = true;
                                        break;
                                    }
                                }
                                if (pinnedApp) continue;
                                found[name] = new DockItem
                                {
                                    ExeName = baseName,
                                    Label = baseName,
                                    Path = exe,
                                    Running = true,
                                    IsExtra = true,
                                    IsTray = true,
                                    Win = cbw,
                                    Icon = CachedIcon(exe)
                                };
                                names.Add(name);
                            }
                            catch (Exception tex)
                            {
                                // one-time-per-scan report: a bare catch here once turned a
                                // systemic P/Invoke error into the dock "silently seeing nothing"
                                if (tErr++ == 0) Program.Log("[TRAY] enum ERR " + tex.GetType().Name + ": " + tex.Message);
                            }
                        }
                    }
                    finally { Program.VirtualFreeEx(proc, remote, UIntPtr.Zero, 0x8000); }
                }
                finally { Program.CloseHandle(proc); }
            }
            names.Sort();
            TrayResidents = new HashSet<string>(names);
            dbg++;
            string sig = string.Join(";", names.ToArray());
            if (sig != lastTraySig)
            {
                lastTraySig = sig;
                Program.Log("[TRAY] tray-only residents: " + (sig.Length > 0 ? sig : "(none)"));
            }
        }

        static string ExeOfPid(uint pid)
        {
            try
            {
                IntPtr h = Program.OpenProcess(0x1000, false, pid);
                if (h == IntPtr.Zero) return null;
                try
                {
                    StringBuilder sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (Program.QueryFullProcessImageName(h, 0, sb, ref size)) return sb.ToString(0, size);
                    return null;
                }
                finally { Program.CloseHandle(h); }
            }
            catch { return null; }
        }

        static Bitmap CachedIcon(string exePath)
        {
            Bitmap b;
            if (iconCache.TryGetValue(exePath, out b)) return b;
            b = LoadBest(exePath);
            iconCache[exePath] = b;
            return b;
        }

        // NEW-USER support: a shipped dockApps lnk points at the dev machine's
        // absolute target. Re-resolve the app by its dock label so the icon and
        // the launch both work on a fresh machine.
        static string ResolveDeadLnk(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            // try the dock label plus its known exe aliases ("网易云音乐" -> cloudmusic)
            List<string> cands = new List<string>();
            cands.Add(label);
            string[] al;
            if (Alias.TryGetValue(label, out al)) cands.AddRange(al);
            foreach (string cand in cands)
            {
                string lnk = FindStartMenuLnk(cand);
                if (lnk != null)
                {
                    string t = ResolveLnk(lnk);
                    if (t.Length > 0 && File.Exists(t)) return t;
                }
                string ap = AppPathsLookup(cand);
                if (ap != null) return ap;
            }
            // fuzzy pass: any start-menu lnk whose name contains the label (or vice versa)
            foreach (string cand in cands)
            {
                if (cand.Length < 2) continue;
                string lnk = FindStartMenuLnkFuzzy(cand);
                if (lnk != null)
                {
                    string t = ResolveLnk(lnk);
                    if (t.Length > 0 && File.Exists(t)) return t;
                }
            }
            return null;
        }

        static string FindStartMenuLnk(string label)
        {
            string want = label + ".lnk";
            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                string hit = ScanLnkDir(root, want, false);
                if (hit != null) return hit;
            }
            return null;
        }

        static string FindStartMenuLnkFuzzy(string label)
        {
            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                string hit = ScanLnkDir(root, label, true);
                if (hit != null) return hit;
            }
            return null;
        }

        static string ScanLnkDir(string dir, string want, bool fuzzy)
        {
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*.lnk"))
                {
                    string fn = Path.GetFileNameWithoutExtension(f);
                    if (string.Equals(fn, want, StringComparison.OrdinalIgnoreCase)) return f;
                    if (fuzzy && (fn.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0
                               || want.IndexOf(fn, StringComparison.OrdinalIgnoreCase) >= 0)) return f;
                }
                foreach (string d in Directory.GetDirectories(dir))
                {
                    string hit = ScanLnkDir(d, want, fuzzy);
                    if (hit != null) return hit;
                }
            }
            catch { }
            return null;
        }

        static string AppPathsLookup(string label)
        {
            try
            {
                string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + label + ".exe";
                Microsoft.Win32.RegistryKey[] roots = { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser };
                foreach (Microsoft.Win32.RegistryKey root in roots)
                {
                    using (Microsoft.Win32.RegistryKey k = root.OpenSubKey(key))
                    {
                        if (k == null) continue;
                        string exe = k.GetValue(null) as string;
                        if (exe != null && exe.Length > 0 && File.Exists(exe)) return exe;
                    }
                }
            }
            catch { }
            return null;
        }

        // last-resort tile so a pinned app never renders as a blank square:
        // rounded blue-gradient card + the label's first letter in white
        static Bitmap LetterTile(string label)
        {
            string ch = string.IsNullOrEmpty(label) ? "?" : label.Substring(0, 1).ToUpper();
            Bitmap m = new Bitmap(MASTER, MASTER, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(m))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = WidgetPaint.RoundRect(0, 0, MASTER - 1, MASTER - 1, (int)(MASTER * 0.22f)))
                using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, MASTER, MASTER),
                    Color.FromArgb(255, 99, 181, 255), Color.FromArgb(255, 23, 90, 208), 90f))
                    g.FillPath(lg, p);
                using (Font f = new Font("Segoe UI", MASTER * 0.46f, FontStyle.Bold))
                {
                    SizeF sz = g.MeasureString(ch, f);
                    using (SolidBrush wb = new SolidBrush(Color.White))
                        g.DrawString(ch, f, wb, (MASTER - sz.Width) / 2f, (MASTER - sz.Height) / 2f);
                }
            }
            return m;
        }

        static string ResolveLnk(string lnk)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object ws = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, ws, new object[] { lnk });
                string target = (string)sc.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, sc, null);
                return target == null ? "" : target;
            }
            catch { return ""; }
        }

        static void MakeShortcut(string exePath)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(exePath);
                string lnk = Path.Combine(dockDirStatic, name + ".lnk");
                if (File.Exists(lnk)) return;
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object ws = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, ws, new object[] { lnk });
                sc.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { exePath });
                sc.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }
        static string dockDirStatic = "";

        // 128px master icon from shell jumbo cache (256px) with fallbacks
        internal static Bitmap LoadBest(string path)
        {
            Bitmap b = null;
            try
            {
                Program.SHFILEINFO fi = new Program.SHFILEINFO();
                IntPtr r = Program.SHGetFileInfo(path, 0, ref fi, (uint)Marshal.SizeOf(typeof(Program.SHFILEINFO)), 0x4000);
                if (r != IntPtr.Zero && fi.iIcon >= 0)
                {
                    int[] lists = { 4, 2 };
                    for (int k = 0; k < lists.Length && b == null; k++)
                    {
                        object o = null;
                        try
                        {
                            Guid iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                            if (Program.SHGetImageList(lists[k], ref iid, out o) == 0)
                            {
                                IntPtr hIco;
                                ((IImageList)o).GetIcon(fi.iIcon, 1, out hIco);
                                if (hIco != IntPtr.Zero)
                                {
                                    using (Icon ic = (Icon)Icon.FromHandle(hIco).Clone())
                                        b = MasterFrom(ic.ToBitmap());
                                    Program.DestroyIcon(hIco);
                                }
                            }
                        }
                        catch { }
                        finally { if (o != null) try { Marshal.ReleaseComObject(o); } catch { } }
                    }
                }
            }
            catch { }
            if (b == null)
            {
                try { using (Icon ic = Icon.ExtractAssociatedIcon(path)) b = MasterFrom(ic.ToBitmap()); }
                catch { }
            }
            return b;
        }

        static Bitmap MasterFrom(Bitmap src)
        {
            if (src == null) return null;
            Bitmap m = new Bitmap(MASTER, MASTER, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(m))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int s = Math.Min(src.Width, src.Height);
                g.DrawImage(src, new Rectangle(0, 0, MASTER, MASTER),
                    new Rectangle((src.Width - s) / 2, (src.Height - s) / 2, s, s), GraphicsUnit.Pixel);
            }
            return m;
        }

        void Relayout()
        {
            int n = Math.Max(1, display.Count + (sepIndex >= 0 ? 1 : 0));
            int maxIcon = (int)(BASE * (1 + AMP));
            int w = 48 + n * (maxIcon + GAP);
            Rectangle sc = Screen.PrimaryScreen.Bounds;
            if (w > sc.Width - 80) w = sc.Width - 80;
            ClientSize = new Size(w, FORMH);
            if (buf == null || buf.Width != w || buf.Height != FORMH)
            {
                if (buf != null) buf.Dispose();
                buf = new Bitmap(w, FORMH, PixelFormat.Format32bppArgb);
            }
            Location = new Point((sc.Width - w) / 2, sc.Height - FORMH - BOTTOMM);
            // resting centers, centered exactly like the render-time layout does
            int slots = display.Count + (sepIndex >= 0 ? 1 : 0);
            float restTotal = display.Count * BASE + GAP * (slots - 1) + (sepIndex >= 0 ? 10 : 0);
            float rx = (w - restTotal) / 2f;
            for (int i = 0; i < display.Count; i++)
            {
                if (i == sepIndex) rx += 10 + GAP;      // separator slot
                display[i].RestCx = rx + BASE / 2f;
                display[i].Cx = display[i].RestCx;
                rx += BASE + GAP;
            }
            Render();
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x80000;   // TOOLWINDOW | LAYERED
                return cp;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0112 && (m.WParam.ToInt64() & 0xFFF0) == 0xF020) return;  // never minimize
            if (m.Msg == WM_APP_RENDER)
            {
                try { if (Visible) Render(); }   // hidden dock (fullscreen) paints nothing
                finally { Interlocked.Exchange(ref renderInFlight, 0); }
                return;
            }
            base.WndProc(ref m);
        }

        // winmm thread: poke the UI thread only - all painting stays on it. The
        // in-flight flag collapses skipped frames into one render instead of a
        // backlog burst after any UI stall.
        void AnimHeartbeat(uint id, uint msg, IntPtr user, IntPtr dw1, IntPtr dw2)
        {
            // adaptive half-rate on weak machines: when a dock frame costs more
            // than ~12ms (see [DOCK] perf) the heartbeat skips every other tick -
            // 30fps springs instead of 66, and the GDI lock gets air back
            int tick = Interlocked.Increment(ref hbTick);
            if (dockSlowDiv == 2 && (tick & 1) == 0) return;
            if (IsHandleCreated && Interlocked.Exchange(ref renderInFlight, 1) == 0)
            {
                try
                {
                    if (!Program.PostMessage(Handle, (uint)WM_APP_RENDER, IntPtr.Zero, IntPtr.Zero))
                        Interlocked.Exchange(ref renderInFlight, 0);
                }
                catch { Interlocked.Exchange(ref renderInFlight, 0); }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (animMmId != 0) { timeKillEvent(animMmId); animMmId = 0; }
            base.Dispose(disposing);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        int IndexNear(float mx)
        {
            int best = -1; float bd = 1e9f;
            for (int i = 0; i < display.Count; i++)
            {
                float dx = Math.Abs(mx - display[i].Cx);
                float reach = BASE * display[i].Scale / 2f + GAP / 2f;
                if (dx < reach && dx < bd) { bd = dx; best = i; }
            }
            return best;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int i = IndexNear(e.X);
                if (i >= 0 && i < display.Count && !display[i].IsGear && !display[i].IsSearch && !display[i].IsDesk)
                {
                    dragArm = true; dragging = false;
                    dragItem = display[i];
                    dragIdx = i; dragInsert = i; dragStartX = e.X;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mouseX = e.X;
            activeUntil = sw.Elapsed.TotalSeconds + 1.2;
            if (dragArm && !dragging && Math.Abs(e.X - dragStartX) > 10)
            {
                dragIdx = display.IndexOf(dragItem);
                if (dragIdx >= 0)
                {
                    dragging = true; dragArm = false;
                    activeUntil = sw.Elapsed.TotalSeconds + 2.0;
                    Program.Log("[DOCK] drag start '" + dragItem.Label + "'");
                }
                else dragArm = false;
            }
            if (dragging)
            {
                dragInsert = ComputeDragInsert();
                activeUntil = sw.Elapsed.TotalSeconds + 1.5;
            }
            base.OnMouseMove(e);
        }

        // insert slot (in display index space) for the dragged item at the current
        // mouse position, clamped to its own section: pinned icons reorder within
        // the pinned range, running apps within the running range; 桌面/搜索/齿轮
        // are fixed at the ends. Uses RestCx (stable rest layout) not Cx - the
        // per-frame flow shifts under the mouse, comparing against it oscillates.
        int ComputeDragInsert()
        {
            if (dragItem == null || dragIdx < 0 || dragIdx >= display.Count) return -1;
            int n = display.Count;
            int slot = n;
            for (int k = 0; k < n; k++) if (mouseX < display[k].RestCx) { slot = k; break; }
            bool isExtra = dragItem.IsExtra;
            int lo = isExtra ? sepIndex + 1 : 1;
            int hi = isExtra ? n - 2 : (sepIndex >= 0 ? sepIndex : n - 2);
            if (slot < lo) slot = lo;
            if (slot > hi) slot = hi;
            return slot;
        }

        void FinishDrag()
        {
            if (dragItem == null) { dragging = false; dragIdx = -1; return; }
            DockItem d = dragItem;
            int slot = ComputeDragInsert();
            if (slot < 0) { dragging = false; dragIdx = -1; return; }
            dragIdx = display.IndexOf(d);   // re-resolve against the current list
            if (dragIdx < 0) { dragging = false; dragIdx = -1; dragItem = null; return; }
            if (d.IsExtra)
            {
                List<DockItem> L = new List<DockItem>(extras);
                int local = 0;
                for (int i = 0; i < L.Count; i++)
                    if (L[i] != d && sepIndex + 1 + i < slot) local++;
                L.Remove(d);
                if (local > L.Count) local = L.Count;
                L.Insert(local, d);
                extras = L;
            }
            else
            {
                List<DockItem> L = new List<DockItem>(pinned);
                int local = 0;
                for (int i = 0; i < L.Count; i++)
                    if (L[i] != d && 1 + i < slot) local++;
                L.Remove(d);
                if (local > L.Count) local = L.Count;
                L.Insert(local, d);
                pinned = L;
            }
            Program.Log("[DOCK] drag drop '" + d.Label + "' -> slot " + slot);
            SaveOrder();
            dragging = false; dragIdx = -1; dragInsert = -1; dragItem = null;
            RebuildDisplay();
            Relayout();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (dragging) return;   // a live drag owns the pointer: keep last mouseX (a -9999 here would pin the insert slot to the section start)
            mouseX = -9999;
            activeUntil = sw.Elapsed.TotalSeconds + 1.2;
            if (dragArm) { dragArm = false; dragItem = null; dragIdx = -1; }   // left the dock before dragging: cancel arm
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && dragConsumedClick)
            {
                dragConsumedClick = false;      // mouse-up after a drag: not a click
                base.OnMouseClick(e);
                return;
            }
            int i = IndexNear(e.X);
            if (i < 0 || i >= display.Count) { base.OnMouseClick(e); return; }
            if (e.Button == MouseButtons.Left)
            {
                DockItem it = display[i];
                it.BounceT0 = sw.Elapsed.TotalSeconds;
                activeUntil = sw.Elapsed.TotalSeconds + 1.4;
                if (it.IsGear)
                {
                    lastMenuPos = PointToScreen(e.Location);
                    BuildControlMenu().Show(this, e.Location);
                    return;
                }
                if (it.IsSearch)
                {
                    LauncherForm.ShowLauncher();
                    return;
                }
                if (it.IsDesk)
                {
                    DesktopForm.ShowDesk();
                    return;
                }
                // always go through TryActivate on a worker thread: it checks liveness
                // itself and decides between activate / window-wake / cold launch.
                // NEVER trust the cached Running flag for this decision.
                // Activation must NEVER run on the UI thread: SetForegroundWindow/
                // ShowWindow send synchronous messages to the target, and a busy
                // target (QQ starting up etc.) would freeze our whole app.
                DockItem target = it;
                ThreadPool.QueueUserWorkItem(delegate { try { TryActivate(target); } catch { } });
            }
            base.OnMouseClick(e);
        }

        // full control surface reachable from the dock itself (taskbar may be gone)
        ContextMenu BuildControlMenu()
        {
            ContextMenu m = new ContextMenu();
            AppendControlItems(m);
            return m;
        }

        // menu items must be FRESH instances each time - reusing items from another
        // menu throws NullReferenceException inside MenuItemCollection.Add
        static void AppendControlItems(ContextMenu m)
        {
            // top of the menu = the two knobs people reach for most
            m.MenuItems.Add(new MenuItem("音量调节…", delegate { OpenVolumeSlider(); }));
            m.MenuItems.Add(BuildNetMenu());
            m.MenuItems.Add("-");
            MenuItem themes = new MenuItem("壁纸主题");
            for (int i = 1; i <= 4; i++)
            {
                int idx = i;
                MenuItem mi = new MenuItem(Scene.NameOf(i), delegate { AppShell.SwitchTheme(idx); });
                mi.RadioCheck = true;
                mi.Checked = (AppShell.CurrentTheme == i);
                themes.MenuItems.Add(mi);
            }
            m.MenuItems.Add(themes);
            MenuItem w = new MenuItem("桌面小组件", delegate { AppShell.ToggleWidgetsMenu(); });
            w.Checked = AppShell.ShowWidgets;
            m.MenuItems.Add(w);
            MenuItem t = new MenuItem("临时显示任务栏", delegate { AppShell.ToggleTaskbarMenu(); });
            t.Checked = AppShell.TempShowTaskbar;
            m.MenuItems.Add(t);
            m.MenuItems.Add(new MenuItem(AppShell.Paused ? "恢复动画" : "暂停动画", delegate { AppShell.TogglePause(); }));
            MenuItem io = new MenuItem("图标浮在壁纸上", delegate { AppShell.ToggleIconsOver(); });
            io.Checked = AppShell.IconsOverWallpaper;
            m.MenuItems.Add(io);
            m.MenuItems.Add(new MenuItem("立即刷新天气", delegate { WeatherState.Wake.Set(); }));
            m.MenuItems.Add("-");
            m.MenuItems.Add(new MenuItem("退出（恢复任务栏）", delegate { AppShell.ExitApp(); }));
        }

        // ---- audio + network quick control (2026-09-24) ----
        // screen coords of the last control-menu origin: slider/toasts anchor here
        static Point lastMenuPos;

        static void OpenVolumeSlider()
        {
            DockForm d = Ui;
            if (d == null || d.IsDisposed) return;
            int gx = d.Left + d.Width / 2;
            foreach (DockItem it in d.display)
                if (it.IsGear) { gx = d.Left + (int)it.RestCx; break; }
            VolumeSliderForm.ShowAt(gx, d.Top + (FORMH - PANELH - BOTTOMM));
        }

        static string SignalDots(uint sig)
        {
            int dots = sig >= 80 ? 4 : sig >= 60 ? 3 : sig >= 35 ? 2 : sig > 0 ? 1 : 0;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 4; i++) sb.Append(i < dots ? '●' : '○');
            return sb.ToString();
        }

        // built fresh on every menu open (menu items must never be reused), so the
        // wifi list always reflects the latest snapshot
        static MenuItem BuildNetMenu()
        {
            MenuItem net = new MenuItem("网络");
            try
            {
                MenuItem ei = new MenuItem("有线网络：" + NetCtl.EthernetStatus());
                ei.Enabled = false;
                net.MenuItems.Add(ei);
                NetCtl.Snapshot s = NetCtl.WifiSnapshot();
                if (!s.WlanOk)
                {
                    MenuItem wi = new MenuItem("WiFi：不可用");
                    wi.Enabled = false;
                    net.MenuItems.Add(wi);
                    return net;
                }
                MenuItem ws = new MenuItem(s.WifiConnected
                    ? "WiFi：已连接 " + s.Ssid + "（信号 " + s.Signal + "%）"
                    : "WiFi：未连接");
                ws.Enabled = false;
                net.MenuItems.Add(ws);
                net.MenuItems.Add(new MenuItem("-"));
                int shown = 0;
                foreach (NetCtl.WifiNet n in s.Nets)
                {
                    if (shown >= 14) break;
                    NetCtl.WifiNet nn = n;
                    string label = SignalDots(nn.Signal) + (nn.Connected ? " ✓" : "") + "  " + nn.Ssid
                        + (nn.NeedsPassword ? " ·需密码" : "");
                    MenuItem ni = new MenuItem(label, delegate { ConnectWifi(nn); });
                    net.MenuItems.Add(ni);
                    shown++;
                }
                if (shown == 0)
                {
                    MenuItem none = new MenuItem("（附近未发现网络）");
                    none.Enabled = false;
                    net.MenuItems.Add(none);
                }
                net.MenuItems.Add(new MenuItem("-"));
                net.MenuItems.Add(new MenuItem("刷新网络列表", delegate { RescanWifi(); }));
            }
            catch (Exception ex)
            {
                Program.Log("[NET] menu build fail " + ex.Message);
                MenuItem bad = new MenuItem("网络状态：获取失败");
                bad.Enabled = false;
                net.MenuItems.Add(bad);
            }
            return net;
        }

        // click a visible network: profiled/open ones connect right away, secured
        // unknown ones ask for the passphrase first. WlanConnect is async, so the
        // outcome is reported later as a toast-menu at the same menu position.
        static void ConnectWifi(NetCtl.WifiNet n)
        {
            if (n.NeedsPassword)
            {
                WifiPasswordForm.ShowFor(n.Ssid, lastMenuPos);
                return;
            }
            Program.Log("[NET] connect " + n.Ssid + (n.HasProfile ? " (profile)" : " (open)"));
            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = NetCtl.Connect(n.Ssid, null);
                bool ok = err == null && NetCtl.WaitConnected(n.Ssid, 16000);
                Program.Log("[NET] connect " + n.Ssid + " -> " + (ok ? "OK" : (err ?? "timeout")));
                ShowNetToast(ok ? "已连接 " + n.Ssid : "连接失败：" + (err ?? "超时，请稍后重试"));
            });
        }

        static void RescanWifi()
        {
            Program.Log("[NET] scan triggered");
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetCtl.TriggerScan();
                Thread.Sleep(3000);        // give the beacons a moment to arrive
                ShowNetToast(null);        // null = reopen the whole control menu
            });
        }

        // small non-interactive result popup at the last menu position; clicking
        // anywhere dismisses it. Must run on the UI thread via BeginInvoke.
        static void ShowNetToast(string text)
        {
            try
            {
                DockForm d = Ui;
                if (d == null || d.IsDisposed) return;
                d.BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        ContextMenu r = new ContextMenu();
                        if (text == null)
                        {
                            AppendControlItems(r);
                        }
                        else
                        {
                            MenuItem ri = new MenuItem(text);
                            ri.Enabled = false;
                            r.MenuItems.Add(ri);
                        }
                        r.Show(d, d.PointToClient(lastMenuPos));
                    }
                    catch { }
                });
            }
            catch { }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (dragging) { dragConsumedClick = true; FinishDrag(); }
                else if (dragArm) { dragArm = false; dragIdx = -1; }
                base.OnMouseUp(e);
                return;
            }
            if (e.Button == MouseButtons.Right)
            {
                if (dragging || dragArm) { dragArm = false; dragging = false; dragIdx = -1; return; }
                int i = IndexNear(e.X);
                ContextMenu m = new ContextMenu();
                if (i >= 0 && i < display.Count && !display[i].IsGear && !display[i].IsSearch && !display[i].IsDesk)
                {
                    int idx = i;
                    DockItem it = display[idx];
                    m.MenuItems.Add("打开 " + it.Label, delegate
                    {
                        // same live-decision path as a left click - never blindly
                        // Process.Start (that spawns login windows for running apps)
                        try { ThreadPool.QueueUserWorkItem(_ => { try { TryActivate(display[idx]); } catch { } }); }
                        catch { }
                    });
                    // running app (pinned-running or auto-detected extra): offer a real exit
                    if (it.IsExtra || it.Running)
                    {
                        m.MenuItems.Add(new MenuItem("退出 " + it.Label, delegate
                        {
                            DockItem target = display[idx];
                            ThreadPool.QueueUserWorkItem(delegate { try { QuitApp(target); } catch { } });
                        }));
                    }
                    if (it.IsExtra)
                    {
                        m.MenuItems.Add("固定到 Dock", delegate { try { MakeShortcut(display[idx].Path); } catch { } ReloadItems(); RefreshApps(); });
                        m.MenuItems.Add("不显示 " + it.ExeName, delegate
                        {
                            DockItem hit = it;   // capture the item, not display[idx]: the list can rebuild while the menu is open
                            try { HideApp(hit.ExeName); } catch { }
                            try
                            {
                                extras.RemoveAll(x => HiddenNames.Contains(x.ExeName.ToLower()));
                                RebuildDisplay();
                                Relayout();
                            }
                            catch { }
                            RefreshApps();
                        });
                        if (it.Win != IntPtr.Zero)
                            m.MenuItems.Add("关闭窗口", delegate { try { Program.PostMessage(display[idx].Win, 0x0010, IntPtr.Zero, IntPtr.Zero); } catch { } });
                    }
                    else if (it.IsLink)
                    {
                        m.MenuItems.Add("从 Dock 移除", delegate { try { File.Delete(display[idx].Path); } catch { } ReloadItems(); RebuildDisplay(); Relayout(); });
                    }
                    else if (it.IsBuiltin)
                    {
                        // built-ins (文件资源管理器/Edge) have no .lnk to delete - the
                        // removal is remembered in dockRemoved.txt (delete the line to restore)
                        m.MenuItems.Add("从 Dock 移除", delegate
                        {
                            DockItem hit = it;
                            try { RemoveBuiltin(hit.Label); } catch { }
                            ReloadItems(); RebuildDisplay(); Relayout();
                        });
                    }
                    m.MenuItems.Add("-");
                }
                AppendControlItems(m);
                lastMenuPos = PointToScreen(e.Location);
                m.Show(this, e.Location);
            }
            base.OnMouseUp(e);
        }

        // returns true only if the window really became the foreground window
        static bool ActivateWindow(IntPtr h)
        {
            try
            {
                if (!Program.IsWindow(h)) return false;
                uint tgtPid;
                Program.GetWindowThreadProcessId(h, out tgtPid);
                bool wasVis = Program.IsWindowVisible(h);
                Program.Log(string.Format("[ACT] activate hwnd {0} pid {1} vis {2}", h, tgtPid, wasVis));
                // tray-hidden / minimized windows wake with SW_RESTORE. Only ranked
                // main-window candidates ever reach this point - showing an unranked
                // hidden helper creates an invisible overlay that eats every click.
                bool woke = false;
                if (!Program.IsWindowVisible(h)) { Program.ShowWindow(h, 9); Thread.Sleep(150); woke = true; }
                else if (Program.IsIconic(h)) { Program.ShowWindow(h, 9); Thread.Sleep(150); woke = true; }
                if (woke)
                {
                    // Chromium/Qt apps (QQNT/WeChat/...) discard their compositor
                    // surface while tray-hidden and do NOT repaint on an external
                    // SW_RESTORE - the window comes back pure black (the QQ "zombie").
                    // A 1px size round-trip forces a full re-layout and repaint.
                    Program.RECT nr;
                    if (Program.GetWindowRect(h, out nr) && nr.Right - nr.Left > 50 && nr.Bottom - nr.Top > 50)
                    {
                        int nw = nr.Right - nr.Left, nh = nr.Bottom - nr.Top;
                        Program.SetWindowPos(h, IntPtr.Zero, nr.Left, nr.Top, nw + 1, nh, 0x0002 | 0x0004 | 0x0010);  // NOMOVE|NOZORDER|NOACTIVATE
                        Thread.Sleep(120);
                        Program.SetWindowPos(h, IntPtr.Zero, nr.Left, nr.Top, nw, nh, 0x0002 | 0x0004 | 0x0010);
                        Thread.Sleep(150);
                    }
                }
                // pull fully off-screen windows back into view (title bar must be reachable)
                try
                {
                    Program.RECT wr;
                    if (Program.GetWindowRect(h, out wr))
                    {
                        Rectangle sb2 = Screen.PrimaryScreen.Bounds;
                        int nx = wr.Left, ny = wr.Top, nw = wr.Right - wr.Left, nh = wr.Bottom - wr.Top;
                        if (nw > sb2.Width) nw = sb2.Width;
                        if (nh > sb2.Height) nh = sb2.Height;
                        if (nx < sb2.Left) nx = sb2.Left;
                        if (ny < sb2.Top) ny = sb2.Top;
                        if (nx + nw > sb2.Right) nx = sb2.Right - nw;
                        if (ny + nh > sb2.Bottom) ny = sb2.Bottom - nh;
                        if (nx != wr.Left || ny != wr.Top || nw != wr.Right - wr.Left || nh != wr.Bottom - wr.Top)
                            Program.SetWindowPos(h, IntPtr.Zero, nx, ny, nw, nh, 0x0004 | 0x0010);  // NOZORDER|NOACTIVATE
                    }
                }
                catch { }
                // TOPMOST-toggle: forces the window to the very top of the z-order
                // (a plain SetForegroundWindow sometimes leaves it behind a maximized window)
                Program.SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                Program.SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                // ALT-down/up around SetForegroundWindow grants this thread the
                // foreground right. NEVER AttachThreadInput/SetFocus (shared input
                // queue deadlocks both sides) and NEVER warp the real cursor with
                // SetCursorPos/mouse_event - synthetic clicks made the mouse jump
                // around and landed on random buttons inside the target app.
                Program.keybd_event(0x12, 0, 0, UIntPtr.Zero);
                Program.SetForegroundWindow(h);
                Program.keybd_event(0x12, 0, 2, UIntPtr.Zero);
                Thread.Sleep(250);
                if (ForegroundPidIs(tgtPid) && Program.IsWindowVisible(h)) return true;
                // one gentle retry of the foreground call only - no minimize/restore
                // dances, no showing further windows
                Program.keybd_event(0x12, 0, 0, UIntPtr.Zero);
                Program.SetForegroundWindow(h);
                Program.keybd_event(0x12, 0, 2, UIntPtr.Zero);
                Thread.Sleep(200);
                if (ForegroundPidIs(tgtPid) && Program.IsWindowVisible(h)) return true;
                return false;
            }
            catch { return false; }
        }

        static bool ForegroundPidIs(uint pid)
        {
            IntPtr fg = Program.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            uint fgPid;
            Program.GetWindowThreadProcessId(fg, out fgPid);
            return fgPid == pid;
        }

        // debug hook for --act: exercise the click-time path without the UI
        public static void DebugActivate(string label)
        {
            DockItem it = new DockItem();
            it.Label = label;
            it.ExeName = label;   // extras carry the exe name in both fields
            TryActivate(it);
        }

        // invisible 1x1 control the chooser menu anchors to (moved before each show);
        // lets ContextMenu.Show park a menu ABOVE the dock using only positive coords
        Control chooserAnchor;

        // one app, several windows: popup above the icon lists them all and the
        // user raises exactly the one they meant (the taskbar-preview equivalent)
        void ShowWindowChooser(DockItem it, List<IntPtr> wins)
        {
            try
            {
                ContextMenu m = new ContextMenu();
                int maxLen = 0;   // weighted: CJK chars count 2
                foreach (IntPtr h in wins)
                {
                    StringBuilder sb = new StringBuilder(240);
                    int ln = Program.GetWindowText(h, sb, 240);
                    string title = ln > 0 ? sb.ToString() : "(untitled window)";
                    title = StripAppSuffix(title, it.Label);
                    if (title.Length > 52) title = title.Substring(0, 50) + "…";
                    int wl = 0;
                    foreach (char c in title) wl += (c > 255 ? 2 : 1);
                    if (wl > maxLen) maxLen = wl;
                    MenuItem mi = new MenuItem(title);
                    IntPtr target = h;
                    mi.Click += delegate
                    {
                        // activation makes synchronous calls to the target - never on
                        // the UI thread (dock would freeze behind a busy app)
                        ThreadPool.QueueUserWorkItem(delegate { try { ActivateWindow(target); } catch { } });
                    };
                    m.MenuItems.Add(mi);
                }
                // place the popup hugging the icon. Probe-verified facts about
                // ContextMenu.Show: the point is CLIENT coords of the source control
                // (PointToScreen'd internally) and the menu's TOP-LEFT anchors there;
                // a negative raw point flips to bottom-right anchoring + monitor
                // clamping (that's what once threw the chooser to the screen top).
                // So park a 1x1 anchor control at the desired menu-top and pass (0,0) -
                // a negative CONTROL location is fine, only the Show point must be >=0.
                // The menu bottom hugs the visible BAR top (Top+FORMH-PANELH-BOTTOMM),
                // i.e. attached to the dock right above the clicked icon - NOT the
                // form top, which floats ~90px above the bar and read as "too far".
                int menuW = Math.Min(Screen.FromControl(this).Bounds.Width - 40, 110 + maxLen * 8);
                int menuH = m.MenuItems.Count * 25 + 4;
                if (chooserAnchor == null)
                {
                    chooserAnchor = new Control();
                    chooserAnchor.Size = new Size(1, 1);
                    Controls.Add(chooserAnchor);
                }
                float cxNow = it.Cx > 0 ? it.Cx : it.RestCx;   // center as drawn at click time
                int ax = Math.Max(8, Math.Min(Width - menuW - 8, (int)cxNow - menuW / 2));
                int ay = FORMH - PANELH - BOTTOMM - 2 - menuH;
                chooserAnchor.Location = new Point(ax, ay);
                m.Show(chooserAnchor, new Point(0, 0));
                Program.Log("[ACT] chooser shown for '" + it.Label + "' (" + wins.Count + " windows)");
            }
            catch (Exception ex) { Program.Log("[ACT] chooser fail " + ex.Message); }
        }

        // "Netscape - Mozilla Firefox" style suffixes waste the menu budget and hide
        // the page title that actually tells the windows apart
        static string StripAppSuffix(string title, string label)
        {
            if (label == null || label.Length == 0) return title;
            foreach (string sep in new[] { " — ", " - " })
            {
                string suf = sep + label;
                if (title.EndsWith(suf, StringComparison.OrdinalIgnoreCase)
                    && title.Length - suf.Length >= 2)
                    return title.Substring(0, title.Length - suf.Length);
            }
            return title;
        }

        // processes that must never be killed from the dock's 退出 menu
        static readonly HashSet<string> QuitDenylist = new HashSet<string>
        {
            "explorer", "techrainwallpaper", "dwm", "sihost", "ctfmon", "chsime",
            "applicationframehost", "searchhost", "startmenuexperiencehost", "shellexperiencehost"
        };

        // graceful-ish exit: WM_CLOSE first (tray apps just hide, real apps exit),
        // then kill whatever is still alive after the grace period
        static void QuitApp(DockItem it)
        {
            try
            {
                string[] names;
                if (!Alias.TryGetValue(it.Label, out names)) names = new[] { it.ExeName };
                List<Process> procs = new List<Process>();
                foreach (string n in names)
                {
                    if (n == null || n.Length == 0) continue;
                    if (QuitDenylist.Contains(n.ToLower())) continue;
                    try { foreach (Process p in Process.GetProcessesByName(n)) procs.Add(p); }
                    catch { }
                }
                if (procs.Count == 0) { Program.Log("[QUIT] " + it.Label + ": no processes"); return; }
                Program.Log("[QUIT] " + it.Label + ": " + procs.Count + " processes");
                foreach (Process p in procs)
                {
                    try { p.CloseMainWindow(); } catch { }
                }
                Thread.Sleep(1500);
                foreach (Process p in procs)
                {
                    try { if (!p.HasExited) { p.Kill(); Program.Log("[QUIT] killed " + p.ProcessName + " " + p.Id); } }
                    catch { }
                }
            }
            catch (Exception ex) { Program.Log("[QUIT] EXCEPTION " + ex.Message); }
        }

        // ---------- tray-icon click: the ONLY wake-up that reconnects input ----------
        // Electron/Qt tray apps (QQNT, WeChat 4.x, cloudmusic) disconnect their input
        // pipeline when THEY hide to tray; an external SW_RESTORE brings the pixels
        // back but the window stays click-dead. Clicking the app's own tray icon runs
        // its show() path, which fully revives the window. The click is DELIVERED via
        // PostMessage to the tray toolbar - the cursor never moves.
        static bool TrayIconClick(string[] procNames)
        {
            bool hidTb = false, hidOv = false;
            IntPtr tbwnd = IntPtr.Zero, ovWnd = IntPtr.Zero;
            bool oldTemp = AppShell.TempShowTaskbar;
            AppShell.TempShowTaskbar = true;   // hold off the 2s taskbar re-hide tick
            bool clicked = false;
            try
            {
                if (procNames == null || procNames.Length == 0) return false;
                tbwnd = Program.FindWindow("Shell_TrayWnd", null);
                ovWnd = Program.FindWindow("NotifyIconOverflowWindow", null);
                List<IntPtr> tbs = new List<IntPtr>();
                if (ovWnd != IntPtr.Zero)
                {
                    // EP-classic overflow has the toolbar directly; stock Win10 via SysPager
                    IntPtr t = Program.FindWindowEx(ovWnd, IntPtr.Zero, "ToolbarWindow32", null);
                    if (t == IntPtr.Zero)
                    {
                        IntPtr pg = Program.FindWindowEx(ovWnd, IntPtr.Zero, "SysPager", null);
                        if (pg != IntPtr.Zero) t = Program.FindWindowEx(pg, IntPtr.Zero, "ToolbarWindow32", null);
                    }
                    if (t != IntPtr.Zero) tbs.Add(t);
                }
                if (tbwnd != IntPtr.Zero)
                {
                    IntPtr tn = Program.FindWindowEx(tbwnd, IntPtr.Zero, "TrayNotifyWnd", null);
                    if (tn != IntPtr.Zero)
                    {
                        IntPtr pg = Program.FindWindowEx(tn, IntPtr.Zero, "SysPager", null);
                        IntPtr t = pg != IntPtr.Zero ? Program.FindWindowEx(pg, IntPtr.Zero, "ToolbarWindow32", null) : IntPtr.Zero;
                        if (t == IntPtr.Zero) t = Program.FindWindowEx(tn, IntPtr.Zero, "ToolbarWindow32", null);
                        if (t != IntPtr.Zero) tbs.Add(t);
                    }
                }
                if (tbs.Count == 0) return false;
                hidTb = tbwnd != IntPtr.Zero && !Program.IsWindowVisible(tbwnd);
                hidOv = ovWnd != IntPtr.Zero && !Program.IsWindowVisible(ovWnd);
                if (hidTb) Program.ShowWindow(tbwnd, 5);
                if (hidOv) { Program.ShowWindow(ovWnd, 8); Thread.Sleep(400); }
                else if (hidTb) Thread.Sleep(400);
                foreach (IntPtr tb in tbs)
                {
                    if (TrayClickOwned(tb, procNames)) { clicked = true; break; }
                }
                Thread.Sleep(800);                 // let the app run its own show path
            }
            catch { }
            finally
            {
                try
                {
                    if (hidOv) Program.ShowWindow(ovWnd, 0);
                    if (hidTb) Program.ShowWindow(tbwnd, 0);
                }
                catch { }
                AppShell.TempShowTaskbar = oldTemp;
            }
            return clicked;
        }

        // scans a notification-area toolbar for a button whose owning process matches
        // one of procNames, then posts a click at its center. Owner matching is a must:
        // Electron tray buttons often carry a bogus/empty tooltip.
        static bool TrayClickOwned(IntPtr tb, string[] procNames)
        {
            uint expPid;
            Program.GetWindowThreadProcessId(tb, out expPid);
            IntPtr proc = Program.OpenProcess(0x1F0FFF, false, expPid);
            if (proc == IntPtr.Zero) return false;
            try
            {
                IntPtr remote = Program.VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x1000, 0x40);
                if (remote == IntPtr.Zero) return false;
                try
                {
                    int count = (int)Program.SendMessage(tb, 0x400 + 24, IntPtr.Zero, IntPtr.Zero);        // TB_BUTTONCOUNT
                    byte[] buf = new byte[64]; IntPtr dummy;
                    for (int i = 0; i < count; i++)
                    {
                        if (Program.SendMessage(tb, 0x400 + 23, (IntPtr)i, remote) == IntPtr.Zero) continue;  // TB_GETBUTTON
                        if (!Program.ReadProcessMemory(proc, remote, buf, (UIntPtr)32, out dummy)) continue;
                        IntPtr dwData = (IntPtr)BitConverter.ToInt64(buf, 16);
                        if (dwData == IntPtr.Zero) continue;
                        byte[] dbuf = new byte[64];
                        if (!Program.ReadProcessMemory(proc, dwData, dbuf, (UIntPtr)64, out dummy)) continue;
                        // the explorer icon record embeds the app's callback HWND; scan
                        // aligned slots for a window NOT owned by explorer and check owner
                        for (int off = 0; off <= 56; off += 4)
                        {
                            IntPtr mh = (IntPtr)BitConverter.ToInt64(dbuf, off);
                            if (mh == IntPtr.Zero) continue;
                            uint wp;
                            Program.GetWindowThreadProcessId(mh, out wp);
                            if (wp == 0 || wp == expPid) continue;
                            string owner = "";
                            try { owner = Process.GetProcessById((int)wp).ProcessName; } catch { }
                            if (owner.Length == 0) continue;
                            bool hit = false;
                            foreach (string n in procNames)
                                if (n != null && owner.Equals(n, StringComparison.OrdinalIgnoreCase)) { hit = true; break; }
                            if (!hit) continue;
                            if (Program.SendMessage(tb, 0x400 + 29, (IntPtr)i, remote) == IntPtr.Zero) break;  // TB_GETITEMRECT
                            if (!Program.ReadProcessMemory(proc, remote, buf, (UIntPtr)16, out dummy)) break;
                            int rx = (BitConverter.ToInt32(buf, 0) + BitConverter.ToInt32(buf, 8)) / 2;
                            int ry = (BitConverter.ToInt32(buf, 4) + BitConverter.ToInt32(buf, 12)) / 2;
                            Program.Log("[TRAY] click " + owner + " button#" + i + " @" + rx + "," + ry);
                            IntPtr lp = (IntPtr)((ry << 16) | (rx & 0xFFFF));
                            // WM_MOUSEMOVE first: the (re)built tray toolbar ignores
                            // click messages for buttons it has not seen hovered
                            Program.PostMessage(tb, 0x0200, IntPtr.Zero, lp);   // WM_MOUSEMOVE
                            Thread.Sleep(150);
                            Program.PostMessage(tb, 0x0201, (IntPtr)0x0001, lp);   // WM_LBUTTONDOWN
                            Thread.Sleep(80);
                            Program.PostMessage(tb, 0x0202, IntPtr.Zero, lp);      // WM_LBUTTONUP
                            return true;
                        }
                    }
                }
                finally { Program.VirtualFreeEx(proc, remote, UIntPtr.Zero, 0x8000); }
            }
            finally { Program.CloseHandle(proc); }
            return false;
        }

        static bool ForegroundInPids(List<uint> pids)
        {
            IntPtr fg = Program.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            uint fgPid;
            Program.GetWindowThreadProcessId(fg, out fgPid);
            return pids.Contains(fgPid);
        }

        // launch path for a pinned item: a live .Path wins; a dead shipped .lnk is
        // re-resolved (start menu / app paths / fuzzy) and cached into the item so
        // the click actually starts the app on a fresh machine
        static string LaunchPathFor(DockItem it)
        {
            try
            {
                if (!string.IsNullOrEmpty(it.Path) && File.Exists(it.Path)) return it.Path;
                string re = ResolveDeadLnk(it.Label);
                if (re != null)
                {
                    Program.Log("[ACT] launch path re-resolved for '" + it.Label + "' -> " + re);
                    // self-heal the shipped lnk so the icon and future launches work
                    if (!string.IsNullOrEmpty(it.Path) && it.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(it.Path))
                    {
                        try
                        {
                            Type t2 = Type.GetTypeFromProgID("WScript.Shell");
                            object ws2 = Activator.CreateInstance(t2);
                            object sc2 = t2.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, ws2, new object[] { it.Path });
                            sc2.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc2, new object[] { re });
                            sc2.GetType().InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc2, new object[] { Path.GetDirectoryName(re) });
                            sc2.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc2, null);
                        }
                        catch { }
                    }
                    return re;
                }
            }
            catch (Exception ex) { Program.Log("[ACT] LaunchPathFor '" + it.Label + "' failed: " + ex.Message); }
            return null;
        }

        // decides on the spot: cold-launch a dead app, or activate an existing window
        static void TryActivate(DockItem it)
        {
            try
            {
                Program.Log(string.Format("[ACT] '{0}' (extra={1}, exe={2})", it.Label, it.IsExtra, it.ExeName));
                // gather pids belonging to this app (alias-aware); record start times so
                // the user's original logged-in instance can be preferred
                List<uint> pids = new List<uint>();
                Dictionary<uint, DateTime> startTimes = new Dictionary<uint, DateTime>();
                if (it.LaunchPid > 0) pids.Add((uint)it.LaunchPid);
                string[] names;
                if (!Alias.TryGetValue(it.Label, out names)) names = new[] { it.ExeName };
                foreach (string n in names)
                {
                    // per-PROCESS try: one denied process (elevated service) must
                    // not abort the whole enumeration and silently drop the real
                    // UI process (ToDesk's windowed pid died exactly this way)
                    foreach (Process p in Process.GetProcessesByName(n))
                    {
                        if (!pids.Contains((uint)p.Id)) pids.Add((uint)p.Id);
                        // ONE StartTime call, inside the try: the old code read it a
                        // SECOND time for the log line - that throw killed the rest
                        try
                        {
                            DateTime st = p.StartTime;
                            startTimes[(uint)p.Id] = st;
                            Program.Log(string.Format("[ACT]   proc {0} pid {1} start {2:HH:mm:ss}", n, p.Id, st));
                        }
                        catch
                        {
                            Program.Log("[ACT]   proc " + n + " pid " + p.Id + " (start time denied)");
                        }
                    }
                }

                // collect ALL candidate windows: BOTH visible and hidden, deduped.
                // (collecting only visible windows while a leftover login dialog is on
                // screen made the real hidden main window invisible to the ranking,
                // which then activated the login dialog - the reported bug)
                List<IntPtr> wins = new List<IntPtr>();
                foreach (uint pid in pids)
                {
                    CollectWins(pid, "", wins, false);
                    CollectWins(pid, "", wins, true);
                }
                if (wins.Count == 0 && it.Label.Length > 0)
                    CollectWins(0, it.Label, wins, false);
                // a dead .lnk's shell ghost handle (or any gone window) must not
                // block the cold launch: pids=0 + one stale handle previously
                // defeated the "provably not running" test forever
                if (it.Win != IntPtr.Zero && Program.IsWindow(it.Win) && !wins.Contains(it.Win)) wins.Add(it.Win);
                // dedupe
                List<IntPtr> uniq = new List<IntPtr>();
                foreach (IntPtr h in wins) if (!uniq.Contains(h)) uniq.Add(h);
                wins = uniq;
                Program.Log(string.Format("[ACT] {0} processes, {1} candidate windows", pids.Count, wins.Count));

                // cold launch ONLY when the app is provably not running and has no windows
                if (pids.Count == 0 && wins.Count == 0)
                {
                    string launchPath = LaunchPathFor(it);
                    Program.Log("[ACT] not running -> cold launch " + launchPath);
                    LauncherForm.RecordRecent(launchPath);
                    try
                    {
                        Process proc = Process.Start(new ProcessStartInfo(launchPath) { UseShellExecute = true });
                        if (proc != null) { it.LaunchPid = proc.Id; it.Running = true; }
                    }
                    catch (Exception lex) { Program.Log("[ACT] cold launch failed: " + lex.Message); }
                    return;
                }

                // NOTE: do NOT re-invoke 微信/QQ here - WeChat 4.x and QQNT support
                // MULTI-instance: launching again while logged in pops a login window
                // for a new account. Window activation (below) is the only safe path.

                // tray-hidden Electron/Qt apps: if the app is running and NONE of its
                // windows is visible, click its own tray icon - the only wake-up that
                // reconnects its input pipeline (external restores leave a click-dead
                // ghost). A tray click on a VISIBLE app would toggle-HIDE it, so this
                // only runs in the fully-hidden state.
                // ONLY QQ/WeChat defer to the tray dance when fully tray-hidden;
                // every other app restores its hidden window directly
                bool trayDoctrine = TrayDoctrine.Contains(it.Label);
                foreach (string n in names) if (TrayDoctrine.Contains(n)) trayDoctrine = true;
                // an item that came FROM the notification area has its tray icon as
                // the wake-up lever (ToDesk et al. - external restore leaves ghosts).
                // The live resident set also covers PINNED copies of tray apps: a
                // pinned ToDesk has IsTray=false, yet its hidden main still needs
                // the tray dance (external restore leaves an input-dead ghost)
                if (TrayResidents.Contains((it.ExeName ?? "").ToLower())) trayDoctrine = true;
                if (it.IsTray) trayDoctrine = true;
                // USER DIRECTIVE override - must stay LAST so it always wins: these
                // apps restore perfectly directly; the tray dance only cost seconds
                foreach (string n in names) if (DirectRestore.Contains(n)) trayDoctrine = false;
                if (DirectRestore.Contains(it.Label ?? "")) trayDoctrine = false;
                bool anyVis = false;
                foreach (IntPtr h in wins) if (Program.IsWindowVisible(h)) { anyVis = true; break; }
                if (pids.Count > 0 && !anyVis && trayDoctrine)
                    Program.Log("[ACT] fully tray-hidden -> tray-icon path (doctrine app)");

                // earliest instance = earliest-starting pid that ACTUALLY owns a candidate
                // window (a leftover orphan process with no windows must not steal the crown)
                uint firstPid = 0;
                DateTime firstStart = DateTime.MaxValue;
                foreach (IntPtr h in wins)
                {
                    uint wp = 0;
                    Program.GetWindowThreadProcessId(h, out wp);
                    DateTime st;
                    if (startTimes.TryGetValue(wp, out st) && st < firstStart) { firstStart = st; firstPid = wp; }
                }
                Program.Log(string.Format("[ACT] firstPid(with windows) = {0}", firstPid));

                Rectangle scr = Screen.PrimaryScreen.Bounds;
                long screenArea = (long)scr.Width * scr.Height;
                // Chromium/Qt apps keep an untitled render-host window (shows as a dead
                // black window if ever restored). When ANY titled candidate exists, the
                // untitled hosts must never win.
                bool hasTitled = false;
                foreach (IntPtr h in wins)
                {
                    if (Program.GetWindowTextLength(h) > 0)
                    {
                        Program.RECT rr;
                        if (Program.GetWindowRect(h, out rr)
                            && (rr.Right - rr.Left) >= 560 && (rr.Bottom - rr.Top) >= 420) { hasTitled = true; break; }
                    }
                }
                Program.Log("[ACT] hasTitledCandidate = " + hasTitled);
                // windows that OWN a 'Default IME' peer accept text input - the real
                // interactive main. QQNT keeps lookalike 'QQ'-titled shell frames that
                // render but never take input; the IME association separates them.
                HashSet<IntPtr> imeOwners = new HashSet<IntPtr>();
                try
                {
                    Program.GLEnumProc imeCb = delegate(IntPtr ih, IntPtr il)
                    {
                        StringBuilder cn = new StringBuilder(64);
                        if (Program.GetClassName(ih, cn, 64) > 0 && cn.ToString() == "IME")
                        {
                            IntPtr own = Program.GetWindow(ih, 4);   // GW_OWNER
                            if (own != IntPtr.Zero) imeOwners.Add(own);
                        }
                        return true;
                    };
                    Program.EnumWindows(imeCb, IntPtr.Zero);
                }
                catch { }
                List<long> scores = new List<long>();
                List<IntPtr> ranked = new List<IntPtr>();
                RankWindows(wins, startTimes, firstPid, screenArea, true, hasTitled, it.Label, imeOwners, ranked, scores);
                // rerun without the size floor also when only junk survived it: the real
                // hidden main (e.g. a smallish 微信 window) then still enters the contest
                if (ranked.Count == 0 || scores[0] < 0)
                {
                    ranked.Clear(); scores.Clear();
                    RankWindows(wins, startTimes, firstPid, screenArea, false, hasTitled, it.Label, imeOwners, ranked, scores);
                }
                for (int k = 0; k < Math.Min(5, ranked.Count); k++)
                {
                    StringBuilder lb = new StringBuilder(200);
                    int ln = Program.GetWindowText(ranked[k], lb, 200);
                    Program.Log(string.Format("[ACT]   #{0} score {1} '{2}'", k + 1, scores[k],
                        ln > 0 ? lb.ToString(0, ln) : "(untitled)"));
                }

                // the app owns SEVERAL real windows: a bare click would always raise
                // only the top-ranked (most recent) one and bury the rest - offer the
                // full list above the icon and let the user pick, taskbar-style.
                // Runs BEFORE the tray path so a tray-hidden multi-window app gets
                // the chooser on the FIRST click (a picked hidden window restores).
                // Visible windows first; hidden titled mains top the menu up when
                // fewer than two are visible (fully tray-hidden state)
                // DOCTRINE APPS FULLY TRAY-HIDDEN: the tray dance runs BEFORE the
                // chooser. The chooser's picked-window restore is an external
                // restore - on QQ/WeChat it surfaces an input-dead zombie (user:
                // "说无数次了"). Visible-window states keep the chooser; only the
                // fully-hidden state is routed straight to the tray.
                if (pids.Count > 0 && !anyVis && trayDoctrine)
                {
                    Program.Log("[ACT] fully tray-hidden doctrine app -> tray dance (chooser skipped)");
                    if (TraySurface(names, pids)) return;
                    Program.Log("[ACT] tray dance failed -> falling through");
                }

                List<IntPtr> choosable = new List<IntPtr>();
                HashSet<string> seenKeys = new HashSet<string>();
                for (int pass = 0; pass < 2; pass++)   // pass 0: visible, pass 1: hidden top-up
                {
                    for (int k = 0; k < ranked.Count && choosable.Count < 8; k++)
                    {
                        if (scores[k] < 0) continue;      // dummy/login-penalized junk
                        IntPtr h = ranked[k];
                        bool vis = Program.IsWindowVisible(h);
                        if (pass == 0 ? !vis : vis) continue;
                        if (Program.GetWindowTextLength(h) == 0) continue;
                        // same class+title = indistinguishable entries (ToDesk keeps a
                        // hidden shell clone of its main window) - keep the first
                        StringBuilder ckb = new StringBuilder(96);
                        Program.GetClassName(h, ckb, 96);
                        StringBuilder tkb = new StringBuilder(128);
                        Program.GetWindowText(h, tkb, 128);
                        string key = ckb.ToString() + "|" + tkb.ToString();
                        if (!seenKeys.Add(key)) continue;
                        choosable.Add(h);
                        if (choosable.Count >= 8) break;
                    }
                    if (choosable.Count >= 2) break;      // hidden top-up only when needed
                }
                if (choosable.Count > 1 && Ui != null && !Ui.IsDisposed)
                {
                    Program.Log("[ACT] " + choosable.Count + " windows -> chooser menu");
                    List<IntPtr> picks = new List<IntPtr>(choosable);
                    DockItem item = it;
                    Ui.BeginInvoke((MethodInvoker)delegate { Ui.ShowWindowChooser(item, picks); });
                    return;
                }

                // DirectRestore apps fully hidden with NO titled window anywhere:
                // there is no real window to restore (chrome closed-to-background
                // keeps only untitled helper surfaces - restoring one pops an
                // abnormal blank frame). Chrome is single-instance per profile:
                // re-invoking its .lnk makes the running instance open a proper
                // new window - still direct, still not the tray path.
                if (!anyVis && !hasTitled && pids.Count > 0)
                {
                    bool drHit = DirectRestore.Contains(it.Label ?? "");
                    foreach (string n in names) if (DirectRestore.Contains(n)) drHit = true;
                    if (drHit)
                    {
                        string launchPath = LaunchPathFor(it);
                        if (launchPath != null)
                        {
                            Program.Log("[ACT] direct-launch: fully hidden, no titled window -> re-invoke (" + it.Label + " via " + launchPath + ")");
                            string lnk = launchPath;
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                try { Process.Start(new ProcessStartInfo(lnk) { UseShellExecute = true }); }
                                catch (Exception ex) { Program.Log("[ACT] direct-launch failed: " + ex.Message); }
                            });
                            return;
                        }
                        Program.Log("[ACT] direct-launch wanted but no resolvable launch path for " + it.Label);
                    }
                }

                // tray-hidden doctrine apps (QQ/WeChat): the tray dance
                if (pids.Count > 0 && !anyVis && trayDoctrine && TraySurface(names, pids)) return;

                // try SAFE candidates (non-dummy), up to three
                int safe = 0;
                for (int k = 0; k < ranked.Count && safe < 3; k++)
                {
                    if (scores[k] < 0) continue;              // dummy/login-penalized junk
                    safe++;
                    if (ActivateWindow(ranked[k])) { Program.Log("[ACT] SUCCESS #" + (k + 1)); return; }
                    Program.Log("[ACT] #" + (k + 1) + " failed verification, next");
                }
                if (ranked.Count > 0)
                {
                    // best-effort raise of the top-ranked window, no showing anything new
                    IntPtr top = ranked[0];
                    if (Program.IsWindowVisible(top))
                    {
                        Program.Log("[ACT] best-effort raise of top candidate");
                        try
                        {
                            Program.keybd_event(0x12, 0, 0, UIntPtr.Zero);
                            Program.SetForegroundWindow(top);
                            Program.keybd_event(0x12, 0, 2, UIntPtr.Zero);
                        }
                        catch { }
                        return;
                    }
                }
                // The app IS running (pids/windows were found above), so NEVER re-invoke
                // its exe here: QQNT / WeChat 4.x are multi-instance - a second launch
                // while logged in pops the "login new account" window, the exact bug
                // this dock suffered from. Failing silently is strictly better.
                Program.Log("[ACT] app running but no activatable window - giving up (no re-invoke)");
            }
            catch (Exception ex) { Program.Log("[ACT] EXCEPTION " + ex.Message); }
        }

        // the tray-icon dance: click the app's own tray button so it resurfaces
        // itself - the only safe wake for QQNT/WeChat-style input pipelines, but
        // slow (seconds of waits), so per-app policy may defer it to last resort
        static bool TraySurface(string[] names, List<uint> pids)
        {
            Program.Log("[ACT] fully tray-hidden -> tray-icon path");
            // up to two tray attempts: a single posted click occasionally gets
            // lost in the flyout - retrying is far safer than an external raise
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (TrayIconClick(names))
                {
                    for (int wait = 0; wait < 6; wait++)
                    {
                        Thread.Sleep(400);
                        if (ForegroundInPids(pids))
                        {
                            Program.Log("[ACT] tray path SUCCESS (app surfaced itself)");
                            return true;
                        }
                    }
                }
            }
            Program.Log("[ACT] tray click done, foreground not yet app - raising");
            return false;
        }

        // helper/overlay window titles that must never steal the main window's crown:
        // tray message windows, GDI+ internals, desktop-lyrics overlays of music apps
        static bool IsJunkTitle(string t)
        {
            if (t == null || t.Length == 0) return false;
            string[] parts = { "GDI+ Window", "TrayIcon", "MessageWindow", "Default IMC", "MSCTFIME", "桌面歌词" };
            foreach (string p in parts) if (t.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // main-window ranking: title-matches-dock-label > iconic (was a real interactive
        // main) > IME-owner (accepts text input) > earliest instance > top-level (no
        // owner) > titled > bigger; login-sized windows excluded in the first pass;
        // untitled render-hosts lose when any titled candidate exists; login-UI titles,
        // junk helper titles, layered dummies and oversized overlays are rejected outright
        static void RankWindows(List<IntPtr> wins, Dictionary<uint, DateTime> startTimes, uint firstPid,
            long screenArea, bool sizeFloor, bool excludeUntitled, string label, HashSet<IntPtr> imeOwners,
            List<IntPtr> ranked, List<long> scores)
        {
            foreach (IntPtr h in wins)
            {
                if (!Program.IsWindow(h)) continue;
                long score = long.MinValue;
                bool vis = Program.IsWindowVisible(h);
                bool titled = Program.GetWindowTextLength(h) > 0;
                uint wpid = 0;
                Program.GetWindowThreadProcessId(h, out wpid);
                Program.RECT r;
                if (Program.GetWindowRect(h, out r))
                {
                    long w = r.Right - r.Left, ht = r.Bottom - r.Top;
                    if (w > 0 && ht > 0)
                    {
                        // iconic windows live at -32000 with a stub rect - their size is
                        // meaningless, so the size floor must never filter them out
                        bool iconic = Program.IsIconic(h);
                        if (sizeFloor && !iconic && (w < 560 || ht < 420)) continue;   // login-sized, skip first pass
                        long area = w * ht;
                        long ex = (long)Program.GetWindowLongPtr(h, -20);
                        bool layeredDummy = (ex & 0x80000) != 0 && (ex & 0x20) != 0;   // LAYERED|TRANSPARENT overlay
                        bool oversize = area > screenArea * 11 / 10;
                        // top-level windows (no owner) are the real mains of Chromium apps;
                        // inner frames / owned popups must lose to them
                        bool topLevel = Program.GetWindow(h, 4) == IntPtr.Zero;         // GW_OWNER
                        // login UI titles must never win the contest
                        string t = "";
                        if (titled)
                        {
                            StringBuilder ts = new StringBuilder(200);
                            int tn = Program.GetWindowText(h, ts, 200);
                            if (tn > 0) t = ts.ToString(0, tn);
                        }
                        bool loginLike = t.Contains("登录") || t.Contains("扫码") || t.Contains("切换账号")
                                      || t.Contains("添加账号") || t.Contains("验证")
                                      || t.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0
                                      || t.IndexOf("sign in", StringComparison.OrdinalIgnoreCase) >= 0;
                        // settings/password dialogs embed the app name in their titles
                        // ('MobaXterm Configuration', 'MobaXterm passwords settings') and
                        // would otherwise outrank the real working window
                        bool settingsLike = t.IndexOf("settings", StringComparison.OrdinalIgnoreCase) >= 0
                                      || t.IndexOf("configuration", StringComparison.OrdinalIgnoreCase) >= 0
                                      || t.IndexOf("preferences", StringComparison.OrdinalIgnoreCase) >= 0
                                      || t.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0
                                      || t.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                                      || t.IndexOf("properties", StringComparison.OrdinalIgnoreCase) >= 0
                                      || t.Contains("设置") || t.Contains("配置") || t.Contains("密码") || t.Contains("属性");
                        // EXACT title == dock label is the true main window ('QQ' == QQ).
                        // A mere CONTAINS match is much weaker: auxiliary dialogs carry
                        // the app name while the real session window often does not
                        // (MobaXterm SSH session 'user@host' vs 'MobaXterm Configuration')
                        bool titleExact = label.Length > 0 && t.Equals(label, StringComparison.OrdinalIgnoreCase);
                        bool titleMatch = label.Length > 0 && t.Length > 0
                                       && t.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0;
                        // iconic = minimized from a real interactive session (not just a
                        // hidden shell); IME owner = accepts text input. Both are strong
                        // "this is the live main window" signals for Chromium tray apps
                        bool imeOwner = imeOwners.Contains(h);
                        score = (titleExact ? 2000000L : 0L)
                              + (!titleExact && titleMatch ? 600000L : 0L)
                              + (iconic ? 600000L : 0L)
                              + (imeOwner ? 600000L : 0L)
                              + (wpid == firstPid ? 3000000L : 0L)
                              + (topLevel ? 800000L : 0L)
                              + (vis ? 150000L : 0L) + (titled ? 200000L : 0L)
                              + Math.Min(area, screenArea);
                        if (layeredDummy || oversize) score -= 2000000000L;           // reject dummies
                        if (loginLike) score -= 1900000000L;                          // reject login UI
                        if (settingsLike) score -= 1750000000L;                       // reject settings/password dialogs
                        if (IsJunkTitle(t)) score -= 1850000000L;                     // reject helper/junk titles
                        if (excludeUntitled && !titled) score -= 1800000000L;          // untitled render host loses when a titled window exists
                        // hidden window that does not look like the main window:
                        // demote below anything visible, but keep it usable as a last
                        // resort (fully tray-minimized apps may only have such windows)
                        if (!vis && !titleMatch) score -= 900000L;
                    }
                }
                if (score == long.MinValue) continue;
                int pos = ranked.Count;
                for (int k = 0; k < ranked.Count; k++) if (scores[k] < score) { pos = k; break; }
                ranked.Insert(pos, h);
                scores.Insert(pos, score);
            }
        }

        static void CollectWins(uint pid, string titleContains, List<IntPtr> into, bool includeHidden)
        {
            // pid-mode: UNTITLED windows count too (tray-minimized QQNT has a titleless
            // hidden main window); title only matters for label matching
            bool needTitle = titleContains != null && titleContains.Length > 0;
            Program.GLEnumProc cb = delegate(IntPtr h, IntPtr l)
            {
                try
                {
                    if (!includeHidden && !Program.IsWindowVisible(h)) return true;
                    if (needTitle && Program.GetWindowTextLength(h) == 0) return true;
                    if (pid != 0)
                    {
                        uint wpid;
                        Program.GetWindowThreadProcessId(h, out wpid);
                        if (wpid != pid) return true;
                    }
                    if (needTitle)
                    {
                        StringBuilder sb = new StringBuilder(256);
                        int n = Program.GetWindowText(h, sb, 256);
                        if (n == 0 || sb.ToString(0, n).IndexOf(titleContains, StringComparison.Ordinal) < 0) return true;
                    }
                    into.Add(h);
                }
                catch { }
                return true;
            };
            Program.EnumWindows(cb, IntPtr.Zero);
        }

        // ---------- taskbar: hard hide while we run ----------
        static IntPtr TaskbarHwnd() { return Program.FindWindow("Shell_TrayWnd", null); }

        public static void HideTaskbar()
        {
            try
            {
                IntPtr tb = TaskbarHwnd();
                if (tb == IntPtr.Zero) return;
                Program.APPBARDATA abd = new Program.APPBARDATA();
                abd.cbSize = Marshal.SizeOf(typeof(Program.APPBARDATA));
                abd.hWnd = tb;
                abd.lParam = (IntPtr)Program.ABS_AUTOHIDE;
                Program.SHAppBarMessage(Program.ABM_SETSTATE, ref abd);
                Program.ShowWindow(tb, 0);        // SW_HIDE: never shows while we run
                taskbarHidden = true;
            }
            catch { }
        }

        public static void HideTaskbarNow() { HideTaskbar(); }

        public static void ShowTaskbarNow()
        {
            try
            {
                IntPtr tb = TaskbarHwnd();
                if (tb != IntPtr.Zero) Program.ShowWindow(tb, 5);
            }
            catch { }
        }

        static void ReassertTaskbar()
        {
            if (!taskbarHidden || Program.ExitRequested) return;
            try
            {
                IntPtr tb = TaskbarHwnd();
                if (tb != IntPtr.Zero && Program.IsWindowVisible(tb)) Program.ShowWindow(tb, 0);
            }
            catch { }
        }

        public static void RestoreTaskbarNow()
        {
            if (!taskbarHidden) return;
            taskbarHidden = false;
            try
            {
                IntPtr tb = TaskbarHwnd();
                if (tb == IntPtr.Zero) return;
                Program.APPBARDATA abd = new Program.APPBARDATA();
                abd.cbSize = Marshal.SizeOf(typeof(Program.APPBARDATA));
                abd.hWnd = tb;
                abd.lParam = (IntPtr)2;
                Program.SHAppBarMessage(Program.ABM_SETSTATE, ref abd);
                Program.ShowWindow(tb, 5);
            }
            catch { }
        }

        void Render()
        {
            if (buf == null) return;
            double now = sw.Elapsed.TotalSeconds;
            float dt = (float)(now - lastT);
            if (dt <= 0 || dt > 0.4f) dt = 0.016f;
            lastT = now;

            bool animating = false;
            foreach (DockItem it in display)
                if (Math.Abs(it.Scale - 1f) > 0.004f || now - it.BounceT0 < 1.2) { animating = true; break; }
            bool active = now < activeUntil || animating || mouseX > -1000;
            if (!active && display.Count > 0)
            {
                bool settled = true;
                foreach (DockItem it in display) if (Math.Abs(it.Scale - 1f) > 0.002f) { settled = false; break; }
                if (settled) return;                 // idle: skip frame entirely
                active = true;
            }

            // perf instrumentation: how long a frame really takes on the UI thread,
            // and how much of that is the UpdateLayeredWindow present itself
            double profT0 = sw.Elapsed.TotalMilliseconds;
            using (Graphics g = Graphics.FromImage(buf))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                if (display.Count == 0) { LayeredPainter.Present(this, buf, dockAlpha); return; }
                int W = buf.Width;

                // re-resolve the dragged item every frame: a scan rebuild can insert/
                // remove items around it; if the item itself is gone, cancel the drag
                if (dragging)
                {
                    dragIdx = dragItem != null ? display.IndexOf(dragItem) : -1;
                    if (dragIdx < 0) { dragging = false; dragInsert = -1; dragItem = null; }
                }

                // spring-damper scales: targets referenced to RESTING centers (no feedback loop).
                // semi-implicit integration + exact exponential damping + dt clamp:
                // unconditionally stable even when frames are dropped (GC pauses etc.)
                float dts = dt;
                if (dts > 1f / 30f) dts = 1f / 30f;
                if (dts < 0.005f) dts = 0.005f;
                float velDecay = (float)Math.Exp(-DAMP * dts);
                for (int i = 0; i < display.Count; i++)
                {
                    DockItem it = display[i];
                    float target = 1f;
                    if (dragging && i == dragIdx) target = 1.3f;   // lifted: bigger than hover magnification
                    else if (mouseX > -1000)
                    {
                        float dx = mouseX - it.RestCx;
                        target = 1f + AMP * (float)Math.Exp(-(dx * dx) / (2f * SIGMA * SIGMA));
                    }
                    it.Vel += (target - it.Scale) * STIFF * dts;
                    it.Vel *= velDecay;
                    it.Scale += it.Vel * dts;
                    if (it.Scale < 0.92f) { it.Scale = 0.92f; it.Vel = 0; }
                    float cap = 1f + AMP + 0.08f;
                    if (it.Scale > cap) { it.Scale = cap; it.Vel = 0; }
                }

                // cumulative layout with separator slot. While dragging, the dragged
                // item leaves the flow (it hovers at the mouse) and a gap is reserved
                // at the drop slot so the user sees where the icon will land.
                int slots = display.Count + (sepIndex >= 0 ? 1 : 0);
                float totalW = 0;
                for (int i = 0; i < display.Count; i++) totalW += BASE * display[i].Scale;
                if (dragging && dragIdx >= 0 && dragIdx < display.Count
                    && dragInsert != dragIdx && dragInsert != dragIdx + 1) totalW += 18;
                totalW += GAP * (slots - 1) + (sepIndex >= 0 ? 10 : 0);
                float x0 = (W - totalW) / 2f;
                float x = x0;
                float gapX = -1;
                for (int i = 0; i < display.Count; i++)
                {
                    if (i == sepIndex) x += 10 + GAP;      // separator gap
                    if (dragging && i == dragInsert && i != dragIdx && i != dragIdx + 1) { gapX = x; x += 18; }
                    if (dragging && i == dragIdx) continue;   // floats above, not in the flow
                    float wI = BASE * display[i].Scale;
                    display[i].Cx = x + wI / 2f;
                    x += wI + GAP;
                }
                if (dragging && dragInsert >= display.Count && dragInsert != dragIdx + 1) gapX = x;
                if (dragging && dragIdx >= 0 && dragIdx < display.Count)
                    display[dragIdx].Cx = Math.Max(24, Math.Min(W - 24, mouseX));
                float lastEdge = display.Count > 0 ? display[display.Count - 1].Cx + BASE * display[display.Count - 1].Scale / 2f : x0;

                // tooltip target = icon whose DRAWN center is nearest the mouse (matches
                // what the user is pointing at; threshold-based detection showed the
                // neighbour's name when hovering between icons)
                hoverIdx = -1;
                if (mouseX > -1000)
                {
                    float bestD = BASE * 1.35f;
                    for (int i = 0; i < display.Count; i++)
                    {
                        float d = Math.Abs(mouseX - display[i].Cx);
                        if (d < bestD) { bestD = d; hoverIdx = i; }
                    }
                }

                // panel
                float pad = 20;
                float px0 = x0 - pad;
                float pw = (lastEdge - x0) + pad * 2;
                int py = FORMH - PANELH - BOTTOMM;
                using (GraphicsPath panel = WidgetPaint.RoundRect((int)px0, py, (int)pw, PANELH, 20))
                {
                    using (LinearGradientBrush lgb = new LinearGradientBrush(
                        new Rectangle((int)px0, py, (int)pw, PANELH),
                        Color.FromArgb(196, 42, 50, 74), Color.FromArgb(222, 11, 15, 29), 90f))
                        g.FillPath(lgb, panel);
                    using (Pen pn = new Pen(Color.FromArgb(42, 105, 160, 255), 1.1f))
                        g.DrawPath(pn, panel);
                    using (Pen hl = new Pen(Color.FromArgb(34, 165, 205, 255), 1f))
                        g.DrawLine(hl, px0 + 16, py + 1.6f, px0 + pw - 16, py + 1.6f);
                }

                // separator between pinned and running
                if (sepIndex >= 0 && sepIndex < display.Count)
                {
                    float sx = display[sepIndex].Cx - BASE * 1.78f / 2f - GAP / 2f - 5;
                    using (Pen sp = new Pen(Color.FromArgb(70, 120, 150, 185), 1.2f))
                        g.DrawLine(sp, sx, py + 14, sx, py + PANELH - 14);
                }

                // drag drop indicator: glowing bar at the reserved insertion gap
                if (dragging && gapX >= 0)
                {
                    using (Pen gp = new Pen(Color.FromArgb(200, 120, 205, 255), 3.2f))
                    {
                        gp.StartCap = LineCap.Round; gp.EndCap = LineCap.Round;
                        g.DrawLine(gp, gapX, py + 12, gapX, py + PANELH - 12);
                    }
                }

                float baseline = py + PANELH - 11;
                // pass 0 draws normal items, pass 1 draws the dragged icon last so it
                // floats above its neighbours
                for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < display.Count; i++)
                {
                    bool isDrag = dragging && i == dragIdx;
                    if (pass == 0 ? isDrag : !isDrag) continue;
                    DockItem it = display[i];
                    float s = it.Scale;
                    float size = BASE * s;
                    float cx = it.Cx;

                    float yBounce = 0;
                    double bt = now - it.BounceT0;
                    if (bt >= 0 && bt < 1.15)
                        yBounce = -(float)(30 * Math.Abs(Math.Sin(2 * Math.PI * 2.1 * bt)) * Math.Exp(-2.4 * bt));
                    float iconBottom = baseline + yBounce;

                    float shW = size * 1.05f;
                    g.DrawImage(shadowSpr, new RectangleF(cx - shW / 2f, baseline - 6, shW, shW * 0.24f));

                    if (s > 1.12f)
                    {
                        using (GraphicsPath gp = new GraphicsPath())
                        {
                            gp.AddEllipse(cx - size / 2, iconBottom - size * 0.4f, size, size * 0.62f);
                            using (PathGradientBrush pg = new PathGradientBrush(gp))
                            {
                                pg.CenterColor = Color.FromArgb((int)(52 * (s - 1f) / AMP), 100, 215, 255);
                                pg.SurroundColors = new Color[] { Color.FromArgb(0, 100, 215, 255) };
                                g.FillPath(pg, gp);
                            }
                        }
                    }

                    if (it.IsGear || it.IsSearch || it.IsDesk)
                    {
                        // settings / search tile: rounded square + glyph
                        float tile = size;
                        using (GraphicsPath tp = WidgetPaint.RoundRect((int)(cx - tile / 2), (int)(iconBottom - tile), (int)tile, (int)tile, (int)(tile * 0.22)))
                        {
                            using (SolidBrush tb = new SolidBrush(Color.FromArgb(120, 34, 44, 68)))
                                g.FillPath(tb, tp);
                            using (Pen tpn = new Pen(Color.FromArgb(120, 110, 190, 240), 1f))
                                g.DrawPath(tpn, tp);
                        }
                        try
                        {
                            if (it.IsGear)
                            {
                                using (Font gf = new Font("Segoe UI Symbol", Math.Max(10f, size * 0.52f)))
                                using (SolidBrush gb = new SolidBrush(Color.FromArgb(235, 225, 238, 252)))
                                {
                                    string gearStr = "\u2699";
                                    SizeF gz = g.MeasureString(gearStr, gf);
                                    g.DrawString(gearStr, gf, gb, cx - gz.Width / 2f, iconBottom - size + (size - gz.Height) / 2f);
                                }
                            }
                            else if (it.IsSearch)
                            {
                                // magnifier: ring + handle, font-independent
                                float r = size * 0.28f;
                                float ccx = cx - size * 0.07f;
                                float ccy = iconBottom - size + size * 0.44f;
                                using (Pen mp = new Pen(Color.FromArgb(235, 225, 238, 252), Math.Max(2f, size * 0.09f)))
                                {
                                    g.DrawEllipse(mp, ccx - r, ccy - r, r * 2, r * 2);
                                    g.DrawLine(mp, ccx + r * 0.72f, ccy + r * 0.72f, ccx + r * 1.5f, ccy + r * 1.5f);
                                }
                            }
                            else
                            {
                                // 桌面: house glyph - roof, body, door
                                float hw = size * 0.60f;
                                float hx = cx - hw / 2;
                                float roofTop = iconBottom - size + size * 0.14f;
                                float bodyTop = iconBottom - size + size * 0.46f;
                                using (Pen hp = new Pen(Color.FromArgb(235, 225, 238, 252), Math.Max(2f, size * 0.085f)))
                                {
                                    // roof triangle (chimney-less, slightly rounded joins)
                                    hp.StartCap = LineCap.Round; hp.EndCap = LineCap.Round;
                                    g.DrawLine(hp, cx - hw / 2 - size * 0.04f, bodyTop, cx, roofTop);
                                    g.DrawLine(hp, cx, roofTop, cx + hw / 2 + size * 0.04f, bodyTop);
                                    // body
                                    g.DrawRectangle(hp, hx, bodyTop, hw, size * 0.34f);
                                    // door
                                    g.DrawRectangle(hp, cx - size * 0.07f, bodyTop + size * 0.14f, size * 0.14f, size * 0.20f);
                                }
                            }
                        }
                        catch { }
                    }
                    else if (it.Icon != null)
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(it.Icon, new RectangleF(cx - size / 2f, iconBottom - size, size, size));
                    }
                    else
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 70, 205, 255)))
                            g.FillEllipse(b, cx - size / 2f, iconBottom - size, size, size);
                    }

                    if (!it.IsGear && !it.IsSearch && !it.IsDesk)
                        using (SolidBrush db = new SolidBrush(it.Running ? Color.FromArgb(235, 235, 242, 252) : Color.FromArgb(58, 130, 145, 165)))
                            g.FillEllipse(db, cx - 2.2f, py + PANELH - 8, 4.4f, 4.4f);
                }

                if (hoverIdx >= 0 && hoverIdx < display.Count)
                {
                    DockItem it = display[hoverIdx];
                    float top = baseline - BASE * it.Scale - 34;
                    using (Font f = new Font("Microsoft YaHei UI", 9.5f))
                    {
                        string txt = it.Label + (it.Running ? "  ·  运行中" : "");
                        SizeF sz = g.MeasureString(txt, f);
                        float tx = Math.Max(6, Math.Min(W - sz.Width - 22, it.Cx - sz.Width / 2f));
                        using (GraphicsPath tp = WidgetPaint.RoundRect((int)tx, (int)top, (int)sz.Width + 18, 24, 9))
                        {
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 13, 18, 33))) g.FillPath(b, tp);
                            using (Pen pn = new Pen(Color.FromArgb(80, 95, 220, 255), 1f)) g.DrawPath(pn, tp);
                            using (SolidBrush tb = new SolidBrush(Color.FromArgb(238, 235, 245, 255)))
                                g.DrawString(txt, f, tb, tx + 9, top + 4);
                        }
                    }
                }
            }
            double pDraw = sw.Elapsed.TotalMilliseconds - profT0;
            LayeredPainter.Present(this, buf, dockAlpha);
            double pPresent = sw.Elapsed.TotalMilliseconds - profT0 - pDraw;
            profN++; profDrawSum += pDraw; profPresentSum += pPresent;
            if (pPresent > profPresentMax) profPresentMax = pPresent;
            if (profN >= 60)
            {
                double avgFrame = (profDrawSum + profPresentSum) / profN;
                // ADAPT for the target machine: weak GDI machines halve the dock rate
                dockSlowDiv = avgFrame > 12.0 ? 2 : 1;
                Program.Log(string.Format("[DOCK] perf n={0} drawAvg={1:F1}ms presentAvg={2:F1}ms presentMax={3:F1}ms window={4:F1}s gc0={5} gc1={6} gc2={7} slowDiv={8}",
                    profN, profDrawSum / profN, profPresentSum / profN, profPresentMax, sw.Elapsed.TotalSeconds - profWinStart,
                    GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), dockSlowDiv));
                profN = 0; profDrawSum = 0; profPresentSum = 0; profPresentMax = 0; profWinStart = sw.Elapsed.TotalSeconds;
            }
        }
    }
}
