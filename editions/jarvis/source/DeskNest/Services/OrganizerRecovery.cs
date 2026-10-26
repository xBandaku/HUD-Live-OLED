using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeskNest.Models;
namespace DeskNest.Services;
internal static class OrganizerRecovery {
 internal static IEnumerable<string> LegacyFolders(){
  var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  yield return Path.Combine(profile,"AppData","Roaming","DeskNest");
  yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DeskNest");
  var packages=Path.Combine(profile,"AppData","Local","Packages");
  string[] folders;try{folders=Directory.Exists(packages)?Directory.GetDirectories(packages):Array.Empty<string>();}catch{yield break;}
  foreach(var folder in folders){string candidate=Path.Combine(folder,"LocalCache","Roaming","DeskNest");if(File.Exists(Path.Combine(candidate,"config.json")))yield return candidate;}
 }
 internal static bool ImportBest(IEnumerable<string> folders,string target,JsonSerializerOptions options){
  if(File.Exists(target)||File.Exists(target+".bak"))return false;
  AppConfig best=null;string selected=null;int bestScore=-1;
  var valid=new List<(string root,string file)>();
  foreach(string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))try{
   string file=Path.Combine(folder,"config.json");if(!File.Exists(file)||new FileInfo(file).Length>8*1024*1024)continue;
   var config=JsonSerializer.Deserialize<AppConfig>(HudWall.SettingsStorage.Read(file),options);if(config?.Frames==null)continue;
   config.Frames.RemoveAll(f=>f==null);config.Launcher??=new();config.Launcher.Tabs??=new();config.Launcher.Tabs.RemoveAll(t=>t==null);config.AutoSort??=new();config.AutoSort.Rules??=new();
   valid.Add((folder,file));int score=config.Frames.Count+config.Frames.Sum(f=>f.Items?.Count??0)*100+(config.Launcher?.Tabs.Sum(t=>t.Items?.Count??0)??0);
   if(score>bestScore){best=config;selected=folder;bestScore=score;}
  }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException){Log.Error("Could not inspect a legacy organizer layout",ex);}
  if(best==null)return false;
  Directory.CreateDirectory(Path.GetDirectoryName(target));
  foreach(var legacy in valid){string id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacy.root)))[..12];string backup=Path.Combine(Path.GetDirectoryName(target),"legacy-config-"+id+".json");if(!File.Exists(backup))File.Copy(legacy.file,backup);}
  string Rebase(string path){
   if(string.IsNullOrWhiteSpace(path)||File.Exists(path)||Directory.Exists(path))return path;
   string normalized=path.Replace('/','\\');int marker=normalized.IndexOf("\\DeskNest\\",StringComparison.OrdinalIgnoreCase);if(marker<0)return path;
   string root=Path.GetFullPath(selected).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   string candidate=Path.GetFullPath(Path.Combine(root,normalized[(marker+10)..]));
   return candidate.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&(File.Exists(candidate)||Directory.Exists(candidate))?candidate:path;
  }
  foreach(var frame in best.Frames){frame.Items=(frame.Items??new()).Select(Rebase).ToList();if(frame.FolderPath!=null)frame.FolderPath=Rebase(frame.FolderPath);
   if(!string.IsNullOrWhiteSpace(frame.Id)&&frame.Id.IndexOfAny(Path.GetInvalidFileNameChars())<0&&!frame.Id.Contains("..")){string storage=Path.Combine(selected,"Fences",frame.Id);if(Directory.Exists(storage))frame.StoragePath=Path.GetFullPath(storage);}
  }
  foreach(var tab in best.Launcher.Tabs)tab.Items=(tab.Items??new()).Select(Rebase).ToList();
  foreach(var rule in best.AutoSort.Rules)rule.TargetFolder=Rebase(rule.TargetFolder);
  HudWall.SettingsStorage.Write(target,JsonSerializer.Serialize(best,options));Log.Info("Recovered organizer layout from "+selected+" into stable profile "+target);return true;
 }
}
