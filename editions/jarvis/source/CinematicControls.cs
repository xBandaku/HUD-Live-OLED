using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace HudWall;

/// <summary>
/// Cinematic JARVIS. The page plays the boot sequence (cinematic.js) and reports "start" and "online"; at "online" the
/// host greets the user aloud with the time, weather and system status. Every spoken reply's loudness is sent to the
/// page so the reactor pulses with JARVIS's actual voice.
/// </summary>
internal sealed partial class HudForm
{
    internal static readonly string[] CinematicVoiceCommands={"power up","boot sequence","wake up","good morning","good afternoon","good evening","i'm home","status report"};

    /// <summary>Greet when the next boot reaches "online": the first boot per run (if enabled), or a replay asked for by voice.</summary>
    private bool? _greetOnBoot;
    private Task<string> _greetingWeather;

    private void OnPageMessage(object sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(!e.Source.StartsWith("https://hud.local/",StringComparison.OrdinalIgnoreCase))return;
        string phase;
        try
        {
            using var doc=JsonDocument.Parse(e.WebMessageAsJson);var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("hud",out var hud)||hud.GetString()!="boot"||!root.TryGetProperty("phase",out var value))return;
            phase=value.GetString();
        }
        catch(Exception ex)when(ex is JsonException||ex is InvalidOperationException){return;}
        _greetOnBoot??=_voice?.Greeting!=false;
        if(_greetOnBoot!=true)return;
        if(phase=="start"){_greetingWeather=GreetingWeatherAsync();WarmNeuralVoice();}  // fetch and load while the reactor spins up
        else if(phase=="online"){_greetOnBoot=false;Greet(_greetingWeather??GreetingWeatherAsync(),automatic:true);_greetingWeather=null;}
    }

    /// <summary>Plays the boot sequence again and greets at the end ("Jarvis, power up").</summary>
    private async void ReplayBoot()
    {
        _greetOnBoot=true;
        _lastAction="Boot sequence";
        try{if(_web?.CoreWebView2!=null)await _web.CoreWebView2.ExecuteScriptAsync("window.hudBoot && window.hudBoot(true)");}
        catch(Exception ex){Program.Log("boot replay: "+ex.Message);}
    }

    private async void Greet(Task<string> weather,bool automatic,bool salutation=true)
    {
        // A fullscreen game or a paused HUD at startup means the user is busy: stay quiet.
        if(automatic&&_playbackPaused){Program.Log("startup greeting skipped: HUD paused");return;}
        string text;
        try{text=await ComposeGreetingAsync(weather,salutation);}
        catch(Exception ex){Program.Log("greeting: "+ex.Message);return;}
        _lastAction=salutation?"Greeting":"Status report";
        Reply(text);
    }

    private async Task<string> ComposeGreetingAsync(Task<string> weather,bool salutation)
    {
        var now=DateTime.Now;
        var parts=new List<string>();
        if(salutation)
        {
            string greeting=now.Hour<12?"Good morning":now.Hour<17?"Good afternoon":"Good evening";
            parts.Add(PrimaryIsShield()
                ?(now.Hour<5?"Late shift, Agent "+Environment.UserName+"?":greeting+", Agent "+Environment.UserName+". Welcome back.")+" S.H.I.E.L.D. operations are online."
                :(now.Hour<5?"Burning the midnight oil, "+Environment.UserName+"?":greeting+", "+Environment.UserName+".")+" J.A.R.V.I.S. is online.");
        }
        string time=now.ToString(ClockSettings.Load(MonitorAssets)=="24"?"HH:mm":"h:mm tt",CultureInfo.InvariantCulture);
        parts.Add("It's "+time+" on "+now.ToString("dddd, MMMM d",CultureInfo.InvariantCulture)+".");
        if(weather!=null){string outside=await weather;if(!string.IsNullOrEmpty(outside))parts.Add(outside);}
        parts.Add(SystemsSummary());
        return string.Join(" ",parts);
    }

    /// <summary>The primary display's interface (its own choice, or the global one) decides who greets: JARVIS or SHIELD.</summary>
    private bool PrimaryIsShield()
    {
        string id=Screen.PrimaryScreen?.DeviceName;
        string chosen=id!=null&&_monitorPreferences.TryGetValue(id,out var preference)?preference.Interface:"Global";
        return (chosen=="Global"?AppearanceSettings.Load(MonitorAssets).Interface:chosen)=="SHIELD";
    }

    private static readonly Dictionary<string,string> WeatherPhrases=new(){
        ["clear"]="under clear skies",["partly cloudy"]="and partly cloudy",["overcast"]="and overcast",["foggy"]="and foggy",
        ["drizzle"]="with light drizzle",["rain"]="and raining",["snow"]="and snowing",["rain showers"]="with passing showers",
        ["snow showers"]="with snow showers",["thunderstorms"]="with thunderstorms in the area"};

    /// <summary>"Outside in Leeds, it's 12 degrees and overcast, with a high of 15." Null when weather is off or unreachable.</summary>
    private async Task<string> GreetingWeatherAsync()
    {
        string location=_voice?.WeatherLocation;
        if(!_aiSettings.WebSearch||string.IsNullOrWhiteSpace(location))return null;
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));
            string result=await PcTools.WeatherAsync(location,TemperatureSettings.Load(MonitorAssets)=="F",timeout.Token);
            if(!result.StartsWith("{"))return null;
            using var doc=JsonDocument.Parse(result);var root=doc.RootElement;
            string condition=root.GetProperty("condition").GetString();
            string phrase=WeatherPhrases.TryGetValue(condition,out var p)?p:"";
            double temperature=Math.Round(root.GetProperty("current").GetProperty("temperature_2m").GetDouble());
            double high=Math.Round(root.GetProperty("forecast").GetProperty("temperature_2m_max")[0].GetDouble());
            return $"Outside in {root.GetProperty("location").GetString()}, it's {temperature} degrees {phrase}, with a high of {high}.".Replace("  "," ").Replace(" ,",",");
        }
        catch(Exception ex)when(ex is System.Net.Http.HttpRequestException||ex is OperationCanceledException||ex is JsonException||ex is KeyNotFoundException||ex is InvalidOperationException||ex is IndexOutOfRangeException)
        {
            Program.Log("greeting weather: "+ex.GetType().Name);
            return null;
        }
    }

    /// <summary>"All systems nominal: processor at 9 percent, memory at 41 percent." or what needs attention.</summary>
    private string SystemsSummary()
    {
        try
        {
            using var json=JsonDocument.Parse(_stats?.SnapshotJson()??"{}");var d=json.RootElement;
            if(!d.TryGetProperty("cpu",out var cpu)||cpu.ValueKind!=JsonValueKind.Number)return "Telemetry is still coming online.";
            int mem=d.TryGetProperty("mem",out var m)&&m.TryGetProperty("percent",out var mp)&&mp.ValueKind==JsonValueKind.Number?(int)Math.Round(mp.GetDouble()):-1;
            var issues=new List<string>();
            if(mem>=90)issues.Add("memory is at "+mem+" percent");
            if(d.TryGetProperty("disks",out var disks)&&disks.ValueKind==JsonValueKind.Array)
                foreach(var disk in disks.EnumerateArray())
                    if(disk.TryGetProperty("percent",out var used)&&used.GetDouble()>=90)
                        issues.Add("drive "+disk.GetProperty("mount").GetString().TrimEnd(':')+" is "+Math.Round(used.GetDouble())+" percent full");
            if(d.TryGetProperty("gpu",out var gpu)&&gpu.ValueKind==JsonValueKind.Object&&gpu.TryGetProperty("temp_c",out var heat)&&heat.ValueKind==JsonValueKind.Number&&heat.GetDouble()>=85)
            {
                bool f=TemperatureSettings.Load(MonitorAssets)=="F";
                issues.Add("the graphics core is running hot at "+Math.Round(f?heat.GetDouble()*9/5+32:heat.GetDouble())+" degrees");
            }
            if(d.TryGetProperty("power",out var power)&&power.ValueKind==JsonValueKind.Object&&power.TryGetProperty("battery_percent",out var battery)&&battery.ValueKind==JsonValueKind.Number
               &&battery.GetInt32()<=20&&power.TryGetProperty("ac",out var ac)&&ac.GetString()=="Offline")
                issues.Add("battery is down to "+battery.GetInt32()+" percent");
            string load="processor at "+Math.Round(cpu.GetDouble())+" percent"+(mem>=0?", memory at "+mem+" percent":"");
            if(issues.Count==0)return "All systems nominal: "+load+".";
            string attention=issues.Count==1?issues[0]:string.Join(", ",issues.Take(issues.Count-1))+", and "+issues[^1];
            return char.ToUpperInvariant(load[0])+load[1..]+". However, "+attention+".";
        }
        catch(Exception ex)when(ex is JsonException||ex is KeyNotFoundException||ex is InvalidOperationException||ex is FormatException)
        {
            return "All systems online.";
        }
    }

    private bool HandleCinematicCommand(string command)
    {
        string c=Regex.Replace(command,@"^(?:(?:please|can you|could you|would you)\s+)+","").Replace("i am home","i'm home").Trim();
        if(Regex.IsMatch(c,@"^(?:power up|boot sequence|run (?:the )?boot sequence|wake up(?:,? daddy'?s home)?)$")){ReplayBoot();return true;}
        if(Regex.IsMatch(c,@"^(?:good (?:morning|afternoon|evening)|i'm home|welcome home)$")){Greet(GreetingWeatherAsync(),automatic:false);return true;}
        if(c=="status report"){Greet(GreetingWeatherAsync(),automatic:false,salutation:false);return true;}
        return false;
    }

    // ---------------------------------------------------------------- voice pulse

    private void PostVoice(string data)
    {
        try{_web?.CoreWebView2?.PostWebMessageAsJson("{\"hud\":\"voice\",\"data\":"+data+"}");}
        catch(Exception ex)when(ex is InvalidOperationException||ex is System.Runtime.InteropServices.COMException){Program.Log("voice pulse: "+ex.Message);}
    }

    /// <summary>
    /// Loudness of 16-bit mono speech in 20 ms steps, scaled to the clip's loudest passage, as the page's voice
    /// envelope. Latency covers the 100 ms WASAPI buffer, so the reactor pulses with what is heard.
    /// </summary>
    internal static string VoiceEnvelopeJson(byte[] pcm,int sampleRate)
    {
        int window=Math.Max(1,sampleRate/50),count=pcm.Length/2;
        var levels=new List<double>(count/window+1);
        for(int start=0;start<count;start+=window)
        {
            int end=Math.Min(count,start+window);double sum=0;
            for(int i=start;i<end;i++){double s=BitConverter.ToInt16(pcm,i*2)/32768.0;sum+=s*s;}
            levels.Add(Math.Sqrt(sum/(end-start)));
        }
        double peak=Math.Max(.02,levels.Count==0?0:levels.Max());
        var json=new StringBuilder("{\"step\":20,\"latency\":120,\"env\":[");
        for(int i=0;i<levels.Count;i++)
        {
            if(i>0)json.Append(',');
            json.Append(Math.Min(1,Math.Pow(levels[i]/peak,.7)).ToString("0.##",CultureInfo.InvariantCulture));
        }
        return json.Append("]}").ToString();
    }
}
