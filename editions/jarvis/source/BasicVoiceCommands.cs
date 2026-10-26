using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HudWall;
internal sealed partial class HudForm {
 private bool _awaitingWeatherLocation;
 private bool HandleBasicRequest(string question){
  string clean=question.Trim().TrimEnd('.','?','!');
  var close=Regex.Match(clean,@"^(?:(?:can|could|would) you (?:please )?)?(?:close|quit|exit) (.+?)(?: please)?$",RegexOptions.IgnoreCase);
  if(close.Success){_lastAction="Close request / "+close.Groups[1].Value;Reply(_aiSettings.PcTools?PcTools.CloseApp(close.Groups[1].Value,question):"PC tools are disabled in AI settings.");return true;}
  if(Regex.IsMatch(clean,@"^(?:what|which|list|show).*(?:apps|applications|programs).*(?:running|open)$",RegexOptions.IgnoreCase)){if(!_aiSettings.PcTools){Reply("PC tools are disabled in AI settings.");return true;}using var apps=JsonDocument.Parse(PcTools.RunningApps());Reply("Visible apps: "+string.Join(", ",apps.RootElement.EnumerateArray().Select(x=>x.GetProperty("app").GetString()).Distinct()));return true;}
  bool weather=Regex.IsMatch(clean,@"\b(weather|forecast|temperature outside)\b",RegexOptions.IgnoreCase);
  if(!weather&&!_awaitingWeatherLocation)return false;
  if(!_aiSettings.WebSearch){Reply("Online weather is disabled. Enable web search in AI settings.");return true;}
  string location=_voice.WeatherLocation;
  if(_awaitingWeatherLocation&&!weather){location=clean;_awaitingWeatherLocation=false;}
  else{var match=Regex.Match(clean,@"\b(?:in|for|at)\s+(.+?)(?:\s+(?:today|tomorrow|right now))?$",RegexOptions.IgnoreCase);if(match.Success)location=match.Groups[1].Value;}
  if(string.IsNullOrWhiteSpace(location)){_awaitingWeatherLocation=true;Reply("Which city or postal code should I use for the weather?");return true;}
  FetchWeather(location);return true;
 }
 private async void FetchWeather(string location){
  _lastAction="Weather lookup / "+location;_voiceReply="Checking weather for "+location;SetVoiceStatus("JARVIS / weather");
  try{string result=await PcTools.WeatherAsync(location,TemperatureSettings.Load(MonitorAssets)=="F",CancellationToken.None);
   if(!result.StartsWith("{")){Reply(result);return;}
   using var doc=JsonDocument.Parse(result);var root=doc.RootElement;var current=root.GetProperty("current");var daily=root.GetProperty("forecast");string unit=TemperatureSettings.Load(MonitorAssets)=="F"?"Fahrenheit":"Celsius";
   _voice.WeatherLocation=location;SaveVoice();
   string answer=$"In {root.GetProperty("location").GetString()}, {root.GetProperty("region").GetString()}, it is {root.GetProperty("condition").GetString()} and {Math.Round(current.GetProperty("temperature_2m").GetDouble())} degrees {unit}, feeling like {Math.Round(current.GetProperty("apparent_temperature").GetDouble())}. Today's high is {Math.Round(daily.GetProperty("temperature_2m_max")[0].GetDouble())}, with a low of {Math.Round(daily.GetProperty("temperature_2m_min")[0].GetDouble())}. Tomorrow's high is {Math.Round(daily.GetProperty("temperature_2m_max")[1].GetDouble())}, with a low of {Math.Round(daily.GetProperty("temperature_2m_min")[1].GetDouble())}. Source: Open Meteo.";
   _centreContext=new{kind="weather",title=root.GetProperty("location").GetString(),body=answer,metrics=new[]{Math.Round(current.GetProperty("temperature_2m").GetDouble())+" degrees "+unit,root.GetProperty("condition").GetString(),"Source / Open-Meteo"}};
   AppendConversation("JARVIS",answer+"\nhttps://open-meteo.com/");Reply(answer);
  }catch(Exception ex)when(ex is System.Net.Http.HttpRequestException||ex is TaskCanceledException||ex is JsonException){Reply("The weather service could not be reached. Please try again shortly.");}
 }
}
