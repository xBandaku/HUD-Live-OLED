#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeskNest.Views;

/// <summary>Small "enter a name" prompt, built in code to keep it self-contained.</summary>
public sealed class InputDialog : Window
{
    private readonly TextBox _box;

    private InputDialog(string title, string prompt, string initial)
    {
        Icon=DeskNest.Services.HudWindowIcon.Image;
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;

        _box = new TextBox { Text = initial, Width = 320, Margin = new Thickness(0, 6, 0, 12), Padding = new Thickness(4, 3, 4, 3) };
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        ok.Click += (_, _) => DialogResult = true;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(new TextBlock { Text = prompt });
        root.Children.Add(_box);
        root.Children.Add(buttons);
        Content = root;

        Loaded += (_, _) =>
        {
            _box.Focus();
            // Select the name without its extension, like Explorer's rename.
            var dot = initial.LastIndexOf('.');
            if (dot > 0) _box.Select(0, dot); else _box.SelectAll();
            Keyboard.Focus(_box);
        };
    }

    /// <summary>Returns the entered text, or null if cancelled or left empty.</summary>
    public static string? Ask(string title, string prompt, string initial = "")
    {
        var dialog = new InputDialog(title, prompt, initial);
        if (dialog.ShowDialog() != true)
            return null;
        var text = dialog._box.Text.Trim();
        return text.Length == 0 ? null : text;
    }
}
