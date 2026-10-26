using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
namespace HudWall;
internal sealed class LayoutSettings {
 public bool BezelSafe {get;set;}=true;
 public bool Span {get;set;}=true;
 internal static LayoutSettings Load(string assets){try{return JsonSerializer.Deserialize<LayoutSettings>(SettingsStorage.Read(Path.Combine(assets,"layout.json")))??new();}catch(Exception ex)when(ex is IOException||ex is JsonException){return new();}}
 internal void Save(string assets)=>SettingsStorage.Write(Path.Combine(assets,"layout.json"),JsonSerializer.Serialize(this));
}
internal sealed partial class HudForm {
 private void AddLayoutMenu(ContextMenuStrip menu){
  var item=new ToolStripMenuItem("Span HUD across all monitors"){Checked=LayoutSettings.Load(MonitorAssets).Span};
  item.Click+=(_,_)=>SetSpan(!LayoutSettings.Load(MonitorAssets).Span);
  menu.Opening+=(_,_)=>item.Checked=LayoutSettings.Load(MonitorAssets).Span;menu.Items.Add(item);
  var safe=new ToolStripMenuItem("Keep widgets clear of monitor bezels");safe.Click+=(_,_)=>{try{var settings=LayoutSettings.Load(MonitorAssets);settings.BezelSafe=!settings.BezelSafe;settings.Save(MonitorAssets);_monitorSignature="";EvaluatePlayback();ApplySettings();}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save monitor layout");}};menu.Opening+=(_,_)=>safe.Checked=LayoutSettings.Load(MonitorAssets).BezelSafe;menu.Items.Add(safe);
 }
 private void SetSpan(bool enabled){try{var settings=LayoutSettings.Load(MonitorAssets);settings.Span=enabled;settings.Save(MonitorAssets);_monitorSignature="";EvaluatePlayback();ApplySettings();}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save monitor layout");}}
}
