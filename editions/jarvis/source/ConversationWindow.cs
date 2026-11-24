using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace HudWall;
internal sealed class AiSettingsDialog:Form {
 internal AiSettings Settings {get;private set;}
 internal string NewKey=>_key.Text.Trim();
 internal bool RemoveKey=>_remove.Checked;
 private readonly TextBox _key=new(){UseSystemPasswordChar=true,Dock=DockStyle.Fill};
 private readonly CheckBox _remove=new(){Text="Remove saved OpenAI API key",AutoSize=true};
 internal AiSettingsDialog(AiSettings settings,bool hasKey){
  Text="JARVIS AI provider settings";Width=770;Height=610;MinimumSize=new Size(690,570);StartPosition=FormStartPosition.CenterScreen;
  var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,AutoScroll=true};panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  var enabled=new CheckBox{Text="Enable AI conversation",Checked=settings.Enabled,AutoSize=true};
  var provider=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};provider.Items.AddRange(new object[]{"Local Ollama (no API key)","OpenAI API"});provider.SelectedIndex=settings.IsLocal?0:1;
  var localModel=new TextBox{Text=settings.LocalModel,Dock=DockStyle.Fill};var cloudModel=new TextBox{Text=settings.Model,Dock=DockStyle.Fill};
  var pc=new CheckBox{Text="Allow AI PC file search, text reading and opening apps/files",Checked=settings.PcTools,AutoSize=true};
  var web=new CheckBox{Text="Allow online web search (search queries leave this PC)",Checked=settings.WebSearch,AutoSize=true};
  var info=new Label{AutoSize=true,MaximumSize=new Size(690,0)};
  void Refresh(){bool local=provider.SelectedIndex==0;localModel.Enabled=local;cloudModel.Enabled=!local;_key.Enabled=!local;_remove.Enabled=!local;info.Text=local?"Local conversations use Ollama at 127.0.0.1:11434. No API key or OpenAI balance is needed. The model uses this PC's GPU/RAM. Web search sends the query to Bing; local conversations and requested files stay with the local model.":"Messages, recent context, requested text excerpts and tool results are sent to OpenAI. API usage/web search may incur charges. A saved key is encrypted for this Windows account. Leave the key blank to keep it.";}
  provider.SelectedIndexChanged+=(_,_)=>Refresh();Refresh();
  panel.Controls.Add(enabled);panel.Controls.Add(new Label{Text="AI provider",AutoSize=true});panel.Controls.Add(provider);
  panel.Controls.Add(new Label{Text="Downloaded local Ollama model",AutoSize=true});panel.Controls.Add(localModel);
  panel.Controls.Add(new Label{Text="OpenAI model (unused in local mode)",AutoSize=true});panel.Controls.Add(cloudModel);
  panel.Controls.Add(new Label{Text=hasKey?"OpenAI key saved (unused locally); enter a replacement below":"OpenAI API key (not required for local Ollama)",AutoSize=true});panel.Controls.Add(_key);panel.Controls.Add(_remove);panel.Controls.Add(pc);panel.Controls.Add(web);panel.Controls.Add(info);
  var bar=new FlowLayoutPanel{AutoSize=true};var save=new Button{Text="Save",AutoSize=true};save.Click+=(_,_)=>{if(!Regex.IsMatch(localModel.Text.Trim(),"^[a-zA-Z0-9._:/-]{1,100}$")||!Regex.IsMatch(cloudModel.Text.Trim(),"^[a-zA-Z0-9._:-]{1,100}$")){MessageBox.Show("Enter a valid model name.");return;}if(provider.SelectedIndex==0&&localModel.Text.Contains("cloud",StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Select a downloaded local model, such as qwen3:8b.");return;}Settings=new(){Provider=provider.SelectedIndex==0?"Ollama":"OpenAI",Enabled=enabled.Checked,LocalModel=localModel.Text.Trim(),Model=cloudModel.Text.Trim(),PcTools=pc.Checked,WebSearch=web.Checked};DialogResult=DialogResult.OK;};bar.Controls.Add(save);bar.Controls.Add(new Button{Text="Cancel",DialogResult=DialogResult.Cancel,AutoSize=true});panel.Controls.Add(bar);Controls.Add(panel);
 }
}
internal sealed class ConversationWindow:Form {
 private readonly RichTextBox _transcript=new(){Dock=DockStyle.Fill,ReadOnly=true,DetectUrls=true,BackColor=Color.FromArgb(7,17,27),ForeColor=Color.FromArgb(220,244,255),Font=new Font("Segoe UI",11)};
 private readonly TextBox _input=new(){Dock=DockStyle.Fill,Multiline=true,Font=new Font("Segoe UI",11),MaxLength=12000};
 private readonly Button _send=new(){Text="Send",AutoSize=true};
 internal event Action<string> Send;
 internal event Action CancelRequest,NewConversation;
 internal ConversationWindow(){
  Text="JARVIS conversation";Width=850;Height=650;MinimumSize=new Size(620,420);StartPosition=FormStartPosition.CenterScreen;
  var bottom=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=115,ColumnCount=2,Padding=new Padding(8)};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));bottom.Controls.Add(_input,0,0);var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,AutoSize=true};buttons.Controls.Add(_send);var cancel=new Button{Text="Stop response",AutoSize=true};cancel.Click+=(_,_)=>CancelRequest?.Invoke();buttons.Controls.Add(cancel);var clear=new Button{Text="New conversation",AutoSize=true};clear.Click+=(_,_)=>NewConversation?.Invoke();buttons.Controls.Add(clear);bottom.Controls.Add(buttons,1,0);Controls.Add(_transcript);Controls.Add(bottom);
  void Submit(){string text=_input.Text.Trim();if(text.Length==0||!_send.Enabled)return;_input.Clear();Send?.Invoke(text);}
  _send.Click+=(_,_)=>Submit();_input.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter&&!e.Shift){e.SuppressKeyPress=true;Submit();}};
  _transcript.LinkClicked+=(_,e)=>{if(Uri.TryCreate(e.LinkText,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https")System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});};
 }
 internal void Append(string speaker,string message){if(IsDisposed)return;if(_transcript.TextLength>60000)_transcript.Clear();_transcript.AppendText(speaker+": "+message+Environment.NewLine+Environment.NewLine);_transcript.SelectionStart=_transcript.TextLength;_transcript.ScrollToCaret();}
 internal void ClearHistory(){if(!IsDisposed)_transcript.Clear();}
 internal void SetBusy(bool busy){if(!IsDisposed)_send.Enabled=!busy;}
}
