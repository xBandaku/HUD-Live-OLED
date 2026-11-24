using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace HudWall;

internal sealed class PauseAppsDialog : Form
{
    private readonly CheckedListBox _apps = new() { CheckOnClick = true, Location = new Point(18, 65), Size = new Size(440, 280) };
    internal List<string> SelectedApps => _apps.CheckedItems.Cast<string>().Select(Path.GetFileNameWithoutExtension).ToList();

    internal PauseAppsDialog(IEnumerable<string> selected)
    {
        Text = "Pause for selected apps";
        ClientSize = new Size(480, 410);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        var saved = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(saved, StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
                try { if (process.Id != Environment.ProcessId && process.ProcessName != "explorer" && process.MainWindowHandle != IntPtr.Zero) names.Add(process.ProcessName); }
                catch { }
        }
        Controls.Add(new Label { Text = "Pause while a checked app is active, including windowed games.\r\nSwitch to another app or the desktop to resume.", Location = new Point(18, 15), Size = new Size(440, 45) });
        foreach (string name in names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) _apps.Items.Add(name + ".exe", saved.Contains(name));
        Controls.Add(_apps);
        var add = new Button { Text = "Add executable...", Location = new Point(18, 360), Size = new Size(155, 32) };
        add.Click += (_, _) => {
            using var picker = new OpenFileDialog { Filter = "Windows applications (*.exe)|*.exe", Title = "Select a game or app executable" };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            string name = Path.GetFileName(picker.FileName);
            int index = _apps.Items.Cast<string>().ToList().FindIndex(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = _apps.Items.Add(name);
            _apps.SetItemChecked(index, true);
        };
        Controls.Add(add);
        var save = new Button { Text = "Save", Location = new Point(330, 360), Size = new Size(125, 32), DialogResult = DialogResult.OK };
        Controls.Add(save); AcceptButton = save;
    }
}
