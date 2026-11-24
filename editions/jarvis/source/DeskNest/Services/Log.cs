#nullable enable
using System.IO;

namespace DeskNest.Services;

/// <summary>Minimal append-only log in the data folder, for diagnosing problems on users' machines.</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static string LogPath => Path.Combine(ConfigStore.DataFolder, "desknest.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 1_000_000)
                    info.Delete();
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
