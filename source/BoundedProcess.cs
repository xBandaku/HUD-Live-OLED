using System.Diagnostics;

namespace HudWall;

internal static class BoundedProcess
{
    // Drain both pipes concurrently. The deadline covers exit AND output reads.
    internal static string Run(string executable, string arguments, int timeoutMs = 2500)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable, arguments) {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true } };
        using var deadline = new CancellationTokenSource(timeoutMs);
        try
        {
            if (!process.Start()) return null;
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = process.StandardError.ReadToEndAsync(deadline.Token);
            process.WaitForExitAsync(deadline.Token).GetAwaiter().GetResult();
            Task.WhenAll(output, error).GetAwaiter().GetResult();
            return process.ExitCode == 0 ? output.Result : null;
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is System.ComponentModel.Win32Exception || ex is IOException || ex is InvalidOperationException)
        {
            return null;
        }
        finally
        {
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(500); } }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is NotSupportedException) { }
        }
    }
}
