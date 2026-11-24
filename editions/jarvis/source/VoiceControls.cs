using System.Drawing;
using System.Diagnostics;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Windows.Forms;
namespace HudWall;
internal sealed partial class HudForm {
 private SpeechRecognitionEngine _recognizer;
 private SpeechSynthesizer _speaker;
 private VoiceSettings _voice;
 private bool _speaking,_voiceDisposed;
 private Prompt _speakingPrompt;
 private DateTime _ignoreUntil,_lastCommand;
 private ToolStripMenuItem _voiceStatusItem;
 private string _lastAction="";
 private string _voiceStatus="Voice starting",_voiceReply="Say: Jarvis, help";
 internal static readonly string[] VoiceCommands={"hello","help","what time is it","what is the date","system status","pause wallpaper","resume wallpaper","transparency on","transparency off","span monitors","separate monitors","stop listening"};
 private void AddVoiceMenu(ContextMenuStrip menu){
  _voice=VoiceSettings.Load(MonitorAssets);var root=new ToolStripMenuItem("JARVIS voice");_voiceStatusItem=new ToolStripMenuItem(_voiceStatus){Enabled=false};root.DropDownItems.Add(_voiceStatusItem);
  AddMicrophoneMenu(root);
  var enabled=new ToolStripMenuItem("Listen for Jarvis commands");enabled.Click+=(_,_)=>{_voice.Enabled=!_voice.Enabled;SaveVoice();RestartVoice();};root.DropDownItems.Add(enabled);
  var replies=new ToolStripMenuItem("Spoken replies");replies.Click+=(_,_)=>{_voice.SpokenReplies=!_voice.SpokenReplies;SaveVoice();};root.DropDownItems.Add(replies);  var greeting=new ToolStripMenuItem("Spoken greeting at startup");greeting.Click+=(_,_)=>{_voice.Greeting=!_voice.Greeting;SaveVoice();};root.DropDownItems.Add(greeting);
  root.DropDownItems.Add(new ToolStripMenuItem("Voice: British neural (automatic)"){Enabled=false});
  var pacing=new ToolStripMenuItem("Speech speed");foreach(double speed in new[]{.9,1,1.1}){var item=new ToolStripMenuItem(speed==.9?"Measured (90%)":speed==1?"Natural (100%)":"Brisk (110%)"){Tag=speed};item.Click+=(_,_)=>{_voice.SpeechSpeed=speed;SaveVoice();};pacing.DropDownItems.Add(item);}pacing.DropDownOpening+=(_,_)=>{foreach(var item in pacing.DropDownItems.OfType<ToolStripMenuItem>())item.Checked=item.Tag is double value&&Math.Abs(_voice.SpeechSpeed-value)<.001;};root.DropDownItems.Add(pacing);
  root.DropDownItems.Add("Configure app commands...",null,(_,_)=>{using var dialog=new VoiceAppsDialog(_voice.Apps);if(dialog.ShowDialog()==DialogResult.OK){_voice.Apps=dialog.Apps;SaveVoice();RestartVoice();}});
  root.DropDownItems.Add("Test spoken response",null,(_,_)=>Reply("JARVIS online. Your systems are ready."));
  root.DropDownItems.Add("Command guide",null,(_,_)=>MessageBox.Show("Say Jarvis before each command.\n\n"+string.Join("\n",VoiceCommands.Select(c=>"Jarvis "+c))+"\n\nCinematic:\n"+string.Join("\n",CinematicVoiceCommands.Select(c=>"Jarvis "+c))+"\n\nDesktop organizer:\n"+string.Join("\n",OrganizerVoiceCommands.Select(c=>"Jarvis "+c))+"\n\n"+string.Join("\n",_voice.Apps.Keys.Select(c=>"Jarvis open "+c))+"\n\nVoice commands run locally. Use Configure app commands to add apps. AI conversation handles other questions when the selected AI provider is ready. Say stop conversation to end follow-up listening.","JARVIS voice commands"));
  root.DropDownItems.Add("Windows microphone settings",null,(_,_)=>Process.Start(new ProcessStartInfo("ms-settings:privacy-microphone"){UseShellExecute=true}));
  root.DropDownOpening+=(_,_)=>{enabled.Checked=_voice.Enabled;replies.Checked=_voice.SpokenReplies;greeting.Checked=_voice.Greeting;};menu.Items.Add(root);
 }
 private void SaveVoice(){try{_voice.Save(MonitorAssets);}catch(Exception ex){SetVoiceStatus("Could not save voice settings: "+ex.Message);}}
 private void InitializeVoice(){
  _voice.SpeechEngine="Kokoro";_voice.NeuralVoice="bm_george";
  InitializeConversation();RefreshVoiceOutput();EnsureSpeechModels();
  try{_speaker=new SpeechSynthesizer();if(!string.IsNullOrEmpty(_voice.Voice))try{_speaker.SelectVoice(_voice.Voice);}catch{} _speaker.SpeakCompleted+=(_,e)=>VoiceUi(()=>{if(e.Prompt!=_speakingPrompt)return;_speaking=false;_ignoreUntil=DateTime.UtcNow.AddMilliseconds(900);_conversationUntil=DateTime.UtcNow.AddSeconds(90);SetVoiceStatus(ListeningStatus);});}catch(Exception ex){SetVoiceStatus("Speech output unavailable: "+ex.Message);}
  FormClosed+=(_,_)=>{_voiceDisposed=true;_modelSetupStop.Cancel();try{_cueOutput?.Stop();}catch{}StopLocalInput();StopNeuralSpeech();try{_neuralSynth?.Dispose();}catch{}try{_recognizer?.RecognizeAsyncCancel();_recognizer?.Dispose();_speaker?.SpeakAsyncCancelAll();_speaker?.Dispose();}catch{}};
  RestartVoice();
 }
 private void VoiceUi(Action action){if(_voiceDisposed||IsDisposed||!IsHandleCreated)return;try{BeginInvoke(action);}catch(InvalidOperationException){}}
 private void RestartVoice(){
  StopLocalInput();EndAiConversation();_aiTools=null;_followupGrammar=null;
  try{var old=_recognizer;_recognizer=null;if(old!=null){old.RecognizeAsyncCancel();old.Dispose();}
   if(!_voice.Enabled){SetVoiceStatus("Voice off");return;}
   StartLocalInput();
   var info=SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r=>r.Culture.TwoLetterISOLanguageName=="en");if(info==null){SetVoiceStatus("Install an English Windows speech recognizer");return;}
   var engine=new SpeechRecognitionEngine(info);var phrases=VoiceCommands.Concat(OrganizerVoiceCommands).Concat(CinematicVoiceCommands).Concat(new[]{"ambient mode","command mode","mission mode","showcase mode","automatic mode","prepare my workspace"}).Concat(CommandCentreSettings.Load(MonitorAssets).Routines.Keys).Concat(_voice.Apps.Keys.Select(a=>"open "+a)).Select(c=>"jarvis "+c).Append("jarvis").ToArray();
   var grammar=new GrammarBuilder{Culture=info.Culture};grammar.Append(new Choices(phrases));engine.LoadGrammar(new Grammar(grammar){Name="Local commands",Priority=2});
   ConfigureConversationGrammar(engine,info.Culture);
   engine.SpeechRecognized+=(_,e)=>{if(e.Result.Confidence>=(e.Result.Grammar.Name=="Local commands"?.55:.40))VoiceUi(()=>{if(_localInput?.Ready==true||_recognizer!=engine||(!ContinuousListening&&(_speaking||DateTime.UtcNow<_ignoreUntil))||DateTime.UtcNow-_lastCommand<TimeSpan.FromMilliseconds(250))return;_lastCommand=DateTime.UtcNow;if(ContinuousListening&&(_speaking||_aiBusy))EndAiConversation();HandleRecognizedSpeech(e.Result.Text,e.Result.Grammar.Name);});};
   engine.RecognizeCompleted+=(_,e)=>VoiceUi(()=>{if(_recognizer==engine&&e.Error!=null)SetVoiceStatus("Microphone unavailable: "+e.Error.Message);});
   try{engine.SetInputToDefaultAudioDevice();_recognizer=engine;engine.EndSilenceTimeout=TimeSpan.FromMilliseconds(650);engine.EndSilenceTimeoutAmbiguous=TimeSpan.FromMilliseconds(1000);engine.RecognizeAsync(RecognizeMode.Multiple);UpdateListeningGrammar();}catch{_recognizer=null;engine.Dispose();throw;}
   SetVoiceStatus(ListeningStatus);
  }catch(Exception ex){SetVoiceStatus("Voice unavailable: "+ex.Message);}
 }
 private void HandleVoiceCommand(string phrase){
  string utterance=phrase.Trim();string command=utterance.ToLowerInvariant().TrimEnd('.','!','?');
  if(command=="jarvis"){SetConversationActive(true);Reply("At your service, "+Environment.UserName+". What would you like to discuss?");return;}
  if(!command.StartsWith("jarvis ")&&!command.StartsWith("jarvis,")&&!command.StartsWith("jarvis:"))return;
  command=command[6..].TrimStart(' ',':',',');string question=utterance[6..].TrimStart(' ',':',',');
  if(command is "stop conversation" or "end conversation" or "stop response"){EndAiConversation();return;}
  if(command=="new conversation"){NewAiConversation();Reply("Ready for a new conversation.");SetConversationActive(true);return;}
  SetConversationActive(true);
  CentreRequest(question);
  if(HandleGodsEyeCommand(command))return;
  if(HandleTaskbarCommand(command))return;
  if(HandleCommandCentreCommand(command))return;
  if(HandleCinematicCommand(command))return;  if(HandleOrganizerCommand(command))return;
  if(HandleBasicRequest(question))return;
  var naturalOpen=System.Text.RegularExpressions.Regex.Match(command,@"^(?:(?:can|could|would) you (?:please )?)?(?:open|launch|start) (.+?)(?: please)?$");
  if(naturalOpen.Success&&_voice.Apps.ContainsKey(naturalOpen.Groups[1].Value))command="open "+naturalOpen.Groups[1].Value;
  if(command.StartsWith("open ")){string alias=command[5..];if(!_voice.Apps.TryGetValue(alias,out var target)||!VoiceSettings.ValidTarget(target)){AskAi(question);return;}try{_lastAction="Opening "+alias;SetVoiceStatus("JARVIS / acting");Process.Start(new ProcessStartInfo(target){UseShellExecute=true});Reply("Opening "+alias+".");}catch{Reply("I could not open "+alias+". Check the application shortcut.");}return;}
  switch(command){
   case "hello":Reply("Hello, "+Environment.UserName+". JARVIS is ready.");break;
   case "help":Reply("You can ask for the time, date, or system status. Say Jarvis open, followed by an app name. Say Jarvis open launcher, or Jarvis sort desktop, for the desktop organizer. Say Jarvis power up to replay the boot sequence, or Jarvis status report. The tray menu has the full command guide.");break;
   case "what time is it":Reply("The time is "+DateTime.Now.ToString("h:mm tt")+".");break;
   case "what is the date":Reply("Today is "+DateTime.Now.ToString("dddd, MMMM d")+".");break;
   case "system status":try{using(var json=JsonDocument.Parse(_stats?.SnapshotJson()??"{}")){var d=json.RootElement;Reply(d.TryGetProperty("cpu",out var cpu)?"Processor load is "+Math.Round(cpu.GetDouble())+" percent. Memory usage is "+d.GetProperty("mem").GetProperty("percent").GetInt32()+" percent.":"System telemetry is still starting.");}}catch{Reply("System telemetry is unavailable.");}break;
   case "pause wallpaper":if(SetManualPause(true))Reply("Wallpaper paused.");break;
   case "resume wallpaper":if(SetManualPause(false))Reply("Manual pause released. Fullscreen and monitor pause rules still apply.");break;
   case "transparency on":case "transparency off":foreach(var screen in Screen.AllScreens)Preference(screen.DeviceName).Transparent=command.EndsWith("on");if(SaveMonitors())ApplySettings();Reply("Transparency "+(command.EndsWith("on")?"enabled.":"disabled."));break;
   case "span monitors":SetSpan(true);Reply("One HUD across all monitors.");break;
   case "separate monitors":SetSpan(false);Reply("Separate monitor layouts enabled.");break;
   default:AskAi(question);break;
   case "stop listening":_voice.Enabled=false;SaveVoice();RestartVoice();Reply("Listening stopped. Re-enable voice from the tray menu.");break;
  }
 }
 private void Reply(string text){CentreEvent("RESPONSE",text);_voiceReply=text;SetVoiceStatus("JARVIS / response");if(!_voice.SpokenReplies){SetVoiceStatus(ListeningStatus);return;}text=SpokenText(text);if(_voice.SpeechEngine=="Kokoro"){SpeakNeural(text);return;}SpeakWindows(text);}
 private async void SetVoiceStatus(string status){CentreCue(status);_voiceStatus=status;Program.Log("voice status: "+status);if(_voiceStatusItem!=null)_voiceStatusItem.Text=status;try{if(_web?.CoreWebView2!=null)await _web.CoreWebView2.ExecuteScriptAsync("window.setJarvisVoice && window.setJarvisVoice("+JsonSerializer.Serialize(new{centre=CentreSnapshot(),status=_voiceStatus,reply=_voiceReply,heard=_lastHeard=="Nothing yet"?"":_lastHeard,device=_localInput?.DeviceName??"Windows default microphone",action=_lastAction,ai=_aiBusy?"THINK":!_aiSettings.Enabled?"OFF":_aiSettings.IsLocal?"LOCAL":_aiHasKey?"READY":"SETUP"})+")");}catch{}}
}
