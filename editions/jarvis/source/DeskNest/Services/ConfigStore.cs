#nullable enable
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using DeskNest.Models;

namespace DeskNest.Services;

/// <summary>Loads and saves <see cref="AppConfig"/> as JSON under %AppData%\DeskNest, debouncing writes.</summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly DispatcherTimer _saveTimer;

    public ConfigStore()
    {
        Directory.CreateDirectory(DataFolder);
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveNow();
        };
        Config = Load(out var isNew);
        IsFirstRun = isNew;
    }

    /// <summary>Stable per-user organizer profile outside app-container-virtualized AppData.
    /// DESKNEST_DATA_DIR points it elsewhere for testing.</summary>
    public static string DataFolder { get; } =
        Environment.GetEnvironmentVariable("DESKNEST_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".HudWall", "Organizer");

    public static string ConfigPath => Path.Combine(DataFolder, "config.json");

    public AppConfig Config { get; }
    public bool IsFirstRun { get; }

    /// <summary>Schedules a save; repeated calls within the debounce window collapse into one write.</summary>
    public void Save()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        try
        {
            HudWall.SettingsStorage.Write(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Error("Saving config failed", ex);
        }
    }

    private static AppConfig Load(out bool isNew)
    {
        if(!File.Exists(ConfigPath)&&!File.Exists(ConfigPath+".bak")&&string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DESKNEST_DATA_DIR")))
            OrganizerRecovery.ImportBest(OrganizerRecovery.LegacyFolders(),ConfigPath,JsonOptions);
        isNew = !File.Exists(ConfigPath)&&!File.Exists(ConfigPath+".bak");
        if (isNew)
            return new AppConfig();
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(HudWall.SettingsStorage.Read(ConfigPath), JsonOptions) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            // Keep the broken file for the user instead of silently overwriting their layout.
            var backup = ConfigPath + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            try { File.Copy(ConfigPath, backup); } catch { }
            Log.Error($"Config could not be read; a copy was kept at {backup}", ex);
            return new AppConfig();
        }
    }
}
