using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HudWall;
using NAudio.Wave;
using System.Drawing;

int failures = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Test(string name, Action action) {
    try { action(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
}
void WithSettingsFolder(Action<string> action) {
    string folder = Directory.CreateTempSubdirectory("HudWall-settings-tests-").FullName;
    try { action(folder); }
    finally {
        if (!Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new Exception("Unexpected test cleanup path");
        Directory.Delete(folder, recursive: true);
    }
}

Test("Audio follows default output and rejects retired-session callbacks", () => {
    string current = "speakers";
    var sessions = new List<FakeLoopbackSession>();
    using var audio = new AudioCapture(() => current, id => {
        var session = new FakeLoopbackSession(id); sessions.Add(session); return session;
    }, startTimer: false);
    audio.PollDevice(); sessions[0].SendSamples();
    using (var json = JsonDocument.Parse(audio.SnapshotJson()))
        Check(json.RootElement.GetProperty("peak").GetDouble() > 0, "Initial capture did not publish");
    audio.PollDevice(); Check(sessions.Count == 1, "Unchanged output restarted capture");
    current = "headphones"; audio.PollDevice();
    Check(sessions.Count == 2 && sessions[0].Disposed, "Old output was not replaced and disposed");
    sessions[0].Stop(); sessions[0].SendSamples();
    using (var json = JsonDocument.Parse(audio.SnapshotJson())) {
        Check(json.RootElement.GetProperty("ok").GetBoolean(), "Old callback invalidated new capture");
        Check(json.RootElement.GetProperty("device").GetString() == "headphones", "Wrong output captured");
        Check(json.RootElement.GetProperty("peak").GetDouble() == 0, "Old samples survived device switch");
    }
    sessions[1].SendSamples();
    using (var json = JsonDocument.Parse(audio.SnapshotJson()))
        Check(json.RootElement.GetProperty("peak").GetDouble() > 0, "Replacement did not publish");
    audio.Dispose(); audio.PollDevice();
    Check(sessions.Count == 2 && sessions[1].Disposed, "Disposal allowed another capture");
});
Test("Audio recovers from failed starts, stopped streams and missing endpoints", () => {
    bool unavailable = false; int opened = 0; FakeLoopbackSession latest = null;
    using var audio = new AudioCapture(() => unavailable ? throw new InvalidOperationException("No output") : "speakers",
        id => latest = new FakeLoopbackSession(id) { FailStart = ++opened == 1 }, startTimer: false);
    audio.PollDevice(); Check(latest.Disposed, "Failed start leaked capture");
    audio.PollDevice(); Check(opened == 2 && !latest.Disposed, "Failed start was not retried");
    latest.Stop(); audio.PollDevice(); Check(opened == 3, "Stopped stream was not restarted");
    unavailable = true; audio.PollDevice(); Check(latest.Disposed, "Unavailable output retained capture");
    unavailable = false; audio.PollDevice(); Check(opened == 4, "Output recovery did not resume capture");
});
Test("Capture disposal lets recording callbacks finish without deadlocking", () => {
    string current = "speakers"; FakeLoopbackSession first = null;
    using var audio = new AudioCapture(() => current, id => {
        var session = new FakeLoopbackSession(id);
        if (first == null) first = session;
        return session;
    }, startTimer: false);
    audio.PollDevice(); bool completed = false;
    first.OnDispose = () => completed = Task.Run(first.Stop).Wait(TimeSpan.FromSeconds(2));
    current = "headphones"; audio.PollDevice();
    Check(completed, "Disposal blocked recording callbacks");
});
Test("Feed disposal immediately releases its fallback port", () => {
    using var occupied = new TcpListener(IPAddress.Loopback, 0);
    occupied.Start(); int requested = ((IPEndPoint)occupied.LocalEndpoint).Port;
    using var server = new FeedServer(requested, new StatsCollector(),
        new AudioCapture(() => "fixture", id => new FakeLoopbackSession(id), startTimer: false));
    server.Start(); int fallback = server.Port;
    Check(fallback != requested, "Fallback was not selected");
    using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) }) {
        string health = http.GetStringAsync($"http://127.0.0.1:{fallback}/health").GetAwaiter().GetResult();
        using var json = JsonDocument.Parse(health);
        Check(json.RootElement.GetProperty("port").GetInt32() == fallback, "Fallback did not serve health");
    }
    server.Dispose();
    using var replacement = new TcpListener(IPAddress.Loopback, fallback);
    replacement.Start();
});
Test("Taskbars hide for fullscreen only on covered monitors", () => {
    var left = new Rectangle(-1920, 0, 1920, 1080);
    var right = new Rectangle(0, 0, 2560, 1440);
    Check(TaskbarVisibility.IsFullscreen(left, left, false, "Game", false), "Fullscreen game did not hide its taskbar");
    Check(!TaskbarVisibility.IsFullscreen(right, left, false, "Game", false), "Game hid another monitor's taskbar");
    Check(!TaskbarVisibility.IsFullscreen(right, new Rectangle(0, 0, 2560, 1378), false, "Browser", true), "Normal maximized app hid taskbar");
    Check(!TaskbarVisibility.IsFullscreen(right, right, false, "Browser", true), "Captioned maximized app was mistaken for fullscreen");
    Check(TaskbarVisibility.IsFullscreen(right, right, false, "Browser", false), "Fullscreen video did not hide taskbar");
});
Test("Wallpaper and shell windows never hide HUD taskbars", () => {
    var monitor = new Rectangle(0, 0, 1920, 1080);
    Check(!TaskbarVisibility.IsFullscreen(monitor, monitor, true, "WindowsForms10.Window", false), "HUD wallpaper hid its own taskbar");
    foreach (var name in new[] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
        Check(!TaskbarVisibility.IsFullscreen(monitor, monitor, false, name, false), "Shell window hid the taskbar: " + name);
});
Test("Desktop membership changes trigger recovery without reacting to unavailable APIs", () => {
    Check(!TaskbarVisibility.NeedsDesktopRebuild(new bool?[] { true, true }), "Stable desktop rebuilt taskbars");
    Check(TaskbarVisibility.NeedsDesktopRebuild(new bool?[] { true, false }), "Taskbar left on another desktop was ignored");
    Check(!TaskbarVisibility.NeedsDesktopRebuild(new bool?[] { null, true }), "Temporary API failure destroyed taskbars");
    Check(!TaskbarVisibility.NeedsDesktopRebuild(Array.Empty<bool?>()), "Empty set triggered endless desktop recovery");
});
Test("Invalid audio setting types recover a valid backup or use safe defaults", () => WithSettingsFolder(folder => {
    string path = Path.Combine(folder, "audio.json");
    foreach (string invalid in new[] { "[]", "null", "\"text\"", "{\"sensitivity\":\"loud\"}", "{\"sensitivity\":null}", "{\"sensitivity\":true}", "{\"sensitivity\":1.5}", "{\"sensitivity\":1e20}" }) {
        File.WriteAllText(path, invalid);
        Check(AudioSettings.Load(folder) == 100, "Invalid audio setting did not use default: " + invalid);
    }
    File.WriteAllText(path + ".bak", "{\"sensitivity\":150}");
    File.WriteAllText(path, "{\"sensitivity\":\"loud\"}");
    Check(AudioSettings.Load(folder) == 150, "Valid backup was not recovered for wrong-type primary");
    File.WriteAllText(path + ".bak", "[]");
    Check(AudioSettings.Load(folder) == 100, "Wrong-type backup did not use default");
    AudioSettings.Save(folder, 999);
    Check(AudioSettings.Load(folder) == 300, "Valid saved setting was not clamped");
    File.WriteAllText(path, "{}");
    Check(AudioSettings.Load(folder) == 100, "Missing property changed default behavior");
}));
Test("Empty taskbar pins survive save, restart and legacy migration", () => WithSettingsFolder(folder => {
    var settings = new HudTaskbarSettings { Enabled = true };
    int imports = 0;
    Check(settings.InitializePins(() => { imports++; return new[] { "browser.lnk" }; }, () => new[] { "explorer.exe" }), "First run was not initialized");
    Check(imports == 1 && settings.Pins.SequenceEqual(new[] { "browser.lnk" }), "Windows pins were not imported");
    settings.Pins.Clear(); settings.Save(folder);
    var reloaded = HudTaskbarSettings.Load(folder);
    Check(reloaded.Enabled && reloaded.PinsInitialized && reloaded.Pins.Count == 0, "Empty pin preference lost on disk");
    Check(!reloaded.InitializePins(() => throw new Exception("Unexpected reimport"), () => throw new Exception("Unexpected defaults")), "Explicit empty list was initialized again");
    File.WriteAllText(Path.Combine(folder, "taskbar.json"), "{\"Enabled\":true,\"Pins\":[]}");
    var legacy = HudTaskbarSettings.Load(folder);
    Check(!legacy.InitializePins(() => throw new Exception("Legacy pins reimported"), () => throw new Exception("Legacy defaults inserted")), "Legacy empty list was not preserved");
    Check(legacy.Pins.Count == 0, "Legacy empty pins changed");
}));
Test("Fresh taskbar defaults import once and existing nonempty pins are preserved", () => WithSettingsFolder(folder => {
    File.Copy(Path.Combine(AppContext.BaseDirectory, "taskbar-defaults.json"), Path.Combine(folder, "taskbar.json"));
    var fresh = HudTaskbarSettings.Load(folder);
    Check(!fresh.PinsInitialized, "Shipped defaults suppress first-run import");
    fresh.InitializePins(() => Array.Empty<string>(), () => new[] { "explorer.exe", "notepad.exe" });
    fresh.Save(folder);
    var reloaded = HudTaskbarSettings.Load(folder);
    Check(reloaded.PinsInitialized && reloaded.Pins.Count == 2, "First-run fallback did not persist");
    File.WriteAllText(Path.Combine(folder, "taskbar.json"), "{\"Pins\":[\"custom.lnk\"]}");
    var legacy = HudTaskbarSettings.Load(folder);
    legacy.InitializePins(() => throw new Exception("Existing pins replaced"), () => throw new Exception("Existing pins replaced"));
    Check(legacy.Pins.SequenceEqual(new[] { "custom.lnk" }), "Existing pin list changed");
}));
Test("Fullscreen video stays protected when focus moves to another monitor", () => {
    var left = new Rectangle(-1920, 0, 1920, 1080);
    var right = new Rectangle(0, 0, 2560, 1440);
    var video = new TaskbarWindowState(left, false, "VideoPlayer", false);
    var normalRight = new TaskbarWindowState(new Rectangle(100, 100, 1000, 700), false, "Editor", false);
    Check(TaskbarVisibility.HasFullscreenOnMonitor(left, new[] { normalRight, video }), "Focus on other monitor exposed fullscreen taskbar");
    Check(!TaskbarVisibility.HasFullscreenOnMonitor(right, new[] { normalRight, video }), "Fullscreen video hid taskbar on unrelated monitor");
    var maximizedRight = new TaskbarWindowState(TaskbarVisibility.VisibleBounds(new Rectangle(-8, -8, 2576, 1456), right, true), false, "Editor", true);
    Check(TaskbarVisibility.HasFullscreenOnMonitor(left, new[] { maximizedRight, video }), "Invisible maximized-window border exposed fullscreen taskbar on neighbor monitor");
    var normalLeft = new TaskbarWindowState(new Rectangle(-1800, 100, 1000, 700), false, "Editor", false);
    Check(!TaskbarVisibility.HasFullscreenOnMonitor(left, new[] { normalLeft, video }), "Occluded fullscreen app hid taskbar over normal app");
    Check(!TaskbarVisibility.HasFullscreenOnMonitor(left, new[] { video with { Minimized = true } }), "Minimized video hid taskbar");
    Check(TaskbarVisibility.HasFullscreenOnMonitor(left, new[] { video with { OwnProcess = true }, video }), "HUD wallpaper masked fullscreen detection");
});
Test("Taskbar bounds meet physical monitor bottoms at mixed DPI and negative origins", () => {
    foreach (var monitor in new[] { new Rectangle(0, 0, 2560, 1440), new Rectangle(-1920, 0, 1920, 1080), new Rectangle(0, -1440, 2560, 1440) }) {
        foreach (int dpi in new[] { 96, 120, 144, 192 }) {
            var target = TaskbarLayout.BottomBounds(monitor, TaskbarLayout.HeightForDpi(dpi));
            Check(target.Bottom == monitor.Bottom && target.Left == monitor.Left && target.Width == monitor.Width, "Taskbar drifted from physical edge");
            Check(target.Height == Math.Max(54, (int)Math.Round(62 * dpi / 96d)), "DPI height incorrect");
            var staleShellRectangle = new Rectangle(monitor.Left, monitor.Bottom - 48 - target.Height, monitor.Width, target.Height);
            Check(target.Bottom != staleShellRectangle.Bottom, "Taskbar retained the old shell gap");
        }
    }
});
Console.WriteLine(failures == 0 ? "All JARVIS recovery tests passed" : $"{failures} tests failed");
return failures == 0 ? 0 : 1;

internal sealed class FakeLoopbackSession(string name) : ILoopbackSession
{
    public string DeviceName => name;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;
    public bool Disposed { get; private set; }
    public bool FailStart { get; init; }
    public Action OnDispose { get; set; }
    public void StartRecording() { if (FailStart) throw new InvalidOperationException("Start failed"); }
    public void Dispose() { Disposed = true; OnDispose?.Invoke(); }
    public void Stop() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
    public void SendSamples() {
        var samples = new float[2048]; Array.Fill(samples, .5f);
        var buffer = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, buffer, 0, buffer.Length);
        DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, buffer.Length));
    }
}

namespace HudWall {
    // Feed telemetry is irrelevant to socket lifetime. Keep tests off real GPU polling.
    internal sealed class StatsCollector : IDisposable {
        public string SnapshotJson() => "{}";
        public void Dispose() { }
    }
    internal static class Program {
        public static void Log(string message) { }
    }
}
