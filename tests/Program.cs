using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using HudWall;
using NAudio.Wave;
using System.Net;
using System.Net.Sockets;

if (args.FirstOrDefault() == "--fixture") {
    if (args[1] == "hang") { File.WriteAllText(args[2], Environment.ProcessId.ToString()); Thread.Sleep(30000); }
    if (args[1] == "pipes") { Console.Error.Write(new string('e', 200000)); Console.WriteLine("42, 55, 100, 200"); }
    if (args[1] == "fail") { Console.WriteLine("42, 55, 100, 200"); return 1; }
    return 0;
}

int failures = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Test(string name, Action action) {
    try { action(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
}
var tempRoot = Path.Combine(Path.GetTempPath(), "HudWall-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
string Folder(string name) { var path = Path.Combine(tempRoot, name); Directory.CreateDirectory(path); return path; }
string fixtureExe = Environment.ProcessPath;
string fixturePrefix = Path.GetFileNameWithoutExtension(fixtureExe).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
    ? "\"" + Assembly.GetExecutingAssembly().Location + "\" " : "";

try {
    Test("Audio follows default output changes and ignores callbacks from retired sessions", () => {
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
            Check(json.RootElement.GetProperty("ok").GetBoolean(), "Old stop callback invalidated new capture");
            Check(json.RootElement.GetProperty("device").GetString() == "headphones", "Wrong output captured");
            Check(json.RootElement.GetProperty("peak").GetDouble() == 0, "Old samples survived device switch");
        }
        sessions[1].SendSamples();
        using (var json = JsonDocument.Parse(audio.SnapshotJson()))
            Check(json.RootElement.GetProperty("peak").GetDouble() > 0, "Replacement capture did not publish");
        audio.Dispose(); audio.PollDevice(); sessions[1].SendSamples();
        Check(sessions.Count == 2 && sessions[1].Disposed, "Disposal allowed another capture");
    });
    Test("Audio retries failed starts, stopped streams, and unavailable endpoints", () => {
        bool unavailable = false; int opened = 0; FakeLoopbackSession latest = null;
        using var audio = new AudioCapture(() => unavailable ? throw new InvalidOperationException("No output") : "speakers",
            id => latest = new FakeLoopbackSession(id) { FailStart = ++opened == 1 }, startTimer: false);
        audio.PollDevice(); Check(latest.Disposed, "Failed start leaked capture");
        audio.PollDevice(); Check(opened == 2 && !latest.Disposed, "Failed start was not retried");
        latest.Stop(); audio.PollDevice(); Check(opened == 3, "Stopped stream was not restarted");
        unavailable = true; audio.PollDevice(); Check(latest.Disposed, "Unavailable output retained capture");
        unavailable = false; audio.PollDevice(); Check(opened == 4, "Output recovery did not resume capture");
    });
    Test("Feed disposal immediately releases its fallback port", () => {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start(); int requested = ((IPEndPoint)occupied.LocalEndpoint).Port;
        using var server = new FeedServer(requested);
        server.Start();
        int fallback = server.Port;
        Check(fallback != requested, "Fallback was not selected");
        server.Dispose();
        using var replacement = new TcpListener(IPAddress.Loopback, fallback);
        replacement.Start();
    });
    Test("Process drains stderr without deadlock and rejects failed exits", () => {
        var output = BoundedProcess.Run(fixtureExe, fixturePrefix + "--fixture pipes", 5000);
        Check(output?.Trim() == "42, 55, 100, 200", "Concurrent output was not read");
        Check(BoundedProcess.Run(fixtureExe, fixturePrefix + "--fixture fail", 5000) == null, "Nonzero exit accepted");
        Check(BoundedProcess.Run(Path.Combine(tempRoot, "missing.exe"), "") == null, "Missing executable should be unavailable");
    });
    Test("Process timeout kills a hung child", () => {
        var pidFile = Path.Combine(tempRoot, "child.pid");
        var elapsed = Stopwatch.StartNew();
        Check(BoundedProcess.Run(fixtureExe, fixturePrefix + "--fixture hang \"" + pidFile + "\"", 1200) == null, "Hung process returned output");
        Check(elapsed.ElapsedMilliseconds < 4000, "Timeout did not bound process execution");
        Check(File.Exists(pidFile), "Fixture never started");
        int pid = int.Parse(File.ReadAllText(pidFile));
        bool exited; try { using var process = Process.GetProcessById(pid); exited = process.HasExited; } catch (ArgumentException) { exited = true; }
        Check(exited, "Hung child was left running");
    });
    Test("GPU parser handles missing, invalid, and partial data", () => {
        var valid = GpuTelemetry.Parse("42, 55, 100, 200\n");
        Check(valid.Util == 42 && valid.TempC == 55 && valid.VramPct == 50, "Valid data lost");
        var partial = GpuTelemetry.Parse("N/A, 50, N/A, 0");
        Check(partial.Util == null && partial.TempC == 50 && partial.VramPct == null, "Partial data was fabricated");
        Check(GpuTelemetry.Parse("NaN, Infinity, -1, 0") == new GpuSample(), "Invalid numbers accepted");
        Check(GpuTelemetry.Parse(null) == new GpuSample(), "Missing data accepted");
    });
    Test("GPU failure and freshness clear old values; pause and dispose stop polling", () => {
        long now = 100; string output = "42, 55, 100, 200"; int calls = 0;
        using var gpu = new GpuTelemetry(() => { calls++; return output; }, () => now, false);
        gpu.Poll(); Check(gpu.Snapshot().Util == 42, "No initial sample");
        now += 5001; Check(gpu.Snapshot().Util == null, "Stale sample retained");
        gpu.Poll(); output = null; gpu.Poll(); Check(gpu.Snapshot().Util == null, "Failure retained old value");
        gpu.SetPaused(true); int before = calls; gpu.Poll(); Check(calls == before, "Paused query executed");
        gpu.SetPaused(false); output = "10, 40, 1, 2"; gpu.Poll(); Check(gpu.Snapshot().Util == 10, "Resume failed");
        gpu.Dispose(); before = calls; gpu.Poll(); Check(calls == before && gpu.Snapshot().Util == null, "Disposed poll published");
    });
    Test("GPU polls cannot overlap or publish after disposal", () => {
        using var entered = new ManualResetEventSlim(); using var finish = new ManualResetEventSlim(); int calls = 0;
        using var gpu = new GpuTelemetry(() => { Interlocked.Increment(ref calls); entered.Set(); finish.Wait(5000); return "42, 55, 1, 2"; }, startTimer: false);
        var first = Task.Run(gpu.Poll);
        try { Check(entered.Wait(3000), "Query never started"); gpu.Poll(); Check(calls == 1, "Queries overlapped"); gpu.Dispose(); }
        finally { finish.Set(); first.Wait(); }
        Check(gpu.Snapshot().Util == null, "Disposed sample was published");
    });
    Test("GPU results from before a pause/resume are discarded", () => {
        using var entered = new ManualResetEventSlim(); using var finish = new ManualResetEventSlim();
        using var gpu = new GpuTelemetry(() => { entered.Set(); finish.Wait(5000); return "42, 55, 1, 2"; }, startTimer: false);
        var first = Task.Run(gpu.Poll);
        try { Check(entered.Wait(3000), "Query never started"); gpu.SetPaused(true); gpu.SetPaused(false); }
        finally { finish.Set(); first.Wait(); }
        Check(gpu.Snapshot().Util == null, "Pre-pause result was published after resume");
    });
    Test("Named profiles round-trip custom palettes and monitor settings", () => {
        var root = Folder("profiles"); var snapshot = ProfileSettings.Capture(root);
        snapshot.Colors = new(ThemeSettings.Presets["OLED - Amber"]);
        snapshot.CustomColors = new(ThemeSettings.Presets["Cyber Cyan"]);
        snapshot.Monitors["display-a"] = new() { Theme = "Custom", OledBrightness = 25, Transparent = true };
        snapshot.MonitorColors["display-a"] = new(ThemeSettings.Presets["OLED - Violet"]);
        snapshot.Performance.Fps = 15; snapshot.Performance.PauseApps.Add("game"); snapshot.Audio = 0;
        snapshot.Clock = "24"; snapshot.Temperature = "C";
        var catalog = ProfileCatalog.Load(root, new[] { "display-a" });
        ProfileCatalog.Save(root, catalog, "My Night", snapshot);
        snapshot.Performance.Fps = 120;
        var saved = ProfileCatalog.Load(root).Single(p => p.Name == "My Night").Settings;
        Check(saved.Performance.Fps == 15, "Saved profile shares mutable state");
        saved.Apply(root); var restored = ProfileSettings.Capture(root);
        Check(JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(restored), "Profile settings did not round-trip");
        ProfileCatalog.Save(root, catalog, "my night", restored);
        Check(catalog.Count(p => p.Name.Equals("My Night", StringComparison.OrdinalIgnoreCase)) == 1, "Replacement duplicated profile");
        Check(!File.Exists(Path.Combine(root, "startup.disabled")), "Profile touched startup");
    });
    Test("Starter profiles cover new displays and use distinct power settings", () => {
        var catalog = ProfileCatalog.Load(Folder("defaults"), new[] { "new-display" });
        var night = catalog.Single(p => p.Name == "Night").Settings;
        Check(night.Monitors["new-display"].OledBrightness == 25 && night.Audio == 0, "Night preset ineffective on new monitor");
        Check(catalog.Single(p => p.Name == "Gaming").Settings.Performance.AutoPause, "Gaming does not auto-pause");
        Check(catalog.Single(p => p.Name == "Minimal").Settings.Performance.Animations == "Off", "Minimal animates");
    });
    Test("Invalid profiles do not overwrite settings; corrupt catalog recovers backup", () => {
        var root = Folder("validation"); var settings = ProfileSettings.Capture(root); settings.Apply(root);
        var before = File.ReadAllText(Path.Combine(root, "performance.json"));
        settings.Performance.Fps = 999;
        bool rejected = false; try { settings.Apply(root); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && File.ReadAllText(Path.Combine(root, "performance.json")) == before, "Invalid profile changed settings");
        var catalog = ProfileCatalog.Load(root); settings.Performance.Fps = 30;
        ProfileCatalog.Save(root, catalog, "First", settings); ProfileCatalog.Save(root, catalog, "Second", settings);
        File.WriteAllText(Path.Combine(root, "profiles.json"), "broken JSON");
        Check(ProfileCatalog.Load(root).Any(p => p.Name == "First"), "Backup did not recover catalog");
        bool invalidName = false; try { ProfileCatalog.Save(root, catalog, " ", settings); } catch (InvalidDataException) { invalidName = true; }
        Check(invalidName, "Empty name accepted");
    });
    Test("Profile write failure rolls back settings already written", () => {
        var root = Folder("rollback"); var previous = ProfileSettings.Capture(root); previous.Apply(root);
        var next = previous.Clone(); next.Colors = new(ThemeSettings.Presets["OLED - Amber"]); next.Performance.Fps = 120;
        using var locked = new FileStream(Path.Combine(root, "clock.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        bool failed = false; try { next.Apply(root); } catch (IOException) { failed = true; }
        Check(failed, "Expected locked settings file to fail");
        Check(PerformanceSettings.Load(root).Fps == previous.Performance.Fps, "Performance was not rolled back");
        Check(ThemeSettings.Match(ThemeSettings.Load(root)) == ThemeSettings.Match(previous.Colors), "Theme was not rolled back");
    });
} finally { Directory.Delete(tempRoot, recursive: true); }
Console.WriteLine($"{(failures == 0 ? "All tests passed" : failures + " tests failed")}");
return failures == 0 ? 0 : 1;

internal sealed class FakeLoopbackSession(string name) : ILoopbackSession
{
    public string DeviceName => name;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;
    public bool Disposed { get; private set; }
    public bool FailStart { get; init; }
    public void StartRecording() { if (FailStart) throw new InvalidOperationException("Start failed"); }
    public void Dispose() => Disposed = true;
    public void Stop() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
    public void SendSamples()
    {
        var samples = new float[2048]; Array.Fill(samples, .5f);
        var buffer = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, buffer, 0, buffer.Length);
        DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, buffer.Length));
    }
}
