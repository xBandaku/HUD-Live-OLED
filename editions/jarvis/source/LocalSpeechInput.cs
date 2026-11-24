using System.Drawing;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Threading.Channels;
using Whisper.net;
namespace HudWall;

// All capture buffers and utterances stay in memory; nothing is recorded to disk.
internal sealed class SpeechSegmenter {
 private readonly Queue<float[]> _lead=new();
 private readonly List<float> _phrase=new();
 private double _noise=.0005,_quiet,_voiced;
 internal bool Hearing {get;private set;}
 internal float Level {get;private set;}
 internal void Reset(){_lead.Clear();_phrase.Clear();_quiet=0;_voiced=0;Hearing=false;}
 internal float[] Push(float[] samples){
  if(samples.Length==0)return null;
  double rms=Math.Sqrt(samples.Sum(x=>(double)x*x)/samples.Length);Level=(float)rms;
  double seconds=samples.Length/16000d;bool speech=rms>Math.Max(.003,_noise*3);
  if(!Hearing){
   _lead.Enqueue(samples);while(_lead.Sum(x=>x.Length)>6400&&_lead.Count>1)_lead.Dequeue();
   if(!speech){_noise=.98*_noise+.02*Math.Min(rms,.003);return null;}
   Hearing=true;foreach(var chunk in _lead)_phrase.AddRange(chunk);_lead.Clear();_voiced=seconds;_quiet=0;return null;
  }
  _phrase.AddRange(samples);if(speech){_voiced+=seconds;_quiet=0;}else _quiet+=seconds;
  if(_quiet<.85&&_phrase.Count<16000*22)return null;
  float[] result=_voiced>=.16?_phrase.ToArray():null;Reset();return result;
 }
}

internal sealed class LocalSpeechInput:IDisposable {
 internal static string ModelPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HudWall","Models","ggml-base.en.bin");
 private readonly CancellationTokenSource _stop=new();
 private readonly Channel<(float[] audio,DateTime at)> _queue=Channel.CreateBounded<(float[],DateTime)>(new BoundedChannelOptions(2){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true,SingleWriter=true});
 private readonly SpeechSegmenter _segments=new();
 private readonly Func<bool> _suppress;
 private MMDevice _device;
 private WasapiCapture _capture;
 internal volatile bool Ready;
 internal string DeviceId {get;}
 internal string DeviceName {get;}
 internal float Level=>_segments.Level;
 internal event Action<string> Transcript,State,Failure;
 internal LocalSpeechInput(MMDevice device,Func<bool> suppress){DeviceId=device.ID;DeviceName=device.FriendlyName;device.Dispose();_suppress=suppress;}
 internal void Start()=>_ = Task.Run(RunAsync);
 private async Task RunAsync(){
  try{
   State?.Invoke("Loading local speech model");
   using var factory=WhisperFactory.FromPath(ModelPath);
   using var processor=factory.CreateBuilder().WithLanguage("en").WithThreads(4).WithNoContext().WithTemperature(0).WithNoSpeechThreshold(.6f).Build();
   _stop.Token.ThrowIfCancellationRequested();
   using(var devices=new MMDeviceEnumerator())_device=devices.GetDevice(DeviceId);
   _capture=new WasapiCapture(_device);_capture.DataAvailable+=OnAudio;
   _capture.RecordingStopped+=(_,e)=>{if(!_stop.IsCancellationRequested){Ready=false;Failure?.Invoke(e.Exception?.Message??"Microphone stopped");_stop.Cancel();}};
   _capture.StartRecording();Ready=true;State?.Invoke("Ready");
   await foreach(var phrase in _queue.Reader.ReadAllAsync(_stop.Token)){
    if(DateTime.UtcNow-phrase.at>TimeSpan.FromSeconds(10))continue;
    State?.Invoke("Transcribing");var words=new List<string>();
    await foreach(var segment in processor.ProcessAsync(phrase.audio,_stop.Token))if(segment.NoSpeechProbability<.7f&&!string.IsNullOrWhiteSpace(segment.Text))words.Add(segment.Text.Trim());
    string text=string.Join(" ",words).Trim();
    // Common silence/music hallucinations must not become desktop commands.
    if(text.Length>1&&!System.Text.RegularExpressions.Regex.IsMatch(text,@"^(?:\[.*\]|\(.*\)|thank you(?: for watching)?[.!]?|thanks for watching[.!]?|you[.!]?)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))Transcript?.Invoke(text);
    State?.Invoke("Ready");
   }
  }catch(OperationCanceledException){}
  catch(Exception ex){Ready=false;Failure?.Invoke(ex.GetType().Name+": "+ex.Message);}
  finally{Ready=false;try{_capture?.StopRecording();_capture?.Dispose();_device?.Dispose();}catch{}}
 }
 private void OnAudio(object sender,WaveInEventArgs e){
  if(_stop.IsCancellationRequested)return;
  if(_suppress()){_segments.Reset();return;}
  var format=_capture.WaveFormat;var samples=ToMono16k(e.Buffer,e.BytesRecorded,format);
  bool before=_segments.Hearing;var phrase=_segments.Push(samples);
  if(!before&&_segments.Hearing)State?.Invoke("Hearing you");
  if(phrase!=null)_queue.Writer.TryWrite((phrase,DateTime.UtcNow));
 }
 internal static float[] ToMono16k(byte[] bytes,int length,WaveFormat format){
  int width=format.BitsPerSample/8,frames=length/format.BlockAlign;
  if(frames==0)return Array.Empty<float>();
  bool floating=format.Encoding==WaveFormatEncoding.IeeeFloat||(format is WaveFormatExtensible ext&&ext.SubFormat==new Guid("00000003-0000-0010-8000-00aa00389b71"));
  var mono=new float[frames];for(int frame=0;frame<frames;frame++){
   float sum=0;for(int channel=0;channel<format.Channels;channel++){int i=frame*format.BlockAlign+channel*width;sum+=floating?BitConverter.ToSingle(bytes,i):width switch{2=>BitConverter.ToInt16(bytes,i)/32768f,4=>BitConverter.ToInt32(bytes,i)/2147483648f,3=>((bytes[i]|bytes[i+1]<<8|bytes[i+2]<<16)<<8)/2147483648f,1=>(bytes[i]-128)/128f,_=>0};}mono[frame]=sum/format.Channels;
  }
  int count=(int)Math.Round(frames*16000d/format.SampleRate);var result=new float[count];for(int i=0;i<count;i++){double position=i*format.SampleRate/16000d;int index=Math.Min((int)position,frames-1);result[i]=mono[index]+(mono[Math.Min(index+1,frames-1)]-mono[index])*(float)(position-index);}return result;
 }
 public void Dispose(){Ready=false;_stop.Cancel();_queue.Writer.TryComplete();try{_capture?.StopRecording();}catch{}}
}
