namespace HudWall;

internal sealed partial class HudForm
{
    private void AddProfilesMenu(ContextMenuStrip menu)
    {
        var profilesMenu = new ToolStripMenuItem("Profiles");
        profilesMenu.DropDownOpening += (_, _) => {
            foreach (var item in profilesMenu.DropDownItems.Cast<ToolStripItem>().ToArray()) item.Dispose();
            profilesMenu.DropDownItems.Clear();
            try {
                foreach (var profile in ProfileCatalog.Load(SettingsStore.Root, Screen.AllScreens.Select(s => s.DeviceName))) {
                    var item = new ToolStripMenuItem(profile.Name);
                    item.Click += (_, _) => {
                        try {
                            var settings = profile.Settings.Clone();
                            settings.Apply(SettingsStore.Root);
                            _performance = settings.Performance; _monitorPreferences = settings.Monitors;
                            _monitorSignature = ""; ApplySettings();
                            var previous = _tray.ContextMenuStrip;
                            _tray.ContextMenuStrip = BuildMenu(); previous.Dispose();
                        } catch (Exception ex) { MessageBox.Show(ex.Message, "Could not apply profile"); }
                    };
                    profilesMenu.DropDownItems.Add(item);
                }
            } catch (Exception ex) {
                Program.Log("Could not load profiles: " + ex.Message);
                profilesMenu.DropDownItems.Add(new ToolStripMenuItem("Profiles unavailable — see Settings") { Enabled = false });
            }
        };
        menu.Items.Add(profilesMenu);
    }
}
