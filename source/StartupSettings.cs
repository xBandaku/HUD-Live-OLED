using System.Runtime.InteropServices;

namespace HudWall;

internal sealed class StartupSettings
{
    internal static string DisabledMarker => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HudWall", "startup.disabled");
    private readonly string _shortcut;
    private readonly string _directory;
    private readonly string _launcher;
    private readonly string _disabledMarker;

    internal StartupSettings(string startupFolder, string applicationDirectory, string disabledMarker = null)
    {
        _shortcut = Path.Combine(Path.GetFullPath(startupFolder), "HUD Live Standalone.lnk");
        _directory = Path.GetFullPath(applicationDirectory);
        _launcher = Path.Combine(_directory, "Start Wallpaper.cmd");
        _disabledMarker = disabledMarker ?? DisabledMarker;
    }

    internal bool IsEnabled()
    {
        if (!File.Exists(_shortcut)) return false;
        object shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            shortcut = ((dynamic)shell).CreateShortcut(_shortcut);
            return string.Equals((string)((dynamic)shortcut).TargetPath, _launcher, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
        finally { Release(shortcut); Release(shell); }
    }

    internal void SetEnabled(bool enabled)
    {
        if (!enabled) { SettingsStore.WriteAllText(_disabledMarker, "disabled"); File.Delete(_shortcut); return; }
        if (!File.Exists(_launcher)) throw new FileNotFoundException("Keep Start Wallpaper.cmd beside HudWall.exe before enabling startup.");
        Directory.CreateDirectory(Path.GetDirectoryName(_shortcut));
        object shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            shortcut = ((dynamic)shell).CreateShortcut(_shortcut);
            dynamic link = shortcut;
            link.TargetPath = _launcher;
            link.WorkingDirectory = _directory;
            link.WindowStyle = 7;
            link.IconLocation = Path.Combine(_directory, "hud.ico") + ",0";
            link.Description = "HUD Live standalone desktop wallpaper";
            link.Save();
            if (File.Exists(_disabledMarker)) File.Delete(_disabledMarker);
        }
        finally { Release(shortcut); Release(shell); }
    }

    private static void Release(object value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
