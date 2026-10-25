using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HudWall;

internal sealed partial class HudForm
{
    private readonly System.Windows.Forms.Timer _navigationTimeout = new() { Interval = 30000 };
    private bool _recovering, _closing, _startupFailed;
    private int _recoveryAttempts;
    private DateTime _lastLoaded;
    private string ReadyPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HudWall", "ready-" + Environment.ProcessId + ".json");

    private void InitializeReliability()
    {
        SetReadiness("loading", "Initializing HUD");
        _navigationTimeout.Tick += (_, _) => { _navigationTimeout.Stop(); RecoverPage("Page load timed out", false); };
        FormClosing += (_, _) => { _closing = true; _navigationTimeout.Stop(); };
        FormClosed += (_, _) => { _navigationTimeout.Dispose(); try { File.Delete(ReadyPath); File.Delete(ReadyPath + ".bak"); } catch { } };
    }

    private void SetReadiness(string state, string message)
    {
        try { SettingsStore.WriteAllText(ReadyPath, JsonSerializer.Serialize(new { state, message, pid = Environment.ProcessId })); }
        catch (Exception ex) { Program.Log("Readiness update failed: " + ex.Message); }
    }

    private void StartupFailed(Exception exception)
    {
        if (_closing || _startupFailed) return;
        _startupFailed = true;
        _navigationTimeout.Stop();
        SetReadiness("failed", exception.Message);
        bool runtime = exception is WebView2RuntimeNotFoundException;
        string message = runtime
            ? "HUD Live needs the Microsoft Edge WebView2 Runtime. Install or repair the Evergreen x64 Runtime, then start HUD Live again.\n\nOpen Microsoft's download page now?"
            : "HUD Live could not load its display.\n\n" + exception.Message + "\n\nDetails are in %LOCALAPPDATA%\\HudWall\\hudwall.log.\nOpen the log folder now?";
        if (MessageBox.Show(message, "HUD Live could not start", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
        {
            try { Process.Start(new ProcessStartInfo(runtime ? "https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section" : Path.GetDirectoryName(ReadyPath)) { UseShellExecute = true }); }
            catch (Exception ex) { Program.Log("Open help: " + ex.Message); }
        }
        Close();
    }

    private async void RecoverPage(string reason, bool recreate)
    {
        if (_recovering || _closing || _startupFailed) return;
        _navigationTimeout.Stop();
        if (_lastLoaded != default && DateTime.UtcNow - _lastLoaded > TimeSpan.FromMinutes(5)) _recoveryAttempts = 0;
        if (++_recoveryAttempts > 3) { StartupFailed(new InvalidOperationException(reason + ". Automatic recovery failed three times. Restart HUD Live.")); return; }
        _recovering = true;
        Program.Log("Recovery attempt " + _recoveryAttempts + ": " + reason);
        SetReadiness("loading", reason);
        try
        {
            await Task.Delay(_recoveryAttempts * 1000);
            if (_closing) return;
            if (recreate)
            {
                Controls.Remove(_web);
                _web.Dispose();
                _web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = _nativeTransparent ? System.Drawing.Color.Transparent : System.Drawing.Color.Black };
                Controls.Add(_web);
                await InitAsync();
            }
            else { _navigationTimeout.Start(); _web.CoreWebView2.Reload(); }
        }
        catch (Exception ex) { StartupFailed(ex); }
        finally { _recovering = false; }
    }
}
