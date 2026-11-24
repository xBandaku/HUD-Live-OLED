#nullable enable
using System.IO;
using System.Windows.Media;
using DeskNest.Services;

namespace DeskNest.Views;

/// <summary>One icon tile in a frame or the launcher.</summary>
public sealed class ItemView
{
    /// <param name="friendlyNames">Show an app's description ("Windows Calculator") instead of its file name, for pinned items.</param>
    public ItemView(string path, int iconSize, bool friendlyNames = false)
    {
        Path = path;
        IsFolder = Directory.Exists(path);
        Exists = IsFolder || File.Exists(path);
        Name = (friendlyNames && Exists ? AppDescription(path) : null) ?? DisplayName(path);
        Icon = ShellIcons.Get(path, iconSize);
        IconSize = iconSize;
        if (Exists)
        {
            var info = IsFolder ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
            Modified = info.LastWriteTime;
            IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden) || info.Attributes.HasFlag(FileAttributes.System);
        }
    }

    public string Path { get; }
    public string Name { get; }
    public ImageSource? Icon { get; }
    public int IconSize { get; }
    public bool IsFolder { get; }
    public bool Exists { get; }
    public bool IsHidden { get; }
    public DateTime Modified { get; }
    public double Opacity => Exists ? 1.0 : 0.45;
    public double TileWidth => Math.Max(76, IconSize + 40);
    public string Extension => IsFolder ? "" : System.IO.Path.GetExtension(Path).ToLowerInvariant();
    public string Tooltip => Exists ? Path : $"{Path}\n(missing)";

    private static string? AppDescription(string path)
    {
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            var description = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            return string.IsNullOrEmpty(description) ? null : description;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Explorer hides .lnk / .url extensions; so do we.</summary>
    private static string DisplayName(string path)
    {
        var trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar);
        if (trimmed.Length <= 3 && trimmed.EndsWith(':'))
            return trimmed; // drive root, e.g. "D:"
        var ext = System.IO.Path.GetExtension(trimmed).ToLowerInvariant();
        return ext is ".lnk" or ".url" or ".appref-ms"
            ? System.IO.Path.GetFileNameWithoutExtension(trimmed)
            : System.IO.Path.GetFileName(trimmed);
    }
}
