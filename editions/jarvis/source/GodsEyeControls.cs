using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace HudWall;
internal sealed class GodsEyeSettings {
 public bool Enabled {get;set;}=true;
 public string Preset {get;set;}="Global";
 public string Focus {get;set;}="World";
 internal static GodsEyeSettings Load(string assets){try{return JsonSerializer.Deserialize<GodsEyeSettings>(SettingsStorage.Read(Path.Combine(assets,"gods-eye.json")))??new();}catch{return new();}}
 internal void Save(string assets)=>SettingsStorage.Write(Path.Combine(assets,"gods-eye.json"),JsonSerializer.Serialize(this));
}
internal sealed partial class HudForm {
 private Process _godsEyeProcess;
 private string _godsEyeUrl="",_godsEyeStatus="Starting local globe";
 private bool _godsEyeStarting,_godsEyeClosed;
 private GodsEyeConsole _godsEyeConsole;
 private object GodsEyePacket(){var s=GodsEyeSettings.Load(MonitorAssets);return new{enabled=s.Enabled,url=_godsEyeUrl,status=_godsEyeStatus,preset=s.Preset,focus=s.Focus};}
 private async Task InitializeGodsEyeAsync(){
  FormClosed+=(_,_)=>{_godsEyeClosed=true;StopGodsEye();};await StartGodsEyeAsync();
 }
 private async Task StartGodsEyeAsync(){
  if(_godsEyeStarting||_godsEyeClosed||!GodsEyeSettings.Load(MonitorAssets).Enabled)return;
  if(_godsEyeProcess!=null&&!_godsEyeProcess.HasExited){ApplySettings();return;}
  _godsEyeStarting=true;_godsEyeStatus="Starting local globe";
  try{
   string root=Path.Combine(AppContext.BaseDirectory,"gods-eye"),node=Path.Combine(root,"node.exe");if(!File.Exists(node))throw new FileNotFoundException("God's Eye runtime is missing");
   int port;using(var listener=new TcpListener(IPAddress.Loopback,0)){listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;}
   var start=new ProcessStartInfo(node){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(Path.Combine(root,"hudwall-server.mjs"));start.ArgumentList.Add(Environment.ProcessId.ToString());start.ArgumentList.Add(port.ToString());
   _godsEyeProcess=new Process{StartInfo=start,EnableRaisingEvents=true};var child=_godsEyeProcess;
   child.OutputDataReceived+=(_,e)=>{if(e.Data?.StartsWith("HUDWALL_")==true)Program.Log(e.Data);};child.ErrorDataReceived+=(_,e)=>{if(!string.IsNullOrEmpty(e.Data))Program.Log("GodsEye: "+e.Data);};
   child.Exited+=(_,_)=>{if(!_godsEyeClosed&&IsHandleCreated)try{BeginInvoke(()=>{if(_godsEyeProcess==child){_godsEyeUrl="";_godsEyeStatus="Globe stopped / archive atlas available";ApplySettings();}});}catch{}};
   child.Start();child.BeginOutputReadLine();child.BeginErrorReadLine();using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(2)};
   string endpoint="http://127.0.0.1:"+port;
   bool ready=false;for(int i=0;i<60&&!_godsEyeClosed&&!child.HasExited;i++){try{using var response=JsonDocument.Parse(await http.GetStringAsync(endpoint+"/__hudwall_health"));if(response.RootElement.GetProperty("pid").GetInt32()==child.Id){ready=true;break;}}catch{}await Task.Delay(300);}
   if(!ready)throw new IOException("Local globe server did not become ready");
   if(!GodsEyeSettings.Load(MonitorAssets).Enabled||_godsEyeClosed){StopGodsEye();return;}
   _godsEyeUrl=endpoint;_godsEyeStatus="Local server ready / loading public feeds";Program.Log("God's Eye local server ready on loopback port "+port);
  }catch(Exception e){StopGodsEye();_godsEyeStatus="Globe unavailable / archive atlas available";Program.Log("God's Eye startup: "+e.Message);}finally{_godsEyeStarting=false;if(!_godsEyeClosed)ApplySettings();}
 }
 private void StopGodsEye(){_godsEyeConsole?.Close();_godsEyeConsole=null;var child=_godsEyeProcess;_godsEyeProcess=null;_godsEyeUrl="";try{if(child!=null&&!child.HasExited)child.Kill(true);}catch{}child?.Dispose();}
 private bool HandleGodsEyeCommand(string command){
  if(command is "focus on the united states" or "focus on united states" or "show united states" or "show the united states" or "focus on the world" or "show whole globe"){var s=GodsEyeSettings.Load(MonitorAssets);s.Enabled=true;s.Focus=command.Contains("united states")?"United States":"World";s.Save(MonitorAssets);_=StartGodsEyeAsync();ApplySettings();Reply("SHIELD map focused on "+s.Focus+".");return true;}

  string preset=command switch {"show global globe" or "show gods eye" or "show god's eye"=>"Global","show air traffic"=>"Air traffic","show satellites"=>"Space","show earthquakes"=>"Earthquakes",_=>null};
  if(command is "show archive map" or "hide gods eye" or "hide god's eye"){var s=GodsEyeSettings.Load(MonitorAssets);s.Enabled=false;s.Save(MonitorAssets);StopGodsEye();ApplySettings();Reply("SHIELD archive map restored.");return true;}
  if(preset==null)return false;var settings=GodsEyeSettings.Load(MonitorAssets);settings.Enabled=true;settings.Preset=preset;settings.Save(MonitorAssets);_=StartGodsEyeAsync();ApplySettings();Reply("SHIELD display set to "+preset+". Public feeds require an internet connection.");return true;
 }
 private void AddGodsEyeMenu(ContextMenuStrip menu){
  var root=new ToolStripMenuItem("SHIELD / God's Eye");var enabled=new ToolStripMenuItem("Show integrated 3D globe");root.DropDownItems.Add(enabled);root.DropDownOpening+=(_,_)=>enabled.Checked=GodsEyeSettings.Load(MonitorAssets).Enabled;
  enabled.Click+=async(_,_)=>{var s=GodsEyeSettings.Load(MonitorAssets);s.Enabled=!s.Enabled;s.Save(MonitorAssets);if(s.Enabled)await StartGodsEyeAsync();else StopGodsEye();ApplySettings();};
  var focusMenu=new ToolStripMenuItem("Focus");foreach(var focus in new[]{"United States","World"}){var item=new ToolStripMenuItem(focus);item.Click+=(_,_)=>{var s=GodsEyeSettings.Load(MonitorAssets);s.Focus=focus;s.Save(MonitorAssets);ApplySettings();};focusMenu.DropDownItems.Add(item);focusMenu.DropDownOpening+=(_,_)=>item.Checked=GodsEyeSettings.Load(MonitorAssets).Focus==focus;}root.DropDownItems.Add(focusMenu);
  foreach(var preset in new[]{"Global","Air traffic","Space","Earthquakes"}){var item=new ToolStripMenuItem(preset);item.Click+=(_,_)=>{var s=GodsEyeSettings.Load(MonitorAssets);s.Preset=preset;s.Save(MonitorAssets);ApplySettings();};root.DropDownItems.Add(item);root.DropDownOpening+=(_,_)=>item.Checked=GodsEyeSettings.Load(MonitorAssets).Preset==preset;}
  root.DropDownItems.Add("Open interactive console...",null,async(_,_)=>{await StartGodsEyeAsync();if(_godsEyeUrl.Length==0)return;if(_godsEyeConsole==null||_godsEyeConsole.IsDisposed){_godsEyeConsole=new GodsEyeConsole(_godsEyeUrl+(GodsEyeSettings.Load(MonitorAssets).Focus=="United States"?"/#v=2&lat=39&lon=-98&alt=6500000&heading=0&pitch=-90&roll=0&map=esri-imagery":"/#v=2&lat=20&lon=-25&alt=20000000&heading=0&pitch=-90&roll=0&map=esri-imagery"),Icon);_godsEyeConsole.Show();}else{_godsEyeConsole.Show();_godsEyeConsole.Activate();}});
  root.DropDownItems.Add("Restart local globe",null,async(_,_)=>{StopGodsEye();await StartGodsEyeAsync();});
  root.DropDownItems.Add("About / setup guide",null,(_,_)=>Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"GODS EYE.txt")){UseShellExecute=true}));menu.Items.Add(root);
 }
}
internal sealed class GodsEyeConsole:Form {
 internal GodsEyeConsole(string url,Icon icon){Icon=(Icon)icon.Clone();Text="SHIELD / God's Eye View";Width=1440;Height=900;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(3,14,26);var web=new WebView2{Dock=DockStyle.Fill};Controls.Add(web);Shown+=async(_,_)=>{try{await web.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HudWall","GodsEyeWebView")));web.CoreWebView2.Settings.IsStatusBarEnabled=false;web.CoreWebView2.Navigate(url);}catch(Exception e){MessageBox.Show(e.Message,"God's Eye console");}};}
}
