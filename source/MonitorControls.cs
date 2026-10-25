using System.Text.Json;
using System.Windows.Forms;

namespace HudWall;

internal sealed partial class HudForm
{
    private Dictionary<string,MonitorPreference> _monitorPreferences = MonitorSettings.Load(SettingsStore.Root);
    private List<MonitorState> _monitorStates = new();
    private string _monitorSignature="";
    private readonly Dictionary<string, Dictionary<string,string>> _monitorPaletteCache = new();
    private Dictionary<string,string> MonitorColors(string id, MonitorPreference preference)
    {
        if (!_monitorPaletteCache.TryGetValue(id, out var colors)) _monitorPaletteCache[id] = colors = MonitorSettings.Colors(MonitorAssets, id, preference);
        return colors;
    }
    private static string MonitorAssets => SettingsStore.Root;

    private MonitorPreference Preference(string id)
    {
        if(!_monitorPreferences.TryGetValue(id,out var preference)) _monitorPreferences[id]=preference=new();
        return preference;
    }

    private bool SaveMonitors()
    {
        try { MonitorSettings.Save(MonitorAssets,_monitorPreferences); return true; }
        catch(Exception ex) { _monitorPreferences=MonitorSettings.Load(MonitorAssets); MessageBox.Show(ex.Message,"Could not save monitor settings"); return false; }
    }

    private void AddMonitorMenu(ContextMenuStrip menu)
    {
        var monitors=new ToolStripMenuItem("Monitors");
        void Rebuild()
        {
            var old=monitors.DropDownItems.Cast<ToolStripItem>().ToArray();
            monitors.DropDownItems.Clear(); foreach(var item in old)item.Dispose();
            foreach(var screen in Screen.AllScreens.OrderBy(s=>s.Bounds.X).ThenBy(s=>s.Bounds.Y))
            {
                string id=screen.DeviceName;
                var preference=Preference(id);
                string label=id.Replace("\\\\.\\","")+(screen.Primary ? " (primary)" : "")+" - "+screen.Bounds.Width+"x"+screen.Bounds.Height;
                var display=new ToolStripMenuItem(label);
                var pause=new ToolStripMenuItem("Pause this monitor") {Checked=preference.Paused};
                pause.Click+=(_,_)=>{Preference(id).Paused=!Preference(id).Paused; if(SaveMonitors())ApplySettings(); pause.Checked=Preference(id).Paused;};
                display.DropDownItems.Add(pause);
                var automatic=new ToolStripMenuItem("Pause this monitor for fullscreen apps") {Checked=preference.AutoPause};
                automatic.Click+=(_,_)=>{Preference(id).AutoPause=!Preference(id).AutoPause; if(SaveMonitors())ApplySettings(); automatic.Checked=Preference(id).AutoPause;};
                display.DropDownItems.Add(automatic);
                var themes=new ToolStripMenuItem("Theme") {DropDown=new ThemeDropDownMenu()};
                foreach(string name in new[]{"Global"}.Concat(ThemeSettings.Presets.Keys))
                {
                    var item=new StickyThemeMenuItem(name=="Global" ? "Use global theme" : name) {Checked=preference.Theme==name};
                    item.Click+=(_,_)=>{Preference(id).Theme=name; if(SaveMonitors())ApplySettings(); foreach(var option in themes.DropDownItems.OfType<ToolStripMenuItem>())option.Checked=option==item;};
                    themes.DropDownItems.Add(item);
                }
                var custom=new ToolStripMenuItem("Custom colors...") {Checked=preference.Theme=="Custom"};
                custom.Click+=(_,_)=>{
                    string folder=MonitorSettings.ColorFolder(MonitorAssets,id);
                    using var dialog=new ColorSettings(folder,MonitorSettings.Colors(MonitorAssets,id,Preference(id)));
                    if(dialog.ShowDialog()!=DialogResult.OK)return;
                    Preference(id).Theme="Custom";
                    if(SaveMonitors())ApplySettings();
                };
                themes.DropDownItems.Add(custom);
                themes.DropDownItems.Add(new ToolStripSeparator());
                themes.DropDownItems.Add(new ToolStripMenuItem("Done"));
                display.DropDownItems.Add(themes);
                var transparency=new ToolStripMenuItem("Transparent background (Windows wallpaper)") {Checked=preference.Transparent};
                transparency.Click+=(_,_)=>{Preference(id).Transparent=!Preference(id).Transparent;if(SaveMonitors())ApplySettings();transparency.Checked=Preference(id).Transparent;};
                display.DropDownItems.Add(transparency);
                AddOledMenu(display,id);
                monitors.DropDownItems.Add(display);
            }
        }
        monitors.DropDownOpening+=(_,_)=>Rebuild(); Rebuild(); menu.Items.Add(monitors);
    }

    private void UpdateMonitorPlayback(bool automatic,string app,string[] fullscreenDevices,string activeDevice)
    {
        var bounds=SystemInformation.VirtualScreen;
        var next=new List<MonitorState>();
        bool selectedApp=_performance.PauseApps.Contains(app,StringComparer.OrdinalIgnoreCase);
        foreach(var screen in Screen.AllScreens.OrderBy(s=>s.Bounds.X).ThenBy(s=>s.Bounds.Y))
        {
            string id=screen.DeviceName; var preference=Preference(id);
            bool localFullscreen=automatic && preference.AutoPause && fullscreenDevices.Contains(id,StringComparer.OrdinalIgnoreCase);
            bool localApp=selectedApp && string.Equals(id,activeDevice,StringComparison.OrdinalIgnoreCase);
            bool paused=_manualPause || preference.Paused || localFullscreen || localApp;
            string reason=_manualPause ? "Paused globally" : preference.Paused ? "Paused manually" : localApp ? "Paused: "+app : localFullscreen ? "Paused: fullscreen app" : "Running";
            next.Add(new(id,id.Replace("\\\\.\\",""),screen.Bounds.X-bounds.X,screen.Bounds.Y-bounds.Y,screen.Bounds.Width,screen.Bounds.Height,paused,reason,MonitorColors(id,preference),preference.Oled,preference.OledBrightness,preference.PixelShift,preference.Oled && preference.IdleDim && IdleInfo.IsIdle(120_000),preference.OledFloat,preference.OledScale,preference.Transparent));
        }
        string signature=JsonSerializer.Serialize(next);
        if(signature==_monitorSignature)return;
        _monitorSignature=signature; _monitorStates=next;
        int pausedCount=next.Count(m=>m.paused);
        _playbackPaused=next.Count>0 && pausedCount==next.Count;
        _playbackStatus.Text=pausedCount==0 ? "All monitors running" : pausedCount+" of "+next.Count+" monitors paused";
        ApplyPlayback();
    }
}
