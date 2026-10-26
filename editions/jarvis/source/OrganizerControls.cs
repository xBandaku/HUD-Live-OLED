using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DeskNest.Models;
using Organizer = DeskNest.App;

namespace HudWall;

internal sealed class OrganizerSettings
{
    public bool Enabled { get; set; } = true;
    internal static OrganizerSettings Load(string assets){try{return JsonSerializer.Deserialize<OrganizerSettings>(SettingsStorage.Read(Path.Combine(assets,"organizer.json")))??new();}catch(Exception ex)when(ex is IOException||ex is JsonException){return new();}}
    internal void Save(string assets)=>SettingsStorage.Write(Path.Combine(assets,"organizer.json"),JsonSerializer.Serialize(this));
}

/// <summary>
/// The DeskNest desktop organizer (folder portals, shortcut fences, tabbed launcher, auto-sort) running inside
/// HUD Live: one tray icon, one startup entry, HUD-themed frames and Jarvis voice commands.
/// </summary>
internal sealed partial class HudForm
{
    internal static readonly string[] OrganizerVoiceCommands={"open launcher","close launcher","sort desktop","auto sort on","auto sort off","edit sort rules","show frames","hide frames","bring frames back","new fence","new portal","new portal for downloads","new portal for documents","new portal for pictures","new portal for music","new portal for videos","new portal for desktop","show desktop icons","hide desktop icons"};

    private Task<Organizer> _organizer;
    private System.Windows.Forms.Timer _organizerWait;
    private readonly OrganizerSettings _organizerSettings = OrganizerSettings.Load(MonitorAssets);

    /// <summary>The organizer while it is running, otherwise null.</summary>
    private Organizer RunningOrganizer => _organizer is { IsCompletedSuccessfully: true } task && !task.Result.Stopped.IsCompleted ? task.Result : null;

    private void InitializeOrganizer()
    {
        _tray.MouseClick+=(_,e)=>{if(e.Button==MouseButtons.Left)RunningOrganizer?.ToggleLauncher();};
        FormClosed+=(_,_)=>{_organizerWait?.Stop();RunningOrganizer?.Stop();};
        if(_organizerSettings.Enabled)StartOrganizer();
    }

    private void StartOrganizer()
    {
        // WPF allows one organizer per process: once started (or failed) it stays that way until HUD Live restarts.
        if(_organizer!=null)return;
        if(Organizer.IsOtherInstanceRunning())
        {
            // The standalone DeskNest still owns the layout; take over as soon as it exits.
            if(_organizerWait!=null)return;
            Notify("Desktop organizer is waiting","Exit the standalone DeskNest from its tray icon. HUD Live then takes over your frames and launcher.");
            _organizerWait=new System.Windows.Forms.Timer{Interval=5000};
            _organizerWait.Tick+=(_,_)=>{if(Organizer.IsOtherInstanceRunning())return;_organizerWait.Stop();_organizerWait=null;StartOrganizer();};
            _organizerWait.Start();
            return;
        }
        var (global,_)=OrganizerPalettes();
        _organizer=Organizer.Start(new OrganizerHost(this),global);
        _organizer.ContinueWith(t=>{
            if(t.IsFaulted){Program.Log("desktop organizer: "+t.Exception?.GetBaseException().Message);Notify("Desktop organizer could not start","Details are in %AppData%\\DeskNest\\desknest.log.");}
            else{Program.Log("desktop organizer started");PushOrganizerTheme();}
        },TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ToggleOrganizerEnabled()
    {
        _organizerSettings.Enabled=!_organizerSettings.Enabled;
        try{_organizerSettings.Save(MonitorAssets);}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save organizer setting");}
        if(!_organizerSettings.Enabled){_organizerWait?.Stop();_organizerWait=null;RunningOrganizer?.Stop();return;}
        if(_organizer==null)StartOrganizer();
        else MessageBox.Show("The desktop organizer starts again the next time HUD Live starts.","Desktop organizer");
    }

    private void Notify(string title,string text){try{_tray.ShowBalloonTip(4000,title,text,ToolTipIcon.Info);}catch(Exception ex){Program.Log("notify: "+ex.Message);}}

    private sealed class OrganizerHost(HudForm form) : DeskNest.IOrganizerHost
    {
        public void Notify(string title,string text)=>form.VoiceUi(()=>form.Notify(title,text));
    }

    /// <summary>
    /// The global theme plus each monitor's own theme, so a frame takes the colors of the HUD behind it.
    /// A spanning HUD uses the primary display's theme everywhere, and so do the frames.
    /// </summary>
    private (HudPalette global,Dictionary<string,HudPalette> screens) OrganizerPalettes()
    {
        var screens=new Dictionary<string,HudPalette>(StringComparer.OrdinalIgnoreCase);
        foreach(var screen in Screen.AllScreens)
            screens[screen.DeviceName]=HudPalette.From(MonitorSettings.Colors(MonitorAssets,screen.DeviceName,Preference(screen.DeviceName)));
        if(LayoutSettings.Load(MonitorAssets).Span&&Screen.PrimaryScreen!=null&&screens.TryGetValue(Screen.PrimaryScreen.DeviceName,out var primary))
            foreach(string id in screens.Keys.ToArray())screens[id]=primary;
        return (HudPalette.From(ThemeSettings.Load(MonitorAssets)),screens);
    }

    private void PushOrganizerTheme()
    {
        var organizer=RunningOrganizer;
        if(organizer==null)return;
        try{var (global,screens)=OrganizerPalettes();organizer.ApplyTheme(global,screens);}
        catch(Exception ex){Program.Log("organizer theme: "+ex.Message);}
    }

    private string OrganizerStatus()=>
        !_organizerSettings.Enabled?"Off":
        _organizer==null?(_organizerWait!=null?"Waiting for the standalone DeskNest to exit":"Not started"):
        !_organizer.IsCompleted?"Starting":
        _organizer.IsFaulted?"Could not start (see desknest.log)":
        _organizer.Result.Stopped.IsCompleted?"Stopped until HUD Live restarts":
        "Running / frames "+(_organizer.Result.FramesVisible?"shown":"hidden");

    private void AddOrganizerMenu(ContextMenuStrip menu)
    {
        var root=new ToolStripMenuItem("Desktop organizer");
        var status=new ToolStripMenuItem("Starting"){Enabled=false};
        var launcher=new ToolStripMenuItem("Open launcher",null,(_,_)=>RunningOrganizer?.ShowLauncher());
        var portal=new ToolStripMenuItem("New folder portal...",null,(_,_)=>RunningOrganizer?.NewFolderPortal(null));
        var fence=new ToolStripMenuItem("New shortcut fence",null,(_,_)=>RunningOrganizer?.NewShortcutFence());
        var frames=new ToolStripMenuItem("Show frames",null,(_,_)=>{var o=RunningOrganizer;o?.SetFramesVisible(!o.FramesVisible);});
        var autoSort=new ToolStripMenuItem("Auto-sort new desktop items",null,(_,_)=>{
            var o=RunningOrganizer;if(o==null)return;
            if(!o.AutoSortEnabled&&!o.HasSortRules){o.EditRules();return;}
            o.SetAutoSort(!o.AutoSortEnabled);
        });
        var rules=new ToolStripMenuItem("Edit auto-sort rules...",null,(_,_)=>RunningOrganizer?.EditRules());
        var sortNow=new ToolStripMenuItem("Sort desktop now",null,async(_,_)=>{var o=RunningOrganizer;if(o==null)return;Notify("Desktop sorted",SortSummary(await o.SortDesktopNow(),o));});
        var icons=new ToolStripMenuItem("Show desktop icons",null,(_,_)=>Organizer.SetDesktopIconsVisible(!Organizer.DesktopIconsVisible));
        var bring=new ToolStripMenuItem("Bring frames back on screen",null,(_,_)=>RunningOrganizer?.BringFramesOnScreen());
        var folder=new ToolStripMenuItem("Open organizer settings folder",null,(_,_)=>RunningOrganizer?.OpenSettingsFolder());
        var enabled=new ToolStripMenuItem("Run desktop organizer",null,(_,_)=>ToggleOrganizerEnabled());
        root.DropDownItems.AddRange(new ToolStripItem[]{status,new ToolStripSeparator(),launcher,portal,fence,new ToolStripSeparator(),frames,autoSort,rules,sortNow,new ToolStripSeparator(),icons,bring,folder,new ToolStripSeparator(),enabled});
        root.DropDownOpening+=(_,_)=>{
            var o=RunningOrganizer;
            status.Text=OrganizerStatus();
            foreach(var item in new ToolStripItem[]{launcher,portal,fence,frames,autoSort,rules,sortNow,bring,folder})item.Enabled=o!=null;
            launcher.Text=o!=null?"Open launcher ("+o.LauncherHotkey+")":"Open launcher";
            frames.Checked=o?.FramesVisible==true;
            autoSort.Checked=o?.AutoSortEnabled==true;
            icons.Checked=Organizer.DesktopIconsVisible;
            enabled.Checked=_organizerSettings.Enabled;
        };
        menu.Items.Add(root);
    }

    private static string SortSummary(int moved,Organizer organizer)=>
        moved>0?"Moved "+moved+(moved==1?" item":" items")+" off the desktop.":
        organizer.HasSortRules?"Nothing on the desktop matched an auto-sort rule.":
        "There are no auto-sort rules yet. Add some in the rule editor.";

    private static readonly Dictionary<string,Func<string>> PortalFolders=new(){
        ["downloads"]=()=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),
        ["documents"]=()=>Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        ["pictures"]=()=>Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        ["music"]=()=>Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        ["videos"]=()=>Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        ["desktop"]=()=>Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
    };

    /// <summary>
    /// "Jarvis ..." commands for the organizer. Accepts the fixed phrases in <see cref="OrganizerVoiceCommands"/> and
    /// natural variants from Whisper ("could you tidy up my desktop", "hide the fences"). False if it is not one.
    /// </summary>
    private bool HandleOrganizerCommand(string command)
    {
        string c=Regex.Replace(command,@"^(?:(?:please|can you|could you|would you)\s+)+","");
        c=Regex.Replace(c,@"\s+please$","");
        c=Regex.Replace(c,@"\b(?:the|my|a|all)\s+","").Replace("auto-sort","auto sort").Replace("autosort","auto sort").Trim();
        Match m;
        Action<Organizer> act;
        if(Regex.IsMatch(c,@"^(?:open|show|launch|start|bring up) (?:app )?launcher$"))act=o=>{o.ShowLauncher();Reply("Launcher open.");};
        else if(Regex.IsMatch(c,@"^(?:close|hide|dismiss) (?:app )?launcher$"))act=o=>{o.HideLauncher();Reply("Launcher closed.");};
        else if(Regex.IsMatch(c,@"^(?:sort|tidy|tidy up|clean up|organi[sz]e) desktop$"))act=async o=>Reply(SortSummary(await o.SortDesktopNow(),o));
        else if((m=Regex.Match(c,@"^(?:turn )?auto sort(?:ing)? (on|off)$|^(enable|disable|start|stop) auto sort(?:ing)?$")).Success){
            bool on=m.Groups[1].Value=="on"||m.Groups[2].Value is "enable" or "start";
            act=o=>{
                if(on&&!o.HasSortRules){o.EditRules();Reply("There are no auto-sort rules yet. I've opened the rule editor.");return;}
                o.SetAutoSort(on);Reply(on?"Auto-sort on. New desktop items will be filed by your rules.":"Auto-sort off.");
            };
        }
        else if(Regex.IsMatch(c,@"^(?:edit|open|show) (?:auto )?sort(?:ing)? rules$"))act=o=>{o.EditRules();Reply("Opening the auto-sort rules.");};
        else if((m=Regex.Match(c,@"^(show|hide) (?:frames|fences|portals)$")).Success){bool show=m.Groups[1].Value=="show";act=o=>{o.SetFramesVisible(show);Reply(show?"Frames visible.":"Frames hidden. Say Jarvis show frames to bring them back.");};}
        else if(Regex.IsMatch(c,@"^bring (?:frames|fences) back(?: on screen)?$"))act=async o=>{int moved=await o.BringFramesOnScreen();Reply(moved==0?"All frames are on screen.":"Moved "+moved+(moved==1?" frame":" frames")+" back on screen.");};
        else if(Regex.IsMatch(c,@"^(?:new|create|add) (?:shortcut )?fence$"))act=o=>{o.NewShortcutFence();Reply("New shortcut fence added. Drop desktop icons on it to tidy them away.");};
        else if((m=Regex.Match(c,@"^(?:new|create|add) (?:folder )?portal(?: (?:for|to|of) (.+))?$")).Success){
            string name=m.Groups[1].Value.Trim();
            if(name.Length>0&&!PortalFolders.ContainsKey(name)){Reply("I don't have a folder called "+name+". Say Jarvis new portal to choose one.");return true;}
            act=async o=>{
                if(name.Length==0)Reply("Choose a folder for the new portal.");
                if(await o.NewFolderPortal(name.Length==0?null:PortalFolders[name]()))Reply("Portal added"+(name.Length>0?" for "+name:"")+".");
            };
        }
        else if((m=Regex.Match(c,@"^(show|hide) desktop icons$")).Success){bool show=m.Groups[1].Value=="show";act=_=>{Organizer.SetDesktopIconsVisible(show);Reply(show?"Desktop icons visible.":"Desktop icons hidden.");};}
        else return false;

        var organizer=RunningOrganizer;
        if(organizer==null){Reply("The desktop organizer is not running. Check the tray menu under Desktop organizer.");return true;}
        _lastAction="Desktop organizer / "+c;
        SetVoiceStatus("JARVIS / acting");
        act(organizer);
        return true;
    }
}
