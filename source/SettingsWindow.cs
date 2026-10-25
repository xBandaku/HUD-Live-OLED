using System.Drawing;
using System.Text.Json;

namespace HudWall;

internal sealed partial class HudForm
{
    private bool _settingsWindowOpen;
    private void ShowSettings()
    {
        if (_settingsWindowOpen) return;
        _settingsWindowOpen = true;
        try {
        using var window = new Form { Text = "HUD Live settings", StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(760, 600), MinimumSize = new Size(720, 570), AutoScaleMode = AutoScaleMode.Dpi,
            Font = new Font("Segoe UI", 10), ShowInTaskbar = true, Icon = Icon };
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12) };
        window.Controls.Add(tabs); window.Controls.Add(footer);
        FlowLayoutPanel Page(string title)
        {
            var page = new TabPage(title);
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16) };
            page.Controls.Add(panel); tabs.TabPages.Add(page); return panel;
        }
        ComboBox Choice(FlowLayoutPanel page, string label, string[] values, string selected)
        {
            page.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 10, 3, 3) });
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 400, AccessibleName = label };
            combo.Items.AddRange(values); combo.SelectedItem = selected; if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
            page.Controls.Add(combo); return combo;
        }
        CheckBox Toggle(FlowLayoutPanel page, string label, bool value)
        {
            var check = new CheckBox { Text = label, Checked = value, AutoSize = true, Margin = new Padding(3, 12, 3, 4) };
            page.Controls.Add(check); return check;
        }
        Button Action(FlowLayoutPanel page, string label, EventHandler handler)
        {
            var button = new Button { Text = label, AutoSize = true, MinimumSize = new Size(190, 32) };
            button.Click += handler; page.Controls.Add(button); return button;
        }
        var appearance = Page("Appearance");
        var draft = ProfileSettings.Capture(SettingsStore.Root);
        var palette = draft.Colors;
        var theme = Choice(appearance, "Theme", ThemeSettings.Presets.Keys.Concat(new[] { "Custom" }).ToArray(), ThemeSettings.Match(palette) ?? "Custom");
        var preview = new Panel { Width = 630, Height = 90, Margin = new Padding(3, 10, 3, 4) };
        var previewTitle = new Label { Text = "HUD LIVE  •  12:45", AutoSize = true, Location = new Point(18, 12), Font = new Font("Segoe UI", 17, FontStyle.Bold) };
        var previewDetail = new Label { Text = "CPU  24%     MEMORY  48%     GPU  32%", AutoSize = true, Location = new Point(20, 55) };
        preview.Controls.Add(previewTitle); preview.Controls.Add(previewDetail); appearance.Controls.Add(preview);
        void PreviewTheme()
        {
            var colors = theme.SelectedItem is string name && ThemeSettings.Presets.TryGetValue(name, out var preset) ? preset : palette;
            preview.BackColor = ColorTranslator.FromHtml(colors["background"]);
            previewTitle.ForeColor = ColorTranslator.FromHtml(colors["accent"]);
            previewDetail.ForeColor = ColorTranslator.FromHtml(colors["text"]);
        }
        theme.SelectedIndexChanged += (_, _) => PreviewTheme(); PreviewTheme();
        appearance.Controls.Add(new Label { Text = "Theme preview above. Apply updates the wallpaper immediately.", AutoSize = true });
        var clock = Choice(appearance, "Clock", new[] { "12", "24" }, ClockSettings.Load(SettingsStore.Root));
        var temperature = Choice(appearance, "Temperature (Celsius / Fahrenheit)", new[] { "C", "F" }, TemperatureSettings.Load(SettingsStore.Root));
        appearance.Controls.Add(new Label { Text = "Audio sensitivity (0 turns capture off; 100 is normal)", AutoSize = true });
        var audio = new NumericUpDown { Minimum = 0, Maximum = 300, Value = AudioSettings.Load(SettingsStore.Root), Width = 140, AccessibleName = "Audio sensitivity" }; appearance.Controls.Add(audio);
        Action(appearance, "Custom colors...", (_, _) => {
            using var dialog = new ColorSettings(SettingsStore.Root);
            if (dialog.ShowDialog(window) == DialogResult.OK) { palette = ThemeSettings.Load(SettingsStore.Root); draft.CustomColors = new(palette); theme.SelectedItem = "Custom"; PreviewTheme(); ApplySettings(); }
        });

        var performance = Page("Performance");
        var fps = Choice(performance, "Frame rate limit", new[] { "15", "30", "60", "120" }, _performance.Fps.ToString());
        var animations = Choice(performance, "Animations", new[] { "Full", "Reduced", "Off" }, _performance.Animations);
        var speed = Choice(performance, "Motion speed", new[] { "0.5", "1", "2" }, _performance.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var automatic = Toggle(performance, "Pause during fullscreen apps / games", _performance.AutoPause);
        var paused = Toggle(performance, "Pause wallpaper on all monitors", _manualPause);
        var pauseApps = new List<string>(_performance.PauseApps);
        Action(performance, "Choose apps that pause HUD...", (_, _) => { using var dialog = new PauseAppsDialog(pauseApps); if (dialog.ShowDialog(window) == DialogResult.OK) pauseApps = dialog.SelectedApps; });
        performance.Controls.Add(new Label { Text = "Paused monitors stop animating. When all monitors pause,\naudio capture and statistics updates stop too.", AutoSize = true, Margin = new Padding(3, 16, 3, 4) });

        var monitors = Page("Monitors & OLED");
        var preferences = JsonSerializer.Deserialize<Dictionary<string, MonitorPreference>>(JsonSerializer.Serialize(_monitorPreferences)) ?? new();
        var screens = Screen.AllScreens.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToArray();
        var display = Choice(monitors, "Monitor", screens.Select(s => s.DeviceName + (s.Primary ? " (primary)" : "") + " — " + s.Bounds.Width + "×" + s.Bounds.Height).ToArray(), "");
        var grid = new PropertyGrid { Width = 650, Height = 335, ToolbarVisible = false, HelpVisible = true, AccessibleName = "Monitor settings" };
        monitors.Controls.Add(grid);
        void SelectDisplay() { var id = screens[display.SelectedIndex].DeviceName; if (!preferences.ContainsKey(id)) preferences[id] = new(); grid.SelectedObject = preferences[id]; }
        display.SelectedIndexChanged += (_, _) => SelectDisplay(); SelectDisplay();
        monitors.Controls.Add(new Label { Text = "OLED care affects the HUD. Keep your monitor's panel care enabled.", AutoSize = true });

        ProfileSettings CaptureDraft()
        {
            var result = draft.Clone();
            string selected = (string)theme.SelectedItem;
            result.Colors = selected != "Custom" && ThemeSettings.Presets.TryGetValue(selected, out var preset) ? new(preset) : new(palette);
            result.CustomColors = selected == "Custom" ? new(palette) : draft.CustomColors;
            result.Clock = (string)clock.SelectedItem; result.Temperature = (string)temperature.SelectedItem;
            result.Audio = (int)audio.Value;
            result.Performance = new PerformanceSettings { Fps = int.Parse((string)fps.SelectedItem), Animations = (string)animations.SelectedItem,
                Speed = double.Parse((string)speed.SelectedItem, System.Globalization.CultureInfo.InvariantCulture), AutoPause = automatic.Checked, PauseApps = new(pauseApps) };
            result.Monitors = JsonSerializer.Deserialize<Dictionary<string, MonitorPreference>>(JsonSerializer.Serialize(preferences));
            foreach (var entry in result.Monitors.Where(p => p.Value.Theme == "Custom"))
                if (!result.MonitorColors.ContainsKey(entry.Key)) result.MonitorColors[entry.Key] = ThemeSettings.Load(MonitorSettings.ColorFolder(SettingsStore.Root, entry.Key));
            return result;
        }
        var profilesPage = Page("Profiles");
        List<NamedProfile> profiles;
        bool profilesReadable = true;
        try { profiles = ProfileCatalog.Load(SettingsStore.Root, screens.Select(s => s.DeviceName)); }
        catch (Exception ex) {
            profilesReadable = false;
            profiles = new();
            profilesPage.Controls.Add(new Label { Text = "Could not read profiles: " + ex.Message + "\nCheck profiles.json and its backup in the settings folder.", AutoSize = true, MaximumSize = new Size(620, 0) });
        }
        var profileChoice = Choice(profilesPage, "Saved profile", profiles.Select(p => p.Name).ToArray(), "");
        var profileName = new TextBox { Width = 400, MaxLength = 80, AccessibleName = "Profile name" };
        profilesPage.Controls.Add(new Label { Text = "Profile name (use an existing name to replace it)", AutoSize = true });
        profilesPage.Controls.Add(profileName);
        var profileStatus = new Label { AutoSize = true, MaximumSize = new Size(620, 0) };
        Action(profilesPage, "Load selected profile", (_, _) => {
            if (profileChoice.SelectedItem is not string name) return;
            try {
                draft = profiles.Single(p => p.Name == name).Settings.Clone(); draft.Validate();
                palette = draft.Colors; theme.SelectedItem = ThemeSettings.Match(palette) ?? "Custom"; PreviewTheme();
                clock.SelectedItem = draft.Clock; temperature.SelectedItem = draft.Temperature; audio.Value = draft.Audio;
                fps.SelectedItem = draft.Performance.Fps.ToString(); animations.SelectedItem = draft.Performance.Animations;
                speed.SelectedItem = draft.Performance.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                automatic.Checked = draft.Performance.AutoPause; pauseApps = new(draft.Performance.PauseApps);
                preferences = draft.Clone().Monitors; SelectDisplay();
                profileName.Text = name; profileStatus.Text = "Loaded " + name + ". Click Apply to update the wallpaper.";
            } catch (Exception ex) { MessageBox.Show(window, ex.Message, "Could not load profile"); }
        });
        var saveProfile = Action(profilesPage, "Save current settings as profile", (_, _) => {
            try {
                ProfileCatalog.Save(SettingsStore.Root, profiles, profileName.Text, CaptureDraft());
                profileChoice.Items.Clear(); profileChoice.Items.AddRange(profiles.Select(p => p.Name).ToArray());
                profileChoice.SelectedItem = profileName.Text.Trim();
                profileStatus.Text = "Saved " + profileName.Text.Trim() + ".";
            } catch (Exception ex) { MessageBox.Show(window, ex.Message, "Could not save profile"); }
        });
        saveProfile.Enabled = profilesReadable;
        profilesPage.Controls.Add(profileStatus);
        profilesPage.Controls.Add(new Label { Text = "Profiles include appearance, audio, performance, and per-monitor OLED settings.\nSave captures the controls in this window, including unapplied edits.\nWindows startup and the temporary pause-all switch are separate.", AutoSize = true, MaximumSize = new Size(620, 0), Margin = new Padding(3, 16, 3, 4) });

        var startupPage = Page("Startup & support");
        var startupSettings = new StartupSettings(Environment.GetFolderPath(Environment.SpecialFolder.Startup), AppContext.BaseDirectory);
        var startup = Toggle(startupPage, "Start HUD Live when I sign in to Windows", startupSettings.IsEnabled());
        startupPage.Controls.Add(new Label { Text = "Your choice is remembered when you launch HUD Live again.\nSettings are stored in your Windows user profile with backup copies.", AutoSize = true, Margin = new Padding(3, 14, 3, 14) });
        Action(startupPage, "Open settings folder", (_, _) => OpenFolder(SettingsStore.Root));
        Action(startupPage, "Open diagnostic logs", (_, _) => OpenFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HudWall")));

        var apply = new Button { Text = "Apply", Width = 100, Height = 32 };
        var close = new Button { Text = "Close", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        footer.Controls.Add(close); footer.Controls.Add(apply); window.CancelButton = close;
        apply.Click += (_, _) => {
            try
            {
                var next = CaptureDraft();
                next.Apply(SettingsStore.Root);
                if (startup.Checked != startupSettings.IsEnabled()) startupSettings.SetEnabled(startup.Checked);
                draft = next.Clone();
                _performance = next.Performance; _monitorPreferences = next.Monitors; _manualPause = paused.Checked;
                _monitorSignature = ""; EvaluatePlayback(); ApplySettings();
                var oldMenu = _tray.ContextMenuStrip; _tray.ContextMenuStrip = BuildMenu(); oldMenu.Dispose();
                apply.Text = "Applied";
            }
            catch (Exception ex) { MessageBox.Show(window, ex.Message, "Could not apply settings", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        window.ShowDialog();
        } finally { _settingsWindowOpen = false; }
    }

    private static void OpenFolder(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Could not open folder"); }
    }
}
