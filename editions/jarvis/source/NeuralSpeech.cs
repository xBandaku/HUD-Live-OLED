using System.Drawing;
using System.Windows.Forms;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using Microsoft.ML.OnnxRuntime;
using NAudio.Wave;
namespace HudWall;
internal sealed partial class HudForm {
 private readonly SemaphoreSlim _speechGate=new(1,1);
 private Task<KokoroWavSynthesizer> _neuralLoad;
 private KokoroWavSynthesizer _neuralSynth;
 private WasapiOut _neuralOutput;
 private NAudio.CoreAudioApi.MMDevice _speechEndpoint;
 private RawSourceWaveStream _neuralStream;
 private CancellationTokenSource _neuralRequest;
 private int _neuralGeneration;
 internal static string NeuralModelPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HudWall","Models","kokoro.onnx");
 private Task<KokoroWavSynthesizer> EnsureNeuralLoad(){
  if(_neuralLoad==null||_neuralLoad.IsFaulted)_neuralLoad=Task.Run(()=>{using var options=new SessionOptions{IntraOpNumThreads=4,InterOpNumThreads=1};return new KokoroWavSynthesizer(NeuralModelPath,options);});
  return _neuralLoad;
 }
 /// <summary>Loads the voice model ahead of the startup greeting, so JARVIS answers on cue.</summary>
 private void WarmNeuralVoice(){if(_voice?.SpokenReplies==true&&File.Exists(NeuralModelPath))_=EnsureNeuralLoad();}
 private void StopNeuralSpeech(){
  if(_neuralOutput!=null)PostVoice("{\"stop\":true}");
  _speaking=false;_ignoreUntil=DateTime.UtcNow.AddMilliseconds(500);
  _neuralGeneration++;_neuralRequest?.Cancel();_neuralRequest=null;
  try{_neuralOutput?.Stop();_neuralOutput?.Dispose();_neuralStream?.Dispose();}catch{}
  _neuralOutput=null;_neuralStream=null;_speechEndpoint?.Dispose();_speechEndpoint=null;
 }
 private async void SpeakNeural(string text){
  StopNeuralSpeech();int generation=_neuralGeneration;
  if(!File.Exists(NeuralModelPath)){SpeakWindows(text);return;}
  var request=new CancellationTokenSource();_neuralRequest=request;
  _speakingPrompt=null;try{_speaker?.SpeakAsyncCancelAll();}catch{} _speaking=true;
  try{
   var synth=await EnsureNeuralLoad().WaitAsync(request.Token);_neuralSynth=synth;
   if(_voiceDisposed){synth.Dispose();return;}
   if(generation!=_neuralGeneration)return;
   string voice=_voice.NeuralVoice;float speed=(float)_voice.SpeechSpeed;
   var channel=System.Threading.Channels.Channel.CreateBounded<byte[]>(2);var token=request.Token;
   // Synthesize the next sentence while the first is playing; keep one synthesizer inference at a time.
   var producer=Task.Run(async()=>{
    try{foreach(string sentence in SpeechChunks(text)){
     await _speechGate.WaitAsync(token);byte[] samples;
     try{token.ThrowIfCancellationRequested();samples=await synth.SynthesizeAsync(sentence,KokoroVoiceManager.GetVoice(voice),new KokoroTTSPipelineConfig{Speed=speed});}finally{_speechGate.Release();}
     await channel.Writer.WriteAsync(samples,token);
    }channel.Writer.TryComplete();}catch(Exception error){channel.Writer.TryComplete(error);}
   });
   await foreach(byte[] samples in channel.Reader.ReadAllAsync(token)){
    if(_voiceDisposed||generation!=_neuralGeneration)return;
    var source=new RawSourceWaveStream(new MemoryStream(samples,false),new WaveFormat(24000,16,1));_speechEndpoint=SelectedVoiceOutput();var endpoint=_speechEndpoint;
    var output=new WasapiOut(endpoint,NAudio.CoreAudioApi.AudioClientShareMode.Shared,true,100);_neuralStream=source;_neuralOutput=output;
    var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    output.PlaybackStopped+=(_,e)=>{if(e.Exception!=null)finished.TrySetException(e.Exception);else finished.TrySetResult();};
    output.Init(source);PostVoice(VoiceEnvelopeJson(samples,24000));output.Play();
    await finished.Task.WaitAsync(token);
    if(generation!=_neuralGeneration)return;
    output.Dispose();source.Dispose();endpoint.Dispose();_neuralOutput=null;_neuralStream=null;_speechEndpoint=null;
   }
   await producer;
   if(generation==_neuralGeneration){_speaking=false;_ignoreUntil=DateTime.UtcNow.AddMilliseconds(250);_conversationUntil=DateTime.UtcNow.AddSeconds(90);SetVoiceStatus(ListeningStatus);}
  }catch(OperationCanceledException){}
  catch(Exception ex){if(generation==_neuralGeneration&&!_voiceDisposed){Program.Log("neural speech fallback: "+ex.GetType().Name);SpeakWindows(text);}}
  finally{if(_neuralRequest==request)_neuralRequest=null;request.Dispose();}
 }
 internal static string[] SpeechChunks(string text){
  var result=new List<string>();
  foreach(string sentence in System.Text.RegularExpressions.Regex.Split(text,@"(?<=[.!?])\s+(?=[A-Z0-9])")){
   string remaining=sentence.Trim();while(remaining.Length>280){int split=remaining.LastIndexOf(' ',240);if(split<80)split=240;result.Add(remaining[..split]);remaining=remaining[split..].Trim();}if(remaining.Length>0)result.Add(remaining);
  }return result.ToArray();
 }
 private void SpeakWindows(string text){
  StopNeuralSpeech();if(_headphones){_headphones=false;UpdateListeningGrammar();Program.Log("Windows speech fallback: continuous listening suspended to prevent speaker echo");}if(_speaker==null){_speaking=false;SetVoiceStatus(ListeningStatus);return;}
  try{_speakingPrompt=null;_speaker.SpeakAsyncCancelAll();_speaking=true;_ignoreUntil=DateTime.UtcNow.AddSeconds(1);_speaker.Rate=(int)Math.Round((_voice.SpeechSpeed-1)*10);_speakingPrompt=_speaker.SpeakAsync(text);PostVoice("{\"synthetic\":true}");}catch(Exception ex){_speaking=false;SetVoiceStatus("Speech output unavailable: "+ex.Message);}
 }
}
