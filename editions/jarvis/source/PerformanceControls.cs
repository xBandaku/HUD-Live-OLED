using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace HudWall;

internal sealed partial class HudForm
{
    private PerformanceSettings _performance = PerformanceSettings.Load(Path.Combine(AppContext.BaseDirectory, "assets"));
    private System.Windows.Forms.Timer _performanceTimer;
    private bool _manualPause, _playbackPaused;
    private ToolStripMenuItem _playbackStatus = new ToolStripMenuItem("Wallpaper running") { Enabled = false };

    private void AddPerformanceMenu(ContextMenuStrip menu)
    {
        menu.Items.Add(_playbackStatus);
        var manual = new ToolStripMenuItem("Pause wallpaper"){Checked=_manualPause};
        manual.Click += (_, _) => { SetManualPause(!_manualPause); manual.Checked = _manualPause; };
        menu.Opening+=(_,_)=>manual.Checked=_manualPause;
        menu.Items.Add(manual);
        var automatic = new ToolStripMenuItem("Pause during fullscreen apps / games") { Checked = _performance.AutoPause };
        automatic.Click += (_, _) => {
            bool previous = _performance.AutoPause;
            _performance.AutoPause = !previous;
            if (!SavePerformance()) _performance.AutoPause = previous;
            automatic.Checked = _performance.AutoPause;
            EvaluatePlayback();
        };
        menu.Items.Add(automatic);
        var apps = new ToolStripMenuItem("Pause for selected apps...");
        apps.Click += (_, _) => {
            using var dialog = new PauseAppsDialog(_performance.PauseApps);
            if (dialog.ShowDialog() != DialogResult.OK) return;
            var previous = _performance.PauseApps;
            _performance.PauseApps = dialog.SelectedApps;
            if (!SavePerformance()) _performance.PauseApps = previous;
            EvaluatePlayback();
        };
        menu.Items.Add(apps);
        var fps = new ToolStripMenuItem("Frame rate limit");
        foreach (int value in new[] { 15, 30, 60, 120 })
        {
            var item = new ToolStripMenuItem(value + " FPS") { Checked = _performance.Fps == value };
            item.Click += (_, _) => {
                int previous = _performance.Fps; _performance.Fps = value;
                if (!SavePerformance()) _performance.Fps = previous;
                foreach (ToolStripMenuItem option in fps.DropDownItems) option.Checked = option.Text == _performance.Fps + " FPS";
                ApplyPlayback();
            };
            fps.DropDownItems.Add(item);
        }
        menu.Items.Add(fps);
        var animations = new ToolStripMenuItem("Animations");
        foreach (string mode in new[] { "Full", "Reduced", "Off" })
        {
            var item = new ToolStripMenuItem(mode) { Checked = _performance.Animations == mode };
            item.Click += (_, _) => {
                string previous = _performance.Animations; _performance.Animations = mode;
                if (!SavePerformance()) _performance.Animations = previous;
                foreach (ToolStripMenuItem option in animations.DropDownItems.OfType<ToolStripMenuItem>()) option.Checked = option.Text == _performance.Animations;
                ApplyPlayback();
            };
            animations.DropDownItems.Add(item);
        }
        var speed = new ToolStripMenuItem("Motion speed");
        foreach (double value in new[] { .5, 1, 2 })
        {
            var item = new ToolStripMenuItem(value == .5 ? "Slow (0.5x)" : value == 1 ? "Normal (1x)" : "Fast (2x)") { Checked = _performance.Speed == value, Tag = value };
            item.Click += (_, _) => {
                double previous = _performance.Speed; _performance.Speed = value;
                if (!SavePerformance()) _performance.Speed = previous;
                foreach (ToolStripMenuItem option in speed.DropDownItems) option.Checked = (double)option.Tag == _performance.Speed;
                ApplyPlayback();
            };
            speed.DropDownItems.Add(item);
        }
        animations.DropDownItems.Add(new ToolStripSeparator());
        animations.DropDownItems.Add(speed);
        menu.Items.Add(animations);
        menu.Items.Add(new ToolStripSeparator());
    }

    private bool SetManualPause(bool paused){
        bool previous=_manualPause;_performance.ManualPause=paused;
        if(!SavePerformance()){_performance.ManualPause=previous;return false;}
        _manualPause=paused;EvaluatePlayback();return true;
    }

    private bool SavePerformance()
    {
        try { _performance.Save(Path.Combine(AppContext.BaseDirectory, "assets")); return true; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Could not save performance settings"); return false; }
    }

    private void InitializePerformance()
    {
        _performanceTimer = new System.Windows.Forms.Timer { Interval = 750 };
        _performanceTimer.Tick += (_, _) => EvaluatePlayback();
        _performanceTimer.Start();
        FormClosed += (_, _) => {
            _performanceTimer.Dispose();
            _statsTimer?.Dispose(); _audioTimer?.Dispose(); _watchTimer?.Dispose();
            _stats?.Dispose(); _audio?.Dispose(); _tray.Dispose();
        };
    }

    private void EvaluatePlayback()
    {
        bool automatic = _performance.AutoPause && FullscreenDetector.IsForegroundFullscreen();
        string app = FullscreenDetector.ForegroundApp();
        UpdateMonitorPlayback(automatic, app, FullscreenDetector.ForegroundFullscreenDevices(), FullscreenDetector.ForegroundMonitor());
    }

    private async void ApplyPlayback()
    {
        try
        {
            ApplyNativeTransparency();
            _stats?.SetPaused(_playbackPaused);
            if (_statsTimer != null) _statsTimer.Enabled = !_playbackPaused;
            bool audioEnabled = !_playbackPaused && _performance.Animations != "Off" && AudioSettings.Load(Path.Combine(AppContext.BaseDirectory, "assets")) > 0;
            if (_audioTimer != null) _audioTimer.Enabled = audioEnabled;
            if (!audioEnabled) { _audio?.Dispose(); _audio = null; }
            else if (_audioTimer != null && _audio == null) _audio = new AudioCapture();
            var core = _web?.CoreWebView2;
            if (core == null) return;
            string json = JsonSerializer.Serialize(new { paused = _playbackPaused, fps = _performance.Fps, animations = _performance.Animations, speed = _performance.Speed, monitors = _monitorStates });
            await core.ExecuteScriptAsync("window.setHudPlayback && window.setHudPlayback(" + json + ")");
        }
        catch (Exception ex) { Program.Log("apply playback failed: " + ex); }
    }
}
