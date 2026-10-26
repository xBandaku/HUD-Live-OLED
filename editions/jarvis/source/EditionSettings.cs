using System.Drawing;
using System.Windows.Forms;
namespace HudWall;
internal static class EditionSettings {
 internal static bool IsJarvis {get {try{return SettingsStorage.Read(Path.Combine(AppContext.BaseDirectory,"assets","edition.js")).Contains("\"JARVIS\"");}catch{return false;}}}
}
