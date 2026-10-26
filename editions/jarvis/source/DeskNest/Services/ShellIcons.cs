#nullable enable
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskNest.Interop;
using static DeskNest.Interop.NativeMethods;

namespace DeskNest.Services;

/// <summary>Fetches Explorer's own icons (with overlays and alpha) through the system image list.</summary>
public static class ShellIcons
{
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns a frozen icon for <paramref name="path"/>; <paramref name="size"/> above 48 uses the 256px list.</summary>
    public static ImageSource? Get(string path, int size)
    {
        var list = size > 48 ? SHIL_JUMBO : size > 32 ? SHIL_EXTRALARGE : SHIL_LARGE;
        var key = CacheKey(path) + "|" + list;
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var icon = Load(path, list);
        if (icon is not null)
            Cache[key] = icon;
        return icon;
    }

    /// <summary>Drops cached icons for a path, e.g. after a shortcut's target changed.</summary>
    public static void Forget(string path)
    {
        var prefix = CacheKey(path) + "|";
        foreach (var key in Cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            Cache.TryRemove(key, out _);
    }

    // Files whose icon depends only on their extension share one cache entry; anything that carries its own icon is per path.
    private static string CacheKey(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (Directory.Exists(path) || ext is "" or ".exe" or ".lnk" or ".url" or ".ico" or ".appref-ms" or ".msc" or ".cpl")
            return path;
        return "*" + ext;
    }

    private static ImageSource? Load(string path, int list)
    {
        var info = new SHFILEINFO();
        uint flags = SHGFI_SYSICONINDEX;
        uint attrs = 0;
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            // Missing pin: show the generic icon for its extension instead of nothing.
            flags |= SHGFI_USEFILEATTRIBUTES;
            attrs = Path.HasExtension(path) ? FILE_ATTRIBUTE_NORMAL : FILE_ATTRIBUTE_DIRECTORY;
        }

        if (SHGetFileInfo(path, attrs, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) == IntPtr.Zero)
            return null;

        var iid = IID_IImageList;
        if (SHGetImageList(list, ref iid, out var imageList) != 0 || imageList is null)
            return null;

        if (imageList.GetIcon(info.iIcon, ILD_TRANSPARENT, out var hIcon) != 0 || hIcon == IntPtr.Zero)
            return null;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
