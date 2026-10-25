namespace HudWall;
internal static class EditionSettings {
 internal static bool IsJarvis {get {try{return File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"assets","edition.js")).Contains("\"JARVIS\"");}catch{return false;}}}
}
