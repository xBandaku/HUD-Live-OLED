#nullable enable
using System.Windows.Input;
using System.Windows.Interop;
using static DeskNest.Interop.NativeMethods;

namespace DeskNest.Services;

/// <summary>One system-wide hotkey, registered on a message-only window.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xD35;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    private readonly HwndSource _source;
    private bool _registered;

    public HotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("DeskNestHotkey") { ParentWindow = new IntPtr(-3) }); // HWND_MESSAGE
        _source.AddHook(WndProc);
    }

    public event Action? Pressed;

    /// <summary>Registers a hotkey such as "Ctrl+Alt+Space". Returns false if the text is invalid or another app owns it.</summary>
    public bool Register(string gesture)
    {
        Unregister();
        if (!TryParse(gesture, out var mods, out var key))
            return false;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        _registered = RegisterHotKey(_source.Handle, HotkeyId, mods | MOD_NOREPEAT, vk);
        if (!_registered)
            Log.Error($"Hotkey {gesture} could not be registered (in use by another app?)");
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
            UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    public static bool TryParse(string gesture, out uint modifiers, out Key key)
    {
        modifiers = 0;
        key = Key.None;
        foreach (var raw in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "win" or "windows": modifiers |= MOD_WIN; break;
                default:
                    if (!Enum.TryParse(raw, ignoreCase: true, out key))
                        return false;
                    break;
            }
        }
        return key != Key.None && modifiers != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.Dispose();
    }
}
