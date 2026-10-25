using System.Text.Json;

namespace HudWall;

internal static class SettingsStore
{
    private static readonly Lazy<string> _root = new(Initialize);
    internal static string Root => _root.Value;
    private static string Initialize()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HudWall", "Settings", EditionSettings.IsJarvis ? "JARVIS" : "OLED");
        Migrate(Path.Combine(AppContext.BaseDirectory, "assets"), root);
        return root;
    }

    internal static void Migrate(string old, string root)
    {
        Directory.CreateDirectory(root);
        string marker = Path.Combine(root, ".migrated");
        if (!File.Exists(marker))
        {
            foreach (string file in Directory.GetFiles(old, "*.json", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(old, file);
                if (Path.GetDirectoryName(relative) != "" && !relative.StartsWith("monitor-colors" + Path.DirectorySeparatorChar)) continue;
                string destination = Path.Combine(root, relative);
                if (File.Exists(destination)) continue;
                try { using var json = JsonDocument.Parse(File.ReadAllText(file)); WriteAllText(destination, json.RootElement.GetRawText()); }
                catch (JsonException ex) { Program.Log("Skipped invalid legacy setting " + relative + ": " + ex.Message); }
            }
            WriteAllText(marker, DateTime.UtcNow.ToString("O"));
        }
    }

    internal static void WriteAllText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static string ReadAllText(string path)
    {
        try
        {
            string text = File.ReadAllText(path);
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) { using var json = JsonDocument.Parse(text); }
            return text;
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException)
        {
            if (!File.Exists(path + ".bak")) throw;
            string backup = File.ReadAllText(path + ".bak");
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) { using var json = JsonDocument.Parse(backup); }
            Program.Log("Recovered setting from backup: " + Path.GetFileName(path));
            return backup;
        }
    }
}
