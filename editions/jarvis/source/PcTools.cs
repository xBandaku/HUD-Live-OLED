using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HudWall;
internal sealed partial class PcTools {
 private readonly Dictionary<string,string> _apps;
 private readonly HashSet<string> _knownPaths=new(StringComparer.OrdinalIgnoreCase);
 private readonly string[] _roots;
 internal event Action<string> Activity;
 internal PcTools(Dictionary<string,string> apps,string[] roots=null){_apps=apps;_roots=roots??DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed&&d.IsReady).Select(d=>d.RootDirectory.FullName).ToArray();}
 internal static object[] Definitions=>new object[]{CloseDefinition,RunningDefinition,
  Function("search_pc","Find files or folders by part of the name on the PC. Search common user folders first, then fixed drives. Results may be partial; refine the query if needed.",new Dictionary<string,object>{{"query",new{type="string"}}}),
  Function("list_apps","List apps available through configured aliases, Start menu shortcuts and App Paths registrations.",new Dictionary<string,object>()),
  Function("open_item","Open an app alias, a known search-result file/folder path, or an http/https website. Only when the user asked to open, launch, run or play it. Never execute commands or scripts.",new Dictionary<string,object>{{"target",new{type="string"}}}),
  Function("read_text_file","Read a requested text/code document for discussion. Its contents are provided to the selected AI model. Binary documents and credential files are not supported.",new Dictionary<string,object>{{"path",new{type="string"}}})};
 internal static object SearchWebDefinition=>Function("search_web","Search the web for current information. Returns search-result titles, snippets and source URLs, not full pages.",new Dictionary<string,object>{{"query",new{type="string"}}});
 private static object Function(string name,string description,Dictionary<string,object> properties)=>new{type="function",name,description,strict=true,parameters=new{type="object",properties,required=properties.Keys.ToArray(),additionalProperties=false}};
 internal async Task<string> ExecuteAsync(string name,string arguments,string userRequest,CancellationToken ct){
  ct.ThrowIfCancellationRequested();
  try{using var json=JsonDocument.Parse(arguments);var args=json.RootElement;
   switch(name){
    case "get_weather":return await WeatherAsync(args.GetProperty("location").GetString(),TemperatureSettings.Load(Path.Combine(AppContext.BaseDirectory,"assets"))=="F",ct);
    case "close_app":return CloseApp(args.GetProperty("app").GetString()??"",userRequest);
    case "list_running_apps":return RunningApps();
    case "search_web":{string query=args.GetProperty("query").GetString()?.Trim()??"";if(query.Length<2||query.Length>400)return "Use a web query between 2 and 400 characters.";Activity?.Invoke("Searching web: "+query);try{using var http=new System.Net.Http.HttpClient{Timeout=TimeSpan.FromSeconds(20)};http.DefaultRequestHeaders.UserAgent.ParseAdd("JARVIS-Desktop/19.0");string xml=await http.GetStringAsync("https://www.bing.com/search?format=rss&q="+Uri.EscapeDataString(query),ct);if(xml.Length>1500000)return "The search response was too large.";using var reader=System.Xml.XmlReader.Create(new StringReader(xml),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null});var feed=System.Xml.Linq.XDocument.Load(reader);var results=feed.Descendants("item").Take(6).Select(item=>new{title=(string)item.Element("title"),url=(string)item.Element("link"),snippet=Regex.Replace(System.Net.WebUtility.HtmlDecode((string)item.Element("description")??""),"<[^>]+>","")}).Where(item=>Uri.TryCreate(item.url,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https").ToArray();return JsonSerializer.Serialize(new{results,note="Search snippets only; verify details at the source links."});}catch(Exception ex)when(ex is System.Net.Http.HttpRequestException||ex is System.Xml.XmlException){return "Online search is currently unavailable. Do not invent search results.";}}
    case "list_apps":{Activity?.Invoke("Looking up installed apps");var apps=await Task.Run(FindApps,ct);return JsonSerializer.Serialize(apps.Select(p=>new{alias=p.Key,path=p.Value}).Take(200));}
    case "search_pc":{string query=args.GetProperty("query").GetString()?.Trim();if(string.IsNullOrWhiteSpace(query)||query.Length<2)return "Use at least two characters in a filename query.";Activity?.Invoke("Searching PC: "+query);return await Task.Run(()=>Search(query,ct),ct);}
    case "read_text_file":{string path=Path.GetFullPath(args.GetProperty("path").GetString()??"");if(!_knownPaths.Contains(path)&&!userRequest.Contains(path,StringComparison.OrdinalIgnoreCase))return "Search for the requested file first.";if(!Readable(path))return "This file is not a supported text document, or is a credential/configuration file.";Activity?.Invoke("Reading text: "+path);string text;using(var reader=new StreamReader(path)){var buffer=new char[16000];int length=await reader.ReadBlockAsync(buffer.AsMemory(),ct);text=new string(buffer,0,length);}return JsonSerializer.Serialize(new{path,text,possibly_truncated=new FileInfo(path).Length>16000});}
    case "open_item":{if(!MayOpen(userRequest))return "The user has not asked to open an item in this turn. Ask which item they want opened.";string target=args.GetProperty("target").GetString()?.Trim()??"";var apps=FindApps();
     if(apps.TryGetValue(target,out var app))target=app;
     else if(Uri.TryCreate(target,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https"){}
     else{target=Path.GetFullPath(target);if(!_knownPaths.Contains(target)&&!userRequest.Contains(target,StringComparison.OrdinalIgnoreCase))return "Use a configured app or search for the item first.";if(!File.Exists(target)&&!Directory.Exists(target))return "That path does not exist.";if(File.Exists(target)&&!Openable(target))return "Scripts, installers and system-changing file types must be opened manually.";}
     ct.ThrowIfCancellationRequested();Activity?.Invoke("Opening: "+target);Process.Start(new ProcessStartInfo(target){UseShellExecute=true});return JsonSerializer.Serialize(new{opened=target});}
    default:return "Unsupported tool.";
   }
  }catch(OperationCanceledException){throw;}catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException||ex is System.ComponentModel.Win32Exception||ex is JsonException||ex is KeyNotFoundException){return "The requested tool could not complete: "+ex.GetType().Name;}
 }
 private string Search(string query,CancellationToken ct){
  var terms=query.Trim('*').Split(' ',StringSplitOptions.RemoveEmptyEntries);var found=new List<string>();var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var timer=Stopwatch.StartNew();int scanned=0;bool partial=false;
  var preferred=new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)};
  var starts=(_roots.Length==0?preferred:preferred.Where(p=>_roots.Any(root=>p.StartsWith(root,StringComparison.OrdinalIgnoreCase))).Concat(_roots)).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
  {var stack=new Queue<string>(starts);while(stack.Count>0){ct.ThrowIfCancellationRequested();if(timer.ElapsedMilliseconds>10000||found.Count>=40||scanned>=250000){partial=true;break;}string dir=stack.Dequeue();if(!visited.Add(dir))continue;
   try{foreach(string path in Directory.EnumerateFileSystemEntries(dir)){ct.ThrowIfCancellationRequested();scanned++;if(timer.ElapsedMilliseconds>10000||found.Count>=40||scanned>=250000){partial=true;break;}
     var attr=File.GetAttributes(path);if((attr&FileAttributes.Directory)!=0&&(attr&FileAttributes.ReparsePoint)!=0)continue;
     if(terms.All(term=>Path.GetFileName(path).Contains(term,StringComparison.OrdinalIgnoreCase))&&!Sensitive(path)){found.Add(path);_knownPaths.Add(Path.GetFullPath(path));}
     if((attr&FileAttributes.Directory)!=0)stack.Enqueue(path);
   }}catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException){}
   if(partial)break;
  }}
  return JsonSerializer.Serialize(new{matches=found,partial,scanned,note=partial?"Search limited to 10 seconds, 250000 entries or 40 results. Refine the filename to continue.":"Search covers accessible fixed drives; protected and reparse-point locations are skipped."});
 }
 private Dictionary<string,string> FindApps(){
  var result=new Dictionary<string,string>(_apps,StringComparer.OrdinalIgnoreCase);
  foreach(var folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)})try{if(!Directory.Exists(folder))continue;foreach(var file in Directory.EnumerateFiles(folder,"*.lnk",SearchOption.AllDirectories)){string alias=Path.GetFileNameWithoutExtension(file).ToLowerInvariant();if(!alias.Contains("uninstall"))result.TryAdd(alias,file);}}catch{}
  foreach(var hive in new[]{Microsoft.Win32.Registry.CurrentUser,Microsoft.Win32.Registry.LocalMachine})try{using var root=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths");if(root==null)continue;foreach(string name in root.GetSubKeyNames()){using var key=root.OpenSubKey(name);string file=(key?.GetValue("") as string)?.Trim('"');if(file!=null&&File.Exists(file)&&Path.GetExtension(file).Equals(".exe",StringComparison.OrdinalIgnoreCase))result.TryAdd(Path.GetFileNameWithoutExtension(name).ToLowerInvariant(),file);}}catch{}
  return result;
 }
 private static bool MayOpen(string request)=>Regex.IsMatch(request,@"\b(open|launch|start|run|play|bring up)\b",RegexOptions.IgnoreCase)&&!Regex.IsMatch(request,@"\b(how (can|do|would|should)|how to|explain|tutorial)\b|\b(do not|don't|never|without)\s+(open|launch|start|run|play)",RegexOptions.IgnoreCase);
 private static bool Sensitive(string path){string name=Path.GetFileName(path).ToLowerInvariant();return name=="openai-key.dat"||name=="auth.json"||name.Contains("token")||name.StartsWith(".env")||name.Contains("credential")||name.Contains("password")||name.Contains("secret")||name.StartsWith("id_rsa")||name.StartsWith("id_ed25519")||new[]{".pem",".key",".pfx",".p12"}.Contains(Path.GetExtension(name));}
 private static bool Readable(string path)=>!Sensitive(path)&&File.Exists(path)&&new[]{".txt",".md",".csv",".log",".json",".xml",".html",".css",".cs",".py",".ts",".tsx",".js",".yaml",".yml",".ini"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
 private static bool Openable(string path)=>!Sensitive(path)&&!new[]{".bat",".cmd",".ps1",".vbs",".js",".jse",".wsf",".hta",".reg",".msi",".msp",".scr",".com",".url"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
}
