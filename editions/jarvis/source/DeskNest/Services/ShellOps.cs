#nullable enable
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.VisualBasic.FileIO;
using static DeskNest.Interop.NativeMethods;

namespace DeskNest.Services;

/// <summary>File actions that go through the shell, so users get Explorer's own progress, conflict and recycle-bin behavior.</summary>
public static class ShellOps
{
    public static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError($"Could not open {Path.GetFileName(path)}", ex);
        }
    }

    public static void OpenInExplorer(string folder)
    {
        if (Directory.Exists(folder))
            Open(folder);
    }

    public static void RevealInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError("Could not open the file location", ex);
        }
    }

    public static void ShowProperties(string path)
    {
        var info = new SHELLEXECUTEINFO
        {
            cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
            fMask = SEE_MASK_INVOKEIDLIST,
            lpVerb = "properties",
            lpFile = path,
            nShow = SW_SHOWNORMAL,
        };
        ShellExecuteEx(ref info);
    }

    /// <summary>Sends to the Recycle Bin (never a permanent delete); Explorer asks for confirmation.</summary>
    public static void Recycle(string path)
    {
        try
        {
            if (Directory.Exists(path))
                FileSystem.DeleteDirectory(path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
            else if (File.Exists(path))
                FileSystem.DeleteFile(path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
        }
        catch (Exception ex)
        {
            ShowError($"Could not delete {Path.GetFileName(path)}", ex);
        }
    }

    public static void Rename(string path, string newName)
    {
        try
        {
            if (Directory.Exists(path))
                FileSystem.RenameDirectory(path, newName);
            else
                FileSystem.RenameFile(path, newName);
        }
        catch (Exception ex)
        {
            ShowError($"Could not rename {Path.GetFileName(path)}", ex);
        }
    }

    /// <summary>Moves or copies items into <paramref name="folder"/> with the shell's dialogs for conflicts.</summary>
    public static void Transfer(IEnumerable<string> sources, string folder, bool copy)
    {
        foreach (var source in sources)
        {
            var name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
            var dest = Path.Combine(folder, name);
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                continue;
            // Dropping a folder into itself would recurse forever.
            if (Directory.Exists(source) && IsInside(folder, source))
                continue;
            try
            {
                if (Directory.Exists(source))
                {
                    if (copy) FileSystem.CopyDirectory(source, dest, UIOption.AllDialogs, UICancelOption.DoNothing);
                    else FileSystem.MoveDirectory(source, dest, UIOption.AllDialogs, UICancelOption.DoNothing);
                }
                else if (File.Exists(source))
                {
                    if (copy) FileSystem.CopyFile(source, dest, UIOption.AllDialogs, UICancelOption.DoNothing);
                    else FileSystem.MoveFile(source, dest, UIOption.AllDialogs, UICancelOption.DoNothing);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ShowError($"Could not {(copy ? "copy" : "move")} {name}", ex);
            }
        }
    }

    /// <summary>Moves silently, picking "name (2).ext" on conflicts. Used by auto-sort, which runs unattended.</summary>
    public static string? MoveQuietly(string source, string folder)
    {
        Directory.CreateDirectory(folder);
        var dest = UniqueDestination(folder, Path.GetFileName(source));
        if (Directory.Exists(source))
            Directory.Move(source, dest);
        else
            File.Move(source, dest);
        return dest;
    }

    public static bool IsInside(string path, string folder)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string UniqueDestination(string folder, string fileName)
    {
        var dest = Path.Combine(folder, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var n = 2; File.Exists(dest) || Directory.Exists(dest); n++)
            dest = Path.Combine(folder, $"{stem} ({n}){ext}");
        return dest;
    }

    private static void ShowError(string message, Exception ex)
    {
        Log.Error(message, ex);
        MessageBox.Show($"{message}.\n\n{ex.Message}", "DeskNest", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
