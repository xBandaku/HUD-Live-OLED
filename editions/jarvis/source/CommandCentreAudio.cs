using NAudio.Wave;
using NAudio.CoreAudioApi;
namespace HudWall;
internal sealed partial class HudForm {
 private WasapiOut _cueOutput;
 private string _cueStatus="";
 private DateTime _lastCue;
 private void CentreCue(string status){
  if(status==_cueStatus)return;_cueStatus=status;
  var settings=CommandCentreSettings.Load(MonitorAssets);
  if(!settings.SoundEffects||settings.EffectsVolume==0||_playbackPaused||_voiceDisposed||DateTime.UtcNow-_lastCue<TimeSpan.FromSeconds(1)||_cueOutput!=null)return;
  bool heard=status.Contains("hearing"),response=status=="JARVIS / response";
  if(!heard&&!response)return;if(heard&&!_headphones)return;
  _lastCue=DateTime.UtcNow;
  try{
   var endpoint=SelectedVoiceOutput();int rate=24000,count=rate/9;byte[] samples=new byte[count*2];double volume=settings.EffectsVolume/100d*.22*(_speaking?.2:1);
   for(int i=0;i<count;i++){double t=i/(double)rate,envelope=Math.Sin(Math.PI*i/count);double frequency=heard?740:1040;short value=(short)(Math.Sin(2*Math.PI*frequency*t)*envelope*envelope*volume*short.MaxValue);samples[i*2]=(byte)value;samples[i*2+1]=(byte)(value>>8);}
   var source=new RawSourceWaveStream(new MemoryStream(samples,false),new WaveFormat(rate,16,1));var output=new WasapiOut(endpoint,AudioClientShareMode.Shared,true,50);_cueOutput=output;
   output.PlaybackStopped+=(_,_)=>VoiceUi(()=>{try{output.Dispose();source.Dispose();endpoint.Dispose();}catch{}if(ReferenceEquals(_cueOutput,output))_cueOutput=null;});output.Init(source);output.Play();
  }catch(Exception ex){_cueOutput=null;Program.Log("interface cue unavailable: "+ex.GetType().Name);}
 }
}
