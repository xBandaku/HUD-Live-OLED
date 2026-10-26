using System.Drawing;
using NAudio.CoreAudioApi;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace HudWall;
internal sealed partial class HudForm {
 private LocalSpeechInput _localInput;
 private DateTime _inputRetry;
 private string _inputState="Starting",_lastHeard="Nothing yet";
 private ToolStripMenuItem _microphoneItem,_heardItem;
 private bool InputAvailable=>_localInput?.Ready==true||_recognizer!=null;
 private void StopLocalInput(){var old=_localInput;_localInput=null;old?.Dispose();}
 private static MMDevice SelectedMicrophone(){using var devices=new MMDeviceEnumerator();try{return devices.GetDefaultAudioEndpoint(DataFlow.Capture,Role.Communications);}catch{return devices.GetDefaultAudioEndpoint(DataFlow.Capture,Role.Multimedia);}}
 private void StartLocalInput(){
  if(!_voice.Enabled||_voiceDisposed||!File.Exists(LocalSpeechInput.ModelPath))return;
  _inputRetry=DateTime.UtcNow.AddSeconds(5);
  try{
   var input=new LocalSpeechInput(SelectedMicrophone(),()=>!_voice.Enabled||(!ContinuousListening&&(_speaking||DateTime.UtcNow<_ignoreUntil)));_localInput=input;
   input.Transcript+=text=>VoiceUi(()=>{if(_localInput!=input||!input.Ready||!_voice.Enabled)return;AcceptLocalSpeech(text);});
   input.State+=state=>VoiceUi(()=>{if(_localInput!=input)return;_inputState=state;if(state=="Ready"){if(!_speaking&&!_aiBusy)SetVoiceStatus(ListeningStatus);}else if(state=="Hearing you"||state=="Transcribing")SetVoiceStatus("JARVIS / "+state.ToLowerInvariant());});
   input.Failure+=error=>VoiceUi(()=>{if(_localInput!=input)return;Program.Log("local speech error: "+error);_inputState="Retrying microphone; Windows fallback";StopLocalInput();_inputRetry=DateTime.UtcNow.AddSeconds(5);SetVoiceStatus(ListeningStatus);});
   input.Start();Program.Log("speech input: "+input.DeviceName+"; local Whisper base.en");
  }catch(Exception ex){Program.Log("speech input startup: "+ex.GetType().Name);StopLocalInput();}
 }
 private void AcceptLocalSpeech(string text){
  if(!_voice.Enabled||(!ContinuousListening&&(_speaking||DateTime.UtcNow<_ignoreUntil)))return;
  _lastHeard=text;SetVoiceStatus("JARVIS / transcribing");string clean=NormalizeWakeName(text);
  if(ContinuousListening&&(_speaking||_aiBusy))EndAiConversation();
  HandleRecognizedSpeech(clean,"Local Whisper");
 }
 internal static string NormalizeWakeName(string text)=>Regex.Replace(text.Trim(),@"^(?:(?:hey|okay|ok)[,\s]+)?(?:jarvis|jarves|jarvice)(?:[,.:!\s]+|$)","jarvis ",RegexOptions.IgnoreCase).Trim();
 private void RefreshMicrophone(){
  if(_voice?.Enabled!=true)return;
  if(_localInput==null){if(DateTime.UtcNow>=_inputRetry)StartLocalInput();}
  else try{using var selected=SelectedMicrophone();if(selected.ID!=_localInput.DeviceId){StopLocalInput();StartLocalInput();}}catch{}
  if(_microphoneItem!=null)_microphoneItem.Text=_localInput==null?"Input: Windows speech fallback":"Mic: "+_localInput.DeviceName+" | "+(_localInput.Level>.003?"signal detected":"quiet")+" | "+_inputState;
  if(_heardItem!=null)_heardItem.Text="Last heard: "+(_lastHeard.Length>90?_lastHeard[..90]+"…":_lastHeard);
 }
 private void AddMicrophoneMenu(ToolStripMenuItem root){
  _microphoneItem=new ToolStripMenuItem("Microphone starting"){Enabled=false};_heardItem=new ToolStripMenuItem("Last heard: Nothing yet"){Enabled=false};root.DropDownItems.Add(_microphoneItem);root.DropDownItems.Add(_heardItem);
  root.DropDownItems.Add("Restart microphone recognition",null,(_,_)=>RestartVoice());
  root.DropDownItems.Add("Windows sound input settings",null,(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:sound"){UseShellExecute=true}));
 }
}
