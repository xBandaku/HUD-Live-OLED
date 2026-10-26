using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace HudWall;
internal sealed class CommandCentreSettings {
 public string Mode {get;set;}="Auto";
 public bool SoundEffects {get;set;}=true;
 public int EffectsVolume {get;set;}=15;
 public Dictionary<string,string[]> Routines {get;set;}=new(){["work mode"]=new[]{"browser"},["gaming mode"]=new[]{"steam","discord"}};
 internal static CommandCentreSettings Load(string assets){try{var s=JsonSerializer.Deserialize<CommandCentreSettings>(SettingsStorage.Read(Path.Combine(assets,"command-centre.json")))??new();if(!new[]{"Auto","Ambient","Command","Showcase"}.Contains(s.Mode))s.Mode="Auto";s.EffectsVolume=Math.Clamp(s.EffectsVolume,0,40);s.Routines??=new();return s;}catch{return new();}}
 internal void Save(string assets)=>SettingsStorage.Write(Path.Combine(assets,"command-centre.json"),JsonSerializer.Serialize(this));
}
internal sealed record CentreEvent(long id,string time,string type,string text);
internal sealed partial class HudForm {
 private readonly List<CentreEvent> _centreEvents=new();
 private long _centreSequence;
 private object _centreContext=new{kind="standby",title="SYSTEM STANDBY",body="Awaiting your request",metrics=Array.Empty<string>()};
 private string _modelSetup="Checking local models";
 private void CentreEvent(string type,string text){_centreEvents.Add(new(++_centreSequence,DateTime.Now.ToString("HH:mm:ss"),type,text.Length>400?text[..400]:text));while(_centreEvents.Count>30)_centreEvents.RemoveAt(0);}
 private void CentreRequest(string question){
  question=Regex.Replace(question,@"\bsk-[A-Za-z0-9_-]{20,}","[redacted credential]");
  var settings=CommandCentreSettings.Load(MonitorAssets);if(settings.Mode=="Showcase"){settings.Mode="Auto";settings.Save(MonitorAssets);ApplySettings();}
  string kind=Regex.IsMatch(question,@"\b(weather|forecast)\b",RegexOptions.IgnoreCase)?"weather":Regex.IsMatch(question,@"\b(system|processor|memory|gpu|status report)\b",RegexOptions.IgnoreCase)?"system":Regex.IsMatch(question,@"\b(open|launch|close|quit|mode|workspace)\b",RegexOptions.IgnoreCase)?"apps":Regex.IsMatch(question,@"\b(file|find|search|folder)\b",RegexOptions.IgnoreCase)?"search":"conversation";
  _centreContext=new{kind,title=question,body="Request received. Preparing response.",metrics=Array.Empty<string>()};CentreEvent("REQUEST",question);
 }
 private object CentreSnapshot()=>new{context=_centreContext,events=_centreEvents.ToArray(),models=new{setup=_modelSetup,voice=!File.Exists(NeuralModelPath)?"Missing":_neuralLoad?.IsFaulted==true?"Windows fallback":_neuralLoad?.IsCompletedSuccessfully==true?"Neural voice ready":_neuralLoad!=null?"Loading neural voice":"Model available",recognition=_localInput?.Ready==true?"Whisper ready":File.Exists(LocalSpeechInput.ModelPath)?"Whisper available":"Windows fallback"}};
 private bool HandleCommandCentreCommand(string command){
  command=Regex.Replace(command,@"^(?:please )?(?:engage|activate|enable|start|run|switch to|enter) ","",RegexOptions.IgnoreCase);
  string mode=command switch{"ambient mode" or "standby mode"=>"Ambient","command mode" or "mission mode"=>"Command","showcase mode" or "demonstration mode" or "showcase"=>"Showcase","automatic mode" or "stop showcase" or "exit showcase"=>"Auto",_=>null};
  if(mode!=null){SetCentreMode(mode);Reply(mode=="Showcase"?"Showcase mode. All demonstrated activity is simulated.":mode+" interface enabled.");return true;}
  string routine=command is "prepare my workspace" or "prepare my work space"?"work mode":command;
  var settings=CommandCentreSettings.Load(MonitorAssets);
  if(!settings.Routines.TryGetValue(routine,out var apps))return false;
  if(!_aiSettings.PcTools){Reply("PC tools are disabled. Enable them to launch a routine.");return true;}
  var opened=new List<string>();var failed=new List<string>();
  foreach(string alias in apps.Take(8)){if(!_voice.Apps.TryGetValue(alias,out var target)||!VoiceSettings.ValidTarget(target)){failed.Add(alias);continue;}try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});opened.Add(alias);CentreEvent("LAUNCHED",alias);}catch{failed.Add(alias);CentreEvent("UNAVAILABLE",alias);}}
  string summary=(opened.Count>0?"Opened "+string.Join(", ",opened)+".":"No configured apps could be opened.")+(failed.Count>0?" Unavailable: "+string.Join(", ",failed)+".":"");
  _centreContext=new{kind="apps",title=routine.ToUpperInvariant(),body=summary,metrics=opened.Select(a=>"Launched / "+a).ToArray()};_lastAction=routine;Reply(summary);return true;
 }
 private void SetCentreMode(string mode){try{if(mode=="Showcase")EndAiConversation();var settings=CommandCentreSettings.Load(MonitorAssets);settings.Mode=mode;settings.Save(MonitorAssets);ApplySettings();}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save command-centre mode");}}
 private void AddCommandCentreMenu(ContextMenuStrip menu){
  var root=new ToolStripMenuItem("Command centre");
  foreach(string mode in new[]{"Auto","Ambient","Command","Showcase"}){var item=new ToolStripMenuItem(mode=="Showcase"?"Showcase (simulated activity)":mode){Tag=mode};item.Click+=(_,_)=>SetCentreMode(mode);root.DropDownItems.Add(item);}
  root.DropDownOpening+=(_,_)=>{var s=CommandCentreSettings.Load(MonitorAssets);foreach(var item in root.DropDownItems.OfType<ToolStripMenuItem>())if(item.Tag is string tag)item.Checked=tag==s.Mode;};
  var effects=new ToolStripMenuItem("Interface sound effects");effects.Click+=(_,_)=>{var s=CommandCentreSettings.Load(MonitorAssets);s.SoundEffects=!s.SoundEffects;s.Save(MonitorAssets);ApplySettings();};root.DropDownOpening+=(_,_)=>effects.Checked=CommandCentreSettings.Load(MonitorAssets).SoundEffects;root.DropDownItems.Add(effects);
  var volume=new ToolStripMenuItem("Effects volume");foreach(int amount in new[]{0,8,15,25,40}){var item=new ToolStripMenuItem(amount+"%"){Tag=amount};item.Click+=(_,_)=>{var s=CommandCentreSettings.Load(MonitorAssets);s.EffectsVolume=amount;s.Save(MonitorAssets);ApplySettings();};volume.DropDownItems.Add(item);}volume.DropDownOpening+=(_,_)=>{foreach(var item in volume.DropDownItems.OfType<ToolStripMenuItem>())item.Checked=(int)item.Tag==CommandCentreSettings.Load(MonitorAssets).EffectsVolume;};root.DropDownItems.Add(volume);
  root.DropDownItems.Add("Configure app routines…",null,(_,_)=>ConfigureCentreRoutines());
  root.DropDownItems.Add("Session timeline…",null,(_,_)=>MessageBox.Show(string.Join("\n",_centreEvents.Select(e=>$"{e.time} / {e.type} / {e.text}")),"JARVIS session timeline"));
  root.DropDownItems.Add("Local model readiness…",null,(_,_)=>MessageBox.Show("Voice model: "+NeuralModelPath+"\nRecognition model: "+LocalSpeechInput.ModelPath+"\n\n"+_modelSetup+"\nNeural engine: "+(_neuralLoad?.IsCompletedSuccessfully==true?"Ready":"Not loaded / Windows fallback available")+"\nRecognition: "+(_localInput?.Ready==true?"Whisper ready":"Windows fallback / starting"),"Local model readiness"));
  root.DropDownItems.Add("Check / install missing speech models",null,(_,_)=>EnsureSpeechModels());menu.Items.Add(root);
 }
 private void ShowCentreTimeline(){
  using var dialog=new Form{Text="JARVIS session timeline",Width=760,Height=500,StartPosition=FormStartPosition.CenterScreen,Icon=Icon};
  dialog.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Text=string.Join(Environment.NewLine+Environment.NewLine,_centreEvents.Select(e=>$"{e.time} / {e.type} / {e.text}"))});dialog.ShowDialog();
 }
 private void ConfigureCentreRoutines(){
  var settings=CommandCentreSettings.Load(MonitorAssets);using var dialog=new Form{Text="JARVIS app routines",Width=650,Height=430,StartPosition=FormStartPosition.CenterScreen,Icon=Icon};
  var label=new Label{Text="One routine per line: work mode = browser, notepad\nUse aliases from JARVIS voice > Configure app commands. Up to 8 apps per routine.",Dock=DockStyle.Top,Height=55};
  var input=new TextBox{Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Text=string.Join(Environment.NewLine,settings.Routines.Select(r=>r.Key+" = "+string.Join(", ",r.Value)))};
  var save=new Button{Text="Save routines",Dock=DockStyle.Bottom,Height=40};save.Click+=(_,_)=>{var routines=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);foreach(string line in input.Lines.Where(x=>!string.IsNullOrWhiteSpace(x))){var parts=line.Split('=',2);if(parts.Length!=2||!Regex.IsMatch(parts[0].Trim(),@"^[a-zA-Z][a-zA-Z ]{2,35}$")){MessageBox.Show("Use routine name = app alias, app alias.");return;}var aliases=parts[1].Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);if(aliases.Length is <1 or >8||aliases.Any(a=>!_voice.Apps.ContainsKey(a))){MessageBox.Show("Choose between 1 and 8 configured app aliases per routine.");return;}routines[parts[0].Trim().ToLowerInvariant()]=aliases;}if(routines.Count>8){MessageBox.Show("Use up to 8 routines.");return;}settings.Routines=routines;settings.Save(MonitorAssets);RestartVoice();dialog.DialogResult=DialogResult.OK;};dialog.Controls.Add(input);dialog.Controls.Add(label);dialog.Controls.Add(save);dialog.ShowDialog();
 }
}
