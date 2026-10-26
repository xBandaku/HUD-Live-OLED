using System.Drawing;
using System.Net.Http;
using System.Speech.Recognition;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace HudWall;
internal sealed partial class HudForm {
 private AiSettings _aiSettings=AiSettings.Load(Path.Combine(AppContext.BaseDirectory,"assets"));
 private readonly AiKeyStore _aiKeyStore=new();
 private readonly ConversationClient _aiClient=new();
 private PcTools _aiTools;
 private ConversationWindow _conversationWindow;
 private readonly List<(string speaker,string message)> _conversationLog=new();
 private CancellationTokenSource _aiRequest;
 private bool _aiBusy,_aiHasKey,_conversationActive;
 private DateTime _conversationUntil;
 private Grammar _followupGrammar;
 private System.Windows.Forms.Timer _conversationTimer;
 private string ListeningStatus=>InputAvailable?(ContinuousListening?"Listening / headphones (always)":_conversationActive?"Listening / conversation":"Listening / say Jarvis"):"Voice off";
 private void AddAiMenu(ContextMenuStrip menu){
  _aiHasKey=_aiSettings.IsLocal||!string.IsNullOrWhiteSpace(_aiKeyStore.Load());
  var root=new ToolStripMenuItem("AI conversation");
  root.DropDownItems.Add("Open conversation...",null,(_,_)=>ShowConversation());
  root.DropDownItems.Add("AI provider settings...",null,(_,_)=>ConfigureAi());
  root.DropDownItems.Add("Test AI connection",null,(_,_)=>AskAi("Please say JARVIS is online in one sentence."));
  root.DropDownItems.Add("New conversation",null,(_,_)=>NewAiConversation());
  root.DropDownItems.Add("Stop response / end conversation",null,(_,_)=>EndAiConversation());
  menu.Items.Add(root);
 }
 private void ConfigureAi(){
  using var dialog=new AiSettingsDialog(_aiSettings,!string.IsNullOrWhiteSpace(_aiKeyStore.Load()));
  if(dialog.ShowDialog()!=DialogResult.OK)return;
  try{
   EndAiConversation();
   if(dialog.RemoveKey)_aiKeyStore.Remove();
   if(!string.IsNullOrWhiteSpace(dialog.NewKey))_aiKeyStore.Save(dialog.NewKey);
   _aiHasKey=_aiSettings.IsLocal||!string.IsNullOrWhiteSpace(_aiKeyStore.Load());
   _aiSettings=dialog.Settings;_aiSettings.Save(MonitorAssets);_aiHasKey=_aiSettings.IsLocal||!string.IsNullOrWhiteSpace(_aiKeyStore.Load());
   _aiClient.Clear();_aiTools=null;RestartVoice();
   _voiceReply=_aiHasKey?(_aiSettings.IsLocal?"Local Ollama selected. Say Jarvis, followed by your question.":"AI configured. Say Jarvis, followed by your question."):"Enter an API key in AI conversation > AI provider settings.";SetVoiceStatus(ListeningStatus);
  }catch(Exception ex){MessageBox.Show(ex.Message,"Could not save AI settings");}
 }
 private void InitializeConversation(){
  _conversationTimer=new System.Windows.Forms.Timer{Interval=1000};
  _conversationTimer.Tick+=(_,_)=>{RefreshVoiceOutput();RefreshMicrophone();if(_conversationActive&&!_speaking&&!_aiBusy&&DateTime.UtcNow>_conversationUntil){SetConversationActive(false);SetVoiceStatus(ListeningStatus);}};
  _conversationTimer.Start();
  FormClosed+=(_,_)=>{_aiRequest?.Cancel();_conversationTimer?.Dispose();_conversationWindow?.Close();_aiClient.Dispose();};
 }
 private void ConfigureConversationGrammar(SpeechRecognitionEngine engine,System.Globalization.CultureInfo culture){
  _followupGrammar=null;
  if(!_aiSettings.Enabled)return;
  var wake=new GrammarBuilder{Culture=culture};wake.Append("jarvis");wake.AppendDictation();
  engine.LoadGrammar(new Grammar(wake){Name="AI wake request",Weight=.6f});
  _followupGrammar=new DictationGrammar{Name="AI followups",Enabled=false};engine.LoadGrammar(_followupGrammar);
 }
 private void SetConversationActive(bool active){
  _conversationActive=active&&_aiSettings.Enabled&&_aiHasKey;
  _conversationUntil=DateTime.UtcNow.AddSeconds(90);
  UpdateListeningGrammar();
 }
 private void HandleRecognizedSpeech(string text,string grammar){
  string clean=text.Trim().TrimEnd('.','!','?');
  if(clean.StartsWith("jarvis",StringComparison.OrdinalIgnoreCase)){HandleVoiceCommand(clean);return;}
  if(ContinuousListening||(_conversationActive&&DateTime.UtcNow<_conversationUntil))HandleVoiceCommand("jarvis "+clean);
 }
 private void ShowConversation(){
  if(_conversationWindow==null||_conversationWindow.IsDisposed){
   _conversationWindow=new ConversationWindow();_conversationWindow.Icon=Icon;
   _conversationWindow.Append("JARVIS","Ask naturally here, or say Jarvis and your question. Headphones listen continuously; speakers use Jarvis and 90-second follow-ups. Say stop conversation when finished. AI provider settings are in the tray menu.");
   foreach(var entry in _conversationLog)_conversationWindow.Append(entry.speaker,entry.message);
   _conversationWindow.Send+=text=>{
    if(text.TrimStart().StartsWith("jarvis",StringComparison.OrdinalIgnoreCase))HandleVoiceCommand(text);
    else HandleVoiceCommand("jarvis "+text);
   };
   _conversationWindow.CancelRequest+=EndAiConversation;
   _conversationWindow.NewConversation+=NewAiConversation;
  }
  _conversationWindow.Show();_conversationWindow.Activate();_conversationWindow.SetBusy(_aiBusy);
 }
 private void NewAiConversation(){EndAiConversation();_aiClient.Clear();_aiTools=null;_conversationLog.Clear();_conversationWindow?.ClearHistory();AppendConversation("JARVIS","Started a new conversation; previous context cleared.");}
 private void EndAiConversation(){
  _aiRequest?.Cancel();_aiRequest=null;_aiBusy=false;
  StopNeuralSpeech();_speakingPrompt=null;try{_speaker?.SpeakAsyncCancelAll();}catch{} _speaking=false;
  SetConversationActive(false);_conversationWindow?.SetBusy(false);SetVoiceStatus(ListeningStatus);
 }
 private void AppendConversation(string speaker,string message){if(speaker=="PC tool"){CentreEvent("ACTION",message);_lastAction=message;SetVoiceStatus("JARVIS / acting");}_conversationLog.Add((speaker,message));while(_conversationLog.Count>80)_conversationLog.RemoveAt(0);_conversationWindow?.Append(speaker,message);}
 private async void AskAi(string question){
  if(_aiBusy)return;
  if(!_aiSettings.Enabled){Reply("AI conversation is disabled. Enable it in the AI conversation settings.");return;}
  string key=_aiSettings.IsLocal?"":_aiKeyStore.Load();_aiHasKey=_aiSettings.IsLocal||!string.IsNullOrWhiteSpace(key);
  if(!_aiHasKey){Reply("Add your OpenAI API key through the tray menu, AI conversation, AI provider settings. Enter it there, not in chat.");return;}
  if((!_aiSettings.IsLocal&&question.Contains(key,StringComparison.Ordinal))||Regex.IsMatch(question,@"\bsk-[A-Za-z0-9_-]{20,}")){Reply("Enter API keys in the private OpenAI settings dialog, not in conversation.");return;}
  SetConversationActive(true);
  _aiBusy=true;
  var request=new CancellationTokenSource(TimeSpan.FromSeconds(_aiSettings.IsLocal?180:90));_aiRequest=request;
  AppendConversation("You",question);_conversationWindow?.SetBusy(true);SetVoiceStatus("JARVIS / thinking");
  if(_aiTools==null)_aiTools=new PcTools(_voice.Apps);
  // Reattach activity to the active request without storing transcript content in logs.
  var tools=_aiTools;
  void Activity(string activity)=>VoiceUi(()=>{if(_aiRequest==request)AppendConversation("PC tool",activity);});
  tools.Activity+=Activity;
  try{
   var reply=await _aiClient.AskAsync(question,key,_aiSettings,tools,request.Token);
   if(_aiRequest!=request||_voiceDisposed)return;
   AppendConversation("JARVIS",reply.Text+(reply.Sources.Length>0?"\nSources:\n"+string.Join("\n",reply.Sources):""));
   _aiBusy=false;_conversationUntil=DateTime.UtcNow.AddSeconds(90);
   string spoken=Regex.Replace(reply.Text,@"https?://\S+","");spoken=Regex.Replace(spoken,@"[*#`]+","");
   Reply(spoken);
  }catch(OperationCanceledException){if(_aiRequest==request){_aiBusy=false;AppendConversation("JARVIS","Response stopped or timed out.");SetVoiceStatus(ListeningStatus);}}
  catch(Exception ex)when(ex is HttpRequestException||ex is InvalidOperationException||ex is System.Text.Json.JsonException){if(_aiRequest==request){_aiBusy=false;string message=ex is HttpRequestException?(_aiSettings.IsLocal?"Could not reach Ollama. Start Ollama or run Setup Local AI.cmd.":"Could not reach OpenAI. Check your internet connection."):ex is System.Text.Json.JsonException?"The AI provider returned an unexpected response.":ex.Message;AppendConversation("JARVIS",message);Reply(message);}}
  finally{tools.Activity-=Activity;if(_aiRequest==request){_aiBusy=false;_aiRequest=null;_conversationWindow?.SetBusy(false);}request.Dispose();}
 }
}
