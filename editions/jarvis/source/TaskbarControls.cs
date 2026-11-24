using System.Drawing;
using System.Windows.Forms;
namespace HudWall;
internal sealed partial class HudForm {
 private HudTaskbars _hudTaskbars;
 private bool _taskbarInitialized;
 private const int TaskbarRecoveryHotkey=0x4854;
 private void AddHudTaskbarMenu(ContextMenuStrip menu){
  var root=new ToolStripMenuItem("HUD taskbar");var enabled=new ToolStripMenuItem("Replace Windows taskbar");enabled.Click+=(_,_)=>SetHudTaskbarEnabled(!HudTaskbarSettings.Load(MonitorAssets).Enabled);root.DropDownItems.Add(enabled);root.DropDownOpening+=(_,_)=>enabled.Checked=HudTaskbarSettings.Load(MonitorAssets).Enabled;
  root.DropDownItems.Add("Show Windows notification tray (25 seconds)",null,(_,_)=>_hudTaskbars?.WindowsTray());
  root.DropDownItems.Add("Restore Windows taskbar",null,(_,_)=>SetHudTaskbarEnabled(false));
  root.DropDownItems.Add(new ToolStripMenuItem("Recovery: Ctrl+Alt+Shift+T"){Enabled=false});menu.Items.Add(root);
 }
 private void InitializeHudTaskbar(){
  if(_taskbarInitialized)return;_taskbarInitialized=true;
  if(!TaskbarNative.RegisterHotKey(Handle,TaskbarRecoveryHotkey,0x4007,0x54))Program.Log("HUD taskbar recovery hotkey unavailable; use HUD menu > Restore Windows taskbar");
  FormClosed+=(_,_)=>{_hudTaskbars?.Dispose();TaskbarNative.UnregisterHotKey(Handle,TaskbarRecoveryHotkey);};
  Application.ApplicationExit+=(_,_)=>_hudTaskbars?.Dispose();
  if(HudTaskbarSettings.Load(MonitorAssets).Enabled){StartHudTaskbar();ApplySettings();}
 }
 private (string,Dictionary<string,string>) TaskbarTheme(Screen screen){
  string id=LayoutSettings.Load(MonitorAssets).Span?Screen.PrimaryScreen.DeviceName:screen.DeviceName;var preference=Preference(id);string style=preference.Interface=="Global"?AppearanceSettings.Load(MonitorAssets).Interface:preference.Interface;
  return (style,MonitorSettings.Colors(MonitorAssets,id,preference));
 }
 private void StartHudTaskbar(){
  if(_hudTaskbars!=null){_hudTaskbars.Resume();return;}
  try{_hudTaskbars=new HudTaskbars(MonitorAssets,Icon,TaskbarTheme,point=>{var screen=Screen.FromPoint(point);_tray.ContextMenuStrip.Show(new Point(Math.Clamp(point.X,screen.WorkingArea.Left,screen.WorkingArea.Right-50),Math.Max(screen.WorkingArea.Top,point.Y-25)));},()=>SetHudTaskbarEnabled(false));_hudTaskbars.LayoutChanged+=()=>ApplySettings();_hudTaskbars.Start();Program.Log("HUD taskbars running on "+Screen.AllScreens.Length+" displays");}
  catch(Exception ex){_hudTaskbars?.Dispose();_hudTaskbars=null;var s=HudTaskbarSettings.Load(MonitorAssets);s.Enabled=false;s.Save(MonitorAssets);Program.Log("HUD taskbar startup failed: "+ex);_tray.ShowBalloonTip(6000,"Windows taskbar restored","The HUD taskbar could not start. Details are in hudwall.log.",ToolTipIcon.Error);}
 }
 private void SetHudTaskbarEnabled(bool enabled){var s=HudTaskbarSettings.Load(MonitorAssets);s.Enabled=enabled;s.Save(MonitorAssets);if(enabled)StartHudTaskbar();else{_hudTaskbars?.Dispose();_hudTaskbars=null;}ApplySettings();}
 private bool HandleTaskbarCommand(string command){if(command is "restore windows taskbar" or "show windows taskbar"){SetHudTaskbarEnabled(false);Reply("Windows taskbar restored.");return true;}if(command is "enable hud taskbar" or "show hud taskbar"){SetHudTaskbarEnabled(true);Reply("HUD taskbar enabled.");return true;}if(command is "show notification tray" or "open system tray"){_hudTaskbars?.WindowsTray();return true;}return false;}
 protected override void WndProc(ref Message m){
  if(m.Msg==0x312&&m.WParam.ToInt32()==TaskbarRecoveryHotkey){SetHudTaskbarEnabled(false);return;}
  if(_taskbarInitialized&&(m.Msg==0x7e||(m.Msg==0x218&&m.WParam.ToInt32() is 7 or 18)))_hudTaskbars?.RequestRefresh();
  base.WndProc(ref m);
 }
}
