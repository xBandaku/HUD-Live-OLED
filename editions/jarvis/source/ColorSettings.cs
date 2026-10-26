using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HudWall;

internal sealed class ColorSettings : Form
{
    private readonly string _assets;
    private readonly Dictionary<string, string> _colors = new()
    {
        ["accent"] = "#a93f68", ["accent2"] = "#ce8fa3",
        ["background"] = "#050409", ["text"] = "#ece2e7"
    };

    internal ColorSettings(string assets) : this(assets, null) { }

    internal ColorSettings(string assets, Dictionary<string,string> initial)
    {
        _assets = assets;
        if (initial != null) foreach (var key in _colors.Keys.ToArray())
            if (initial.TryGetValue(key, out string value) && Regex.IsMatch(value ?? "", "^#[0-9a-fA-F]{6}$")) _colors[key] = value;
        Text = "HUD Live color settings";
        ClientSize = new Size(390, 280);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(
                SettingsStorage.Read(Path.Combine(assets, "colors.json")));
            foreach (var key in _colors.Keys.ToArray())
                if (saved != null && saved.TryGetValue(key, out string value) &&
                    Regex.IsMatch(value ?? "", "^#[0-9a-fA-F]{6}$")) _colors[key] = value;
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException) { }

        string[] labels = { "Primary accent", "Secondary accent", "Background", "Text" };
        int i = 0;
        foreach (var key in _colors.Keys.ToArray())
        {
            Controls.Add(new Label { Text = labels[i], AutoSize = true, Location = new Point(20, 25 + i * 45) });
            var button = new Button { Text = _colors[key], BackColor = ColorTranslator.FromHtml(_colors[key]),
                Location = new Point(180, 16 + i * 45), Size = new Size(185, 32), UseVisualStyleBackColor = false };
            button.ForeColor = button.BackColor.GetBrightness() < .5f ? Color.White : Color.Black;
            button.Click += (_, _) =>
            {
                using var picker = new ColorDialog { FullOpen = true, Color = ColorTranslator.FromHtml(_colors[key]) };
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                string hex = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
                _colors[key] = hex;
                button.Text = hex;
                button.BackColor = picker.Color;
                button.ForeColor = picker.Color.GetBrightness() < .5f ? Color.White : Color.Black;
            };
            Controls.Add(button);
            i++;
        }
        var save = new Button { Text = "Save and apply", Location = new Point(180, 215), Size = new Size(185, 36) };
        save.Click += (_, _) =>
        {
            try
            {
                ThemeSettings.Save(_assets, _colors, custom: true);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save colors", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        Controls.Add(save);
        AcceptButton = save;
    }
}
