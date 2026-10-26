using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HudWall;
internal sealed partial class PcTools {
 internal static object WeatherDefinition=>Function("get_weather","Get current weather and a three-day forecast for a city or postal code. Ask for a location if none is known. Returns Open-Meteo data and units.",new Dictionary<string,object>{{"location",new{type="string"}}});
 internal static object CloseDefinition=>Function("close_app","Request a normal app to close, allowing its save prompts. Only when the user explicitly asks to close that app. Never force terminates.",new Dictionary<string,object>{{"app",new{type="string"}}});
 internal static object RunningDefinition=>Function("list_running_apps","List visible running app process names and window titles.",new Dictionary<string,object>());
 private static IEnumerable<Process> WindowApps()=>Process.GetProcesses().Where(p=>{try{return p.MainWindowHandle!=IntPtr.Zero&&p.Id!=Environment.ProcessId&&p.ProcessName is not "explorer" and not "HudWall" and not "ApplicationFrameHost";}catch{return false;}});
 internal static string RunningApps(){var items=new List<object>();foreach(var process in WindowApps())using(process)try{items.Add(new{app=process.ProcessName,title=process.MainWindowTitle});}catch{}return JsonSerializer.Serialize(items);}
 internal static string CloseApp(string app,string request){
  if(!Regex.IsMatch(request,@"\b(close|quit|exit)\b",RegexOptions.IgnoreCase)||Regex.IsMatch(request,@"\b(how|don't|do not|never|without)\b",RegexOptions.IgnoreCase))return "No app was closed. Ask explicitly to close a named app.";
  string target=app.Trim().ToLowerInvariant();if(target.EndsWith(".exe"))target=target[..^4];target=target switch{"google chrome"=>"chrome","microsoft edge"=>"msedge","edge"=>"msedge","calculator"=>"CalculatorApp",_=>target};
  if(target.Length<2||target is "all" or "everything" or "windows" or "explorer" or "hudwall" or "jarvis")return "Name a specific normal app to close.";
  int sent=0;foreach(var process in WindowApps())using(process)try{if(process.ProcessName.Equals(target,StringComparison.OrdinalIgnoreCase)||process.MainWindowTitle.Equals(app,StringComparison.OrdinalIgnoreCase)){if(process.CloseMainWindow())sent++;}}catch{}
  return sent>0?$"Asked {app} to close ({sent} window(s)). Save dialogs may still need your attention. No process was force-terminated.":$"No matching visible window for {app}. Ask which apps are running to get their exact names.";
 }
 internal static async Task<string> WeatherAsync(string location,bool fahrenheit,CancellationToken ct){
  if(string.IsNullOrWhiteSpace(location))return "Which city or postal code should I use for the weather?";
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};
  string geoUrl="https://geocoding-api.open-meteo.com/v1/search?count=1&language=en&name="+Uri.EscapeDataString(location);
  using var geo=JsonDocument.Parse(await http.GetStringAsync(geoUrl,ct));
  if(!geo.RootElement.TryGetProperty("results",out var results)||results.GetArrayLength()==0)return "I could not find that location. Try a city and country or postal code.";
  var city=results[0];string lat=city.GetProperty("latitude").GetDouble().ToString(CultureInfo.InvariantCulture),lon=city.GetProperty("longitude").GetDouble().ToString(CultureInfo.InvariantCulture);
  string url=$"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&forecast_days=3&timezone=auto&temperature_unit={(fahrenheit?"fahrenheit":"celsius")}&wind_speed_unit=mph";
  using var data=JsonDocument.Parse(await http.GetStringAsync(url,ct));var current=data.RootElement.GetProperty("current");int code=current.GetProperty("weather_code").GetInt32();string condition=code switch{0=>"clear",1 or 2=>"partly cloudy",3=>"overcast",45 or 48=>"foggy",>=51 and <=57=>"drizzle",>=61 and <=67=>"rain",>=71 and <=77=>"snow",>=80 and <=82=>"rain showers",85 or 86=>"snow showers",>=95=>"thunderstorms",_=>"conditions unavailable"};
  return JsonSerializer.Serialize(new{location=city.GetProperty("name").GetString(),region=city.TryGetProperty("admin1",out var region)?region.GetString():"",country=city.TryGetProperty("country",out var country)?country.GetString():"",condition,current=current.Clone(),units=data.RootElement.GetProperty("current_units").Clone(),forecast=data.RootElement.GetProperty("daily").Clone(),source="https://open-meteo.com/",note="Current conditions are weather-model estimates. Confirm the resolved city matches the requested location."});
 }
}
