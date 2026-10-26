using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace HudWall;
internal sealed class VoiceSettings {
 public string WeatherLocation {get;set;}="";
 public bool Enabled {get;set;}=true;
 public bool SpokenReplies {get;set;}=true;
 public bool Greeting {get;set;}=true;
 public string SpeechEngine {get;set;}="Kokoro";
 public string NeuralVoice {get;set;}="bm_george";
 public double SpeechSpeed {get;set;}=1;
 public string Voice {get;set;}="";
 public Dictionary<string,string> Apps {get;set;}=DefaultApps();
 private static Dictionary<string,string> DefaultApps(){
  var apps=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["notepad"]="notepad.exe",["calculator"]="calc.exe",["file explorer"]="explorer.exe",["settings"]="ms-settings:",["browser"]="https://www.bing.com"};
  var known=new Dictionary<string,string>{{"Discord","discord"},{"Steam","steam"},{"Google Chrome","chrome"},{"Microsoft Edge","edge"},{"Spotify","spotify"}};
  foreach(string folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)})try{if(!Directory.Exists(folder))continue;foreach(string link in Directory.EnumerateFiles(folder,"*.lnk",SearchOption.AllDirectories))if(known.TryGetValue(Path.GetFileNameWithoutExtension(link),out string alias))apps.TryAdd(alias,link);}catch{}
  return apps;
 }
 internal static VoiceSettings Load(string assets){try{var s=JsonSerializer.Deserialize<VoiceSettings>(SettingsStorage.Read(Path.Combine(assets,"voice.json")))??new();if(s.SpeechEngine!="Kokoro"&&s.SpeechEngine!="Windows")s.SpeechEngine="Kokoro";if(!new[]{"bm_george","bm_lewis","bm_daniel"}.Contains(s.NeuralVoice))s.NeuralVoice="bm_george";s.SpeechSpeed=double.IsFinite(s.SpeechSpeed)?Math.Clamp(s.SpeechSpeed,.8,1.2):1;s.Apps=new((s.Apps??new()).Where(p=>Regex.IsMatch(p.Key??"","^[a-zA-Z][a-zA-Z0-9 ]{0,39}$")&&ValidTarget(p.Value)).ToDictionary(p=>p.Key.Trim().ToLowerInvariant(),p=>p.Value),StringComparer.OrdinalIgnoreCase);return s;}catch(Exception ex)when(ex is IOException||ex is JsonException||ex is ArgumentException){return new();}}
 internal void Save(string assets)=>SettingsStorage.Write(Path.Combine(assets,"voice.json"),JsonSerializer.Serialize(this));
 internal static bool ValidTarget(string value)=>value is "notepad.exe" or "calc.exe" or "explorer.exe" or "ms-settings:" or "https://www.bing.com" || (!string.IsNullOrWhiteSpace(value)&&Path.IsPathFullyQualified(value)&&File.Exists(value)&&new[]{".exe",".lnk"}.Contains(Path.GetExtension(value),StringComparer.OrdinalIgnoreCase));
}
internal sealed class VoiceAppsDialog:Form {
 private readonly DataGridView grid=new(){Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
 internal Dictionary<string,string> Apps {get;private set;}
 internal VoiceAppsDialog(Dictionary<string,string> apps){
  Text="JARVIS voice apps";Width=820;Height=440;StartPosition=FormStartPosition.CenterScreen;grid.Columns.Add("Alias","Say: Jarvis open ...");grid.Columns.Add("Target","Application or shortcut");grid.Columns[1].ReadOnly=true;foreach(var app in apps)grid.Rows.Add(app.Key,app.Value);
  var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48};var add=new Button{Text="Add app...",AutoSize=true};add.Click+=(_,_)=>{using var pick=new OpenFileDialog{Filter="Apps and shortcuts|*.exe;*.lnk",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.Programs),CheckFileExists=true};if(pick.ShowDialog(this)==DialogResult.OK)grid.Rows.Add(Path.GetFileNameWithoutExtension(pick.FileName).ToLowerInvariant(),pick.FileName);};bar.Controls.Add(add);
  var remove=new Button{Text="Remove",AutoSize=true};remove.Click+=(_,_)=>{if(grid.CurrentRow!=null)grid.Rows.Remove(grid.CurrentRow);};bar.Controls.Add(remove);
  var save=new Button{Text="Save",AutoSize=true};save.Click+=(_,_)=>{grid.EndEdit();var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(DataGridViewRow row in grid.Rows){string alias=Convert.ToString(row.Cells[0].Value)?.Trim().ToLowerInvariant()??"",target=Convert.ToString(row.Cells[1].Value);if(!Regex.IsMatch(alias,"^[a-z][a-z0-9 ]{0,39}$")||!VoiceSettings.ValidTarget(target)||!result.TryAdd(alias,target)){MessageBox.Show("Use a unique spoken name with letters, numbers and spaces, and an existing app or shortcut.");return;}}Apps=result;DialogResult=DialogResult.OK;};bar.Controls.Add(save);Controls.Add(grid);Controls.Add(bar);
 }
}
