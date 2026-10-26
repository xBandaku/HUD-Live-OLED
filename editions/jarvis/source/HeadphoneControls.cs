using System.Drawing;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
namespace HudWall;
internal sealed partial class HudForm {
 private string _outputId="",_outputName="Detecting output";
 private bool _headphones;
 private bool ContinuousListening=>_headphones&&_voice?.Enabled==true&&_aiSettings.Enabled&&_aiHasKey;
 internal static bool IsHeadphoneForm(int form)=>form is 3 or 5;
 private MMDevice SelectedVoiceOutput(){
  using var devices=new MMDeviceEnumerator();
  try{var communications=devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Communications);if(IsHeadphoneForm(Convert.ToInt32(communications.Properties[PropertyKeys.PKEY_AudioEndpoint_FormFactor].Value)))return communications;communications.Dispose();}catch{}
  return devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
 }
 private void RefreshVoiceOutput(){
  try{using var output=SelectedVoiceOutput();bool headphones=IsHeadphoneForm(Convert.ToInt32(output.Properties[PropertyKeys.PKEY_AudioEndpoint_FormFactor].Value));if(output.ID==_outputId)return;
   bool changed=_outputId.Length>0;_outputId=output.ID;_outputName=output.FriendlyName;_headphones=headphones;
   if(changed)EndAiConversation();
   UpdateListeningGrammar();SetVoiceStatus(ListeningStatus);Program.Log("voice output: "+_outputName+"; continuous="+ContinuousListening);
  }catch{if(_headphones){_headphones=false;EndAiConversation();} _outputId="";}
 }
 private void UpdateListeningGrammar(){try{if(_followupGrammar!=null)_followupGrammar.Enabled=ContinuousListening||_conversationActive;}catch(InvalidOperationException){}}
 internal static string SpokenText(string text){
  text=System.Text.RegularExpressions.Regex.Replace(text,@"\bJ\s*\.\s*A\s*\.\s*R\s*\.\s*V\s*\.\s*I\s*\.\s*S(?:\s*\.)?","Jarvis",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  text=System.Text.RegularExpressions.Regex.Replace(text,@"\bS\s*\.\s*H\s*\.\s*I\s*\.\s*E\s*\.\s*L\s*\.\s*D(?:\s*\.)?","Shield",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  return System.Text.RegularExpressions.Regex.Replace(text,@"\bVEYDR\b","Vader",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 }
}
