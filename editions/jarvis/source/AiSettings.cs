using System.Drawing;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HudWall;
internal sealed class AiSettings {
 public bool Enabled {get;set;}=true;
 public string Provider {get;set;}="Ollama";
 public string LocalModel {get;set;}="qwen3:8b";
 internal bool IsLocal=>Provider=="Ollama";
 public string Model {get;set;}="gpt-5.4-mini";
 public bool PcTools {get;set;}=true;
 public bool WebSearch {get;set;}=true;
 internal static AiSettings Load(string assets){try{var s=JsonSerializer.Deserialize<AiSettings>(SettingsStorage.Read(Path.Combine(assets,"ai.json")))??new();if(!Regex.IsMatch(s.Model??"","^[a-zA-Z0-9._:-]{1,100}$"))s.Model="gpt-5.4-mini";if(s.Provider!="Ollama"&&s.Provider!="OpenAI")s.Provider="Ollama";if(!Regex.IsMatch(s.LocalModel??"","^[a-zA-Z0-9._:/-]{1,100}$"))s.LocalModel="qwen3:8b";return s;}catch(Exception ex)when(ex is IOException||ex is JsonException){return new();}}
 internal void Save(string assets)=>SettingsStorage.Write(Path.Combine(assets,"ai.json"),JsonSerializer.Serialize(this));
}
internal sealed class AiKeyStore {
 private readonly string _path;
 internal AiKeyStore(string directory=null){_path=Path.Combine(directory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HudWall"),"openai-key.dat");}
 internal string Load(){try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_path),null,DataProtectionScope.CurrentUser));}catch(Exception ex)when(ex is IOException||ex is CryptographicException||ex is UnauthorizedAccessException){return "";}}
 internal void Save(string key){if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Enter an API key.");Directory.CreateDirectory(Path.GetDirectoryName(_path));var bytes=Encoding.UTF8.GetBytes(key.Trim());try{File.WriteAllBytes(_path,ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser));}finally{CryptographicOperations.ZeroMemory(bytes);}}
 internal void Remove(){if(File.Exists(_path))File.Delete(_path);}
}
