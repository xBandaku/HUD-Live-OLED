using System.Net.Http;
using System.Security.Cryptography;
namespace HudWall;
internal sealed partial class HudForm {
 private bool _modelsBusy;
 private readonly CancellationTokenSource _modelSetupStop=new();
 private async void EnsureSpeechModels(){
  if(_modelsBusy||_voiceDisposed)return;_modelsBusy=true;bool installed=false;
  try{
   using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(15)};
   foreach(var model in new[]{(path:NeuralModelPath,url:"https://github.com/Lyrcaxis/KokoroSharpBinaries/releases/download/v2.0.0/kokoro.onnx",hash:"0CFD5E79AAB70A3D8C1A57DC639835110DDB32C9F5FF4FDD1F4DB202EA43BB05",name:"Neural voice"),(path:LocalSpeechInput.ModelPath,url:"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin",hash:"A03779C86DF3323075F5E796CB2CE5029F00EC8869EEE3FDFB897AFE36C6D002",name:"Whisper recognition")}){
    if(File.Exists(model.path))continue;
    Directory.CreateDirectory(Path.GetDirectoryName(model.path));string temporary=model.path+".setup-"+Guid.NewGuid().ToString("N")+".download";
    _modelSetup="Downloading "+model.name;SetVoiceStatus(_voiceStatus);
    using(var response=await http.GetAsync(model.url,HttpCompletionOption.ResponseHeadersRead,_modelSetupStop.Token)){
     response.EnsureSuccessStatusCode();long total=response.Content.Headers.ContentLength??0,received=0;var reported=DateTime.UtcNow;
     await using(var input=await response.Content.ReadAsStreamAsync(_modelSetupStop.Token))await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)){
      byte[] buffer=new byte[81920];int size;while((size=await input.ReadAsync(buffer,_modelSetupStop.Token))>0){await output.WriteAsync(buffer.AsMemory(0,size),_modelSetupStop.Token);received+=size;if(DateTime.UtcNow-reported>TimeSpan.FromSeconds(1)){reported=DateTime.UtcNow;_modelSetup=$"Downloading {model.name}: {received/1048576} MB"+(total>0?$" / {total/1048576} MB":"");SetVoiceStatus(_voiceStatus);}}
     }
    }
    using(var file=File.OpenRead(temporary)){string hash=Convert.ToHexString(await SHA256.HashDataAsync(file,_modelSetupStop.Token));if(hash!=model.hash)throw new InvalidOperationException(model.name+" integrity check failed. The downloaded file was not activated.");}
    File.Move(temporary,model.path,true);installed=true;
   }
   _modelSetup="Local speech models available";if(installed&&_voice?.Enabled==true)RestartVoice();WarmNeuralVoice();
  }catch(OperationCanceledException){_modelSetup="Model setup stopped";}
  catch(Exception ex){_modelSetup="Model setup unavailable; use Check / install missing speech models to retry";Program.Log("model setup: "+ex.GetType().Name);}
  finally{_modelsBusy=false;if(!_voiceDisposed)SetVoiceStatus(_voiceStatus);}
 }
}
