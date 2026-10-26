using System.Drawing;
using System.Windows.Forms;
using System.Text;
using System.Text.Json;
namespace HudWall;
internal static class SettingsStorage {
 internal static string Read(string path){
  try{return CheckedRead(path);}catch(Exception ex)when(ex is IOException||ex is JsonException){if(File.Exists(path+".bak")){Program.Log("Restoring saved setting: "+Path.GetFileName(path));return CheckedRead(path+".bak",path.EndsWith(".json",StringComparison.OrdinalIgnoreCase));}throw;}
 }
 private static string CheckedRead(string path,bool json=false){string text=File.ReadAllText(path);if(json||path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)){using var document=JsonDocument.Parse(text);}return text;}
 internal static void Write(string path,string text){
  path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path));
  bool json=path.EndsWith(".json",StringComparison.OrdinalIgnoreCase);
  if(json){using var document=JsonDocument.Parse(text);}
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   byte[] bytes=new UTF8Encoding(false).GetBytes(text);
   using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){file.Write(bytes);file.Flush(true);}
   if(File.Exists(path)){
    bool valid=true;if(json)try{CheckedRead(path);}catch(Exception ex)when(ex is IOException||ex is JsonException){valid=false;}
    if(valid)File.Replace(temporary,path,path+".bak");else File.Move(temporary,path,true);
   }else File.Move(temporary,path);
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
