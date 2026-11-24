using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HudWall
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool preview = Array.IndexOf(args, "--preview") >= 0;
            bool serve = Array.IndexOf(args, "--serve") >= 0;

            if(Array.IndexOf(args,"--wallpaper-size-test")>=0){DesktopSizing.Test(args[Array.IndexOf(args,"--wallpaper-size-test")+1]);return;}
            if(Array.IndexOf(args,"--taskbar-window-fixture")>=0){Application.EnableVisualStyles();using var fixture=new Form{Text="HUD app switching test",Width=430,Height=120,StartPosition=FormStartPosition.CenterScreen};Application.Run(fixture);return;}
            if(Array.IndexOf(args,"--restore-taskbar")>=0){TaskbarRecovery.RequestRestore();return;}
            int probe=Array.IndexOf(args,"--taskbar-test");if(probe>=0&&probe+1<args.Length){TaskbarProbe.Run(Path.GetFullPath(args[probe+1]),Array.IndexOf(args,"--hold")>=0);return;}
            int status=Array.IndexOf(args,"--taskbar-status");if(status>=0&&status+1<args.Length){TaskbarProbe.Status(args[status+1]);return;}
            int watchdog=Array.IndexOf(args,"--taskbar-watchdog");
            if(watchdog>=0&&watchdog+2<args.Length&&int.TryParse(args[watchdog+1],out int owner)){TaskbarRecovery.Watch(owner,args[watchdog+2]);return;}
            if (serve)
            {
                RunFeed();
                return;
            }

            int snapshot = Array.IndexOf(args, "--organizer-snapshot");
            if (snapshot >= 0 && snapshot + 1 < args.Length)
            {
                OrganizerSnapshot(args[snapshot + 1]);
                return;
            }

            using var mutex = new Mutex(true, "HudWall.Native.SingleInstance", out bool created);
            if (!created) { Log("already running"); return; }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (_, e) => Log("ui exception: " + e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("fatal: " + e.ExceptionObject);

            // A preview window is for trying a build; it leaves the installed copy's startup entry alone.
            if (!preview) try { var startup=new StartupSettings(Environment.GetFolderPath(Environment.SpecialFolder.Startup),AppContext.BaseDirectory);startup.SetEnabled(true); }catch(Exception ex){Log("startup registration: "+ex.Message);}
            try { Application.Run(new HudForm(preview)); }
            catch (Exception ex) { Log("run failed: " + ex); }
        }

        /// <summary>Headless mode: JSON feed only, no window (Wallpaper Engine / Lively).</summary>
        private static void RunFeed()
        {
            using var mutex = new Mutex(true, "HudWall.Feed.SingleInstance", out bool created);
            if (!created) { Log("feed already running"); return; }

            try
            {
                var server = new FeedServer(8787);
                server.Start();

                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HudWall");
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "feed.port"), server.Port.ToString());
                }
                catch { }

                var stop = new ManualResetEventSlim(false);
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
                AppDomain.CurrentDomain.ProcessExit += (_, __) => stop.Set();
                stop.Wait();
                server.Dispose();
            }
            catch (Exception ex)
            {
                Log("feed failed: " + ex);
            }
        }

        /// <summary>Developer aid: renders the desktop organizer's frames and launcher to PNGs in the current HUD theme.</summary>
        private static void OrganizerSnapshot(string folder)
        {
            try
            {
                var palette = DeskNest.Models.HudPalette.From(ThemeSettings.Load(Path.Combine(AppContext.BaseDirectory, "assets")));
                var organizer = DeskNest.App.Start(new LogOnlyHost(), palette, Path.GetFullPath(folder)).GetAwaiter().GetResult();
                organizer.Stopped.Wait(TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                Log("organizer snapshot failed: " + ex);
            }
        }

        private sealed class LogOnlyHost : DeskNest.IOrganizerHost
        {
            public void Notify(string title, string text) => Log("organizer: " + title + " / " + text);
        }

        public static void Log(string message)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HudWall");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "hudwall.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
            }
            catch { }
        }
    }

    internal sealed partial class HudForm : Form
    {
        private readonly bool _preview;
        private WebView2 _web;
        private StatsCollector _stats;
        private AudioCapture _audio;
        private System.Windows.Forms.Timer _statsTimer, _audioTimer, _watchTimer;
        private NotifyIcon _tray;
        private bool _attached;

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        public HudForm(bool preview)
        {
            _preview = preview;
            _manualPause=_performance.ManualPause;
            Text = "HUD Live";
            using (var iconStream = typeof(HudForm).Assembly.GetManifestResourceStream("HudWall.hud.ico"))
            { using var icon = new Icon(iconStream); Icon = (Icon)icon.Clone(); }
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            ShowInTaskbar = preview;
            var vs = SystemInformation.VirtualScreen;
            Bounds = preview ? new Rectangle(vs.X + 80, vs.Y + 80, 1600, 900) : vs;

            _web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
            Controls.Add(_web);

            _tray = new NotifyIcon
            {
                Icon = Icon,
                Text = "HUD Live wallpaper",
                Visible = true,
                ContextMenuStrip = BuildMenu(),
            };

            InitializePerformance();
            Load += async (_, __) => { InitializeOrganizer(); await InitAsync(); InitializeVoice(); InitializeHudTaskbar(); _=InitializeGodsEyeAsync(); };
            FormClosing += (_, __) => { try { _tray.Visible = false; } catch { } };
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("HUD Live — native wallpaper", null, (_, __) => { });
            menu.Items[0].Enabled = false;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Reload page", null, (_, __) => { try { _web.CoreWebView2?.Reload(); } catch { } });
            menu.Items.Add("Color settings...", null, (_, __) => {
                using var dialog = new ColorSettings(Path.Combine(AppContext.BaseDirectory, "assets"));
                if (dialog.ShowDialog() == DialogResult.OK) ApplySettings();
            });
            menu.Items.Add(new ToolStripSeparator());
            string assets = Path.Combine(AppContext.BaseDirectory, "assets");
            AddPersonalizationMenu(menu);
            AddMonitorMenu(menu);
            AddTransparencyMenu(menu);
            AddAppearanceMenu(menu);
            AddCommandCentreMenu(menu);
            AddHudTaskbarMenu(menu); AddGodsEyeMenu(menu);
            AddLayoutMenu(menu);
            AddVoiceMenu(menu);
            AddAiMenu(menu);
            AddOrganizerMenu(menu);
            string unit = TemperatureSettings.Load(assets);
            try { TemperatureSettings.Save(assets, unit); }
            catch (Exception ex) { Program.Log("temperature settings: " + ex.Message); }
            var temperature = new ToolStripMenuItem("Temperature units");
            var celsius = new ToolStripMenuItem("Celsius (\u00b0C)") { Checked = unit == "C" };
            var fahrenheit = new ToolStripMenuItem("Fahrenheit (\u00b0F)") { Checked = unit == "F" };
            void SelectUnit(string selected)
            {
                try
                {
                    TemperatureSettings.Save(assets, selected);
                    celsius.Checked = selected == "C";
                    fahrenheit.Checked = selected == "F";
                    ApplySettings();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Could not save temperature setting"); }
            }
            celsius.Click += (_, __) => SelectUnit("C");
            fahrenheit.Click += (_, __) => SelectUnit("F");
            temperature.DropDownItems.Add(celsius);
            temperature.DropDownItems.Add(fahrenheit);
            menu.Items.Add(temperature);
            string clockFormat = ClockSettings.Load(assets);
            var clock = new ToolStripMenuItem("Clock format");
            var hour12 = new ToolStripMenuItem("12-hour (AM/PM)") { Checked = clockFormat == "12" };
            var hour24 = new ToolStripMenuItem("24-hour") { Checked = clockFormat == "24" };
            void SelectClock(string selected)
            {
                try
                {
                    ClockSettings.Save(assets, selected);
                    hour12.Checked = selected == "12";
                    hour24.Checked = selected == "24";
                    ApplySettings();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Could not save clock format"); }
            }
            hour12.Click += (_, __) => SelectClock("12");
            hour24.Click += (_, __) => SelectClock("24");
            clock.DropDownItems.Add(hour12);
            clock.DropDownItems.Add(hour24);
            menu.Items.Add(clock);
            menu.Items.Add(new ToolStripSeparator());
            var startupSettings = new StartupSettings(Environment.GetFolderPath(Environment.SpecialFolder.Startup), AppContext.BaseDirectory);
            var startup = new ToolStripMenuItem("Starts automatically with Windows") { Checked = startupSettings.IsEnabled(), Enabled = false };
            menu.Opening += (_, __) => startup.Checked = startupSettings.IsEnabled();
            menu.Items.Add(startup);
            menu.Items.Add(new ToolStripSeparator());
            AddPerformanceMenu(menu);
            menu.Items.Add("Exit", null, (_, __) => Close());
            return menu;
        }

        private async void ApplySettings()
        {
            try
            {
                var core = _web?.CoreWebView2;
                if (core == null) return;
                string assets = Path.Combine(AppContext.BaseDirectory, "assets");
                var colors = new System.Collections.Generic.Dictionary<string, string>();
                string path = Path.Combine(assets, "colors.json");
                if (File.Exists(path))
                {
                    try { colors = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(SettingsStorage.Read(path)) ?? colors; }
                    catch (System.Text.Json.JsonException ex) { Program.Log("invalid color settings: " + ex.Message); }
                }
                EvaluatePlayback();
                PushOrganizerTheme();
                _hudTaskbars?.Refresh();
                string json = System.Text.Json.JsonSerializer.Serialize(new {
                    godsEye=GodsEyePacket(), taskbar=new{Enabled=HudTaskbarSettings.Load(assets).Enabled,reserved=_hudTaskbars?.Bars.Select(b=>new{id=b.DeviceName,height=b.Height}).ToArray()}, commandCentre=CommandCentreSettings.Load(assets), appearance = AppearanceSettings.Load(assets), colors, temperatureUnit = TemperatureSettings.Load(assets), clockFormat = ClockSettings.Load(assets), audioSensitivity = AudioSettings.Load(assets), monitors = _monitorStates
                });
                await core.ExecuteScriptAsync("window.applyHudSettings && window.applyHudSettings(" + json + ")");
                ApplyPlayback();
            }
            catch (Exception ex) { Program.Log("apply settings failed: " + ex); }
        }
        private async System.Threading.Tasks.Task InitAsync()
        {
            try
            {
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HudWall", "WebView2");
                Directory.CreateDirectory(userData);

                string browserArgs =
                    "--disable-features=CalculateNativeWinOcclusion,msWebOOUI,msPdfOOUI " +
                    "--autoplay-policy=no-user-gesture-required " +
                    "--disable-background-timer-throttling --disable-renderer-backgrounding " +
                    "--disable-backgrounding-occluded-windows " +
                    "--force-device-scale-factor=1";

                var env = await CoreWebView2Environment.CreateAsync(null, userData,
                    new CoreWebView2EnvironmentOptions(browserArgs));
                await _web.EnsureCoreWebView2Async(env);

                var core = _web.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDevToolsEnabled = true;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.IsSwipeNavigationEnabled = false;

                string assets = Path.Combine(AppContext.BaseDirectory, "assets");
                if (!File.Exists(Path.Combine(assets, "index.html")))
                    throw new FileNotFoundException("The assets folder must be next to HudWall.exe.");

                core.SetVirtualHostNameToFolderMapping("hud.local", assets, CoreWebView2HostResourceAccessKind.Allow);
                core.WebMessageReceived += OnPageMessage;
                await core.AddScriptToExecuteOnDocumentCreatedAsync(Shim.Js);

                core.NavigationCompleted += (_, e) =>
                {
                    Program.Log("navigation completed ok=" + e.IsSuccess + " " + e.WebErrorStatus);
                    if (e.IsSuccess) { ApplySettings(); SetVoiceStatus(_voiceStatus); }
                    if (!_preview) PinToDesktop(false);
                };

                core.Navigate("https://hud.local/index.html");

                _stats = new StatsCollector();
                _audio = new AudioCapture();

                _statsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                _statsTimer.Tick += (_, __) => PushStats();
                _statsTimer.Start();

                _audioTimer = new System.Windows.Forms.Timer { Interval = 50 };
                _audioTimer.Tick += (_, __) => PushAudio();
                _audioTimer.Start();

                _watchTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                _watchTimer.Tick += (_, __) =>
                {
                    DesktopSizing.Ensure(Handle);
                    if (_preview || !_attached) return;
                    if (GetParent(Handle) == IntPtr.Zero)
                    {
                        Program.Log("lost desktop parent, re-attaching");
                        PinToDesktop(true);
                    }
                };
                _watchTimer.Start();
                EvaluatePlayback();
                ApplyPlayback();
            }
            catch (Exception ex)
            {
                Program.Log("init failed: " + ex);
            }
        }

        private void PushStats()
        {
            try
            {
                var core = _web?.CoreWebView2;
                if (core == null || _stats == null) return;
                core.PostWebMessageAsJson("{\"hud\":\"stats\",\"data\":" + _stats.SnapshotJson() + "}");
            }
            catch (Exception ex) { Program.Log("push stats: " + ex.Message); }
        }

        private void PushAudio()
        {
            try
            {
                var core = _web?.CoreWebView2;
                if (core == null || _audio == null) return;
                core.PostWebMessageAsJson("{\"hud\":\"audio\",\"data\":" + _audio.SnapshotJson() + "}");
            }
            catch { }
        }

        private void PinToDesktop(bool force)
        {
            try
            {
                if (!_attached || force)
                {
                    var parent = DesktopHost.Attach(Handle, SystemInformation.VirtualScreen, out string detail);
                    Program.Log("attach " + detail);
                    _attached = parent != IntPtr.Zero;
                }
                DesktopHost.HideFromAltTab(Handle);
            }
            catch (Exception ex) { Program.Log("pin failed: " + ex.Message); }
        }
    }
}
