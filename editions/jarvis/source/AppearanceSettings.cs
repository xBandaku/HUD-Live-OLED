using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
namespace HudWall;
internal sealed class AppearanceSettings {
 public bool MapAnimation {get;set;}=true;
 public string MapView {get;set;}="World";
 public string Interface {get;set;}="JARVIS";
 public int PanelOpacity {get;set;}=88;
 public bool Hardware {get;set;}=true;
 public bool History {get;set;}=true;
 public bool Spectrum {get;set;}=true;
 public bool Reactor {get;set;}=true;
 public bool MovieIntro {get;set;}=true;
 public bool MapDepth {get;set;}=true;
 public bool EngineeringView {get;set;}=true;
 public int RepeatMinutes {get;set;}=5;
 public bool RichMapAnimation {get;set;}=true;
 public bool BootSequence {get;set;}=true;
 internal static AppearanceSettings Load(string assets){try{var s=JsonSerializer.Deserialize<AppearanceSettings>(SettingsStorage.Read(Path.Combine(assets,"appearance.json")))??new();if(!new[]{0,2,5,10,15}.Contains(s.RepeatMinutes))s.RepeatMinutes=5;if(!new[]{"World","North America","Europe","Asia-Pacific"}.Contains(s.MapView))s.MapView="World";if(s.Interface!="SHIELD")s.Interface="JARVIS";s.PanelOpacity=Math.Clamp(s.PanelOpacity,65,100);return s;}catch(Exception ex)when(ex is IOException||ex is JsonException){return new();}}
 internal void Save(string assets)=>SettingsStorage.Write(Path.Combine(assets,"appearance.json"),JsonSerializer.Serialize(this));
}
internal sealed partial class HudForm {
 private void AddAppearanceMenu(ContextMenuStrip menu){
  string assets=Path.Combine(AppContext.BaseDirectory,"assets");var root=new ToolStripMenuItem("HUD appearance");
  void Change(Action<AppearanceSettings> edit){try{var s=AppearanceSettings.Load(assets);edit(s);s.Save(assets);ApplySettings();}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save appearance");}}
  var interfaces=new ToolStripMenuItem("Interface style");
  foreach(string style in new[]{"JARVIS","SHIELD"}){var option=new ToolStripMenuItem(style=="SHIELD"?"S.H.I.E.L.D. operations":"J.A.R.V.I.S. reactor"){Tag=style};option.Click+=(_,_)=>Change(s=>s.Interface=style);interfaces.DropDownItems.Add(option);}interfaces.DropDownOpening+=(_,_)=>{foreach(var option in interfaces.DropDownItems.OfType<ToolStripMenuItem>())option.Checked=(string)option.Tag==AppearanceSettings.Load(assets).Interface;};root.DropDownItems.Add(interfaces);
  var maps=new ToolStripMenuItem("SHIELD map view");foreach(string view in new[]{"World","North America","Europe","Asia-Pacific"}){var option=new ToolStripMenuItem(view){Tag=view};option.Click+=(_,_)=>Change(s=>s.MapView=view);maps.DropDownItems.Add(option);}maps.DropDownOpening+=(_,_)=>{foreach(var option in maps.DropDownItems.OfType<ToolStripMenuItem>())option.Checked=(string)option.Tag==AppearanceSettings.Load(assets).MapView;};root.DropDownItems.Add(maps);
  var movie=new ToolStripMenuItem("Use supplied JARVIS intro");movie.Click+=(_,_)=>Change(s=>s.MovieIntro=!s.MovieIntro);root.DropDownOpening+=(_,_)=>movie.Checked=AppearanceSettings.Load(assets).MovieIntro;root.DropDownItems.Add(movie);
  var depth=new ToolStripMenuItem("Holographic map depth");depth.Click+=(_,_)=>Change(s=>s.MapDepth=!s.MapDepth);root.DropDownOpening+=(_,_)=>depth.Checked=AppearanceSettings.Load(assets).MapDepth;root.DropDownItems.Add(depth);
  var engineering=new ToolStripMenuItem("Detailed JARVIS engineering view");engineering.Click+=(_,_)=>Change(s=>s.EngineeringView=!s.EngineeringView);root.DropDownOpening+=(_,_)=>engineering.Checked=AppearanceSettings.Load(assets).EngineeringView;root.DropDownItems.Add(engineering);
  var repeats=new ToolStripMenuItem("Cinematic refresh interval");foreach(int minutes in new[]{0,2,5,10,15}){var option=new ToolStripMenuItem(minutes==0?"Off":minutes+" minutes"){Tag=minutes};option.Click+=(_,_)=>Change(s=>s.RepeatMinutes=minutes);repeats.DropDownItems.Add(option);}repeats.DropDownOpening+=(_,_)=>{foreach(var option in repeats.DropDownItems.OfType<ToolStripMenuItem>())option.Checked=(int)option.Tag==AppearanceSettings.Load(assets).RepeatMinutes;};root.DropDownItems.Add(repeats);
  var rich=new ToolStripMenuItem("Enhanced SHIELD map effects");rich.Click+=(_,_)=>{Change(s=>s.RichMapAnimation=!s.RichMapAnimation);};root.DropDownOpening+=(_,_)=>rich.Checked=AppearanceSettings.Load(assets).RichMapAnimation;root.DropDownItems.Add(rich);
  var mapMotion=new ToolStripMenuItem("Animate SHIELD map");mapMotion.Click+=(_,_)=>{Change(s=>s.MapAnimation=!s.MapAnimation);mapMotion.Checked=AppearanceSettings.Load(assets).MapAnimation;};root.DropDownOpening+=(_,_)=>mapMotion.Checked=AppearanceSettings.Load(assets).MapAnimation;root.DropDownItems.Add(mapMotion);
  var opacity=new ToolStripMenuItem("Transparent panel darkness"){DropDown=new ThemeDropDownMenu()};
  foreach(int value in new[]{70,88,100}){var item=new StickyThemeMenuItem(value==70?"Light (70%)":value==88?"Balanced (88%)":"Solid (100%)"){Tag=value};item.Click+=(_,_)=>{Change(s=>s.PanelOpacity=value);foreach(var option in opacity.DropDownItems.OfType<ToolStripMenuItem>())option.Checked=option.Tag is int n&&n==AppearanceSettings.Load(assets).PanelOpacity;};opacity.DropDownItems.Add(item);}
  opacity.DropDownItems.Add(new ToolStripMenuItem("Done"));root.DropDownItems.Add(opacity);
  foreach(var choice in new[]{("Hardware panel",nameof(AppearanceSettings.Hardware)),("History graphs",nameof(AppearanceSettings.History)),("Audio spectrum",nameof(AppearanceSettings.Spectrum)),("Reactor rings",nameof(AppearanceSettings.Reactor)),("Boot sequence at startup",nameof(AppearanceSettings.BootSequence))}){
   var property=typeof(AppearanceSettings).GetProperty(choice.Item2);var item=new ToolStripMenuItem(choice.Item1);item.Click+=(_,_)=>{Change(s=>property.SetValue(s,!(bool)property.GetValue(s)));item.Checked=(bool)property.GetValue(AppearanceSettings.Load(assets));};root.DropDownItems.Add(item);
   root.DropDownOpening+=(_,_)=>item.Checked=(bool)property.GetValue(AppearanceSettings.Load(assets));
  }
  root.DropDownOpening+=(_,_)=>{foreach(var option in opacity.DropDownItems.OfType<ToolStripMenuItem>())option.Checked=option.Tag is int n&&n==AppearanceSettings.Load(assets).PanelOpacity;};
  root.DropDownItems.Add("Play boot sequence",null,(_,_)=>ReplayBoot());
  root.DropDownItems.Add(new ToolStripSeparator());var reset=new ToolStripMenuItem("Reset appearance");reset.Click+=(_,_)=>{try{new AppearanceSettings().Save(assets);ApplySettings();}catch(Exception ex){MessageBox.Show(ex.Message,"Could not reset appearance");}};root.DropDownItems.Add(reset);menu.Items.Add(root);
 }
}
