$ErrorActionPreference = 'Stop'
# Exercise the shipped watchdog with simulated native calls; never reparent real windows.
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
public static class WallpaperDesktop {
    public static int Tick, Attempts, RecoverAt, WindowAt, LostAt, Found;
    public static bool Attached;
    public static void Reset(int recoverAt, int windowAt, int lostAt) {
        Tick=Attempts=Found=0; RecoverAt=recoverAt; WindowAt=windowAt; LostAt=lostAt; Attached=false;
    }
    public static bool IsWindow(IntPtr window) {
        if(window == new IntPtr(100)) return Tick >= WindowAt;
        return window == new IntPtr(200) && Attached && Tick != LostAt;
    }
    public static IntPtr FindHost(int owner) { Found++; return Tick >= WindowAt ? new IntPtr(100) : IntPtr.Zero; }
    public static IntPtr GetParent(IntPtr window) { return Attached && Tick != LostAt ? new IntPtr(200) : IntPtr.Zero; }
    public static bool Covers(IntPtr window,int x,int y,int width,int height) { return Attached; }
    public static bool Attach(IntPtr window,int x,int y,int width,int height) {
        Attempts++; Attached = Tick >= RecoverAt; return Attached;
    }
}
'@
$launcher = Join-Path $PSScriptRoot '../source/dist/Launch.ps1'
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($launcher, [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
$watchdog = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Watch-WallpaperHost'
}, $true)
if (!$watchdog) { throw 'Launcher watchdog missing' }
Invoke-Expression $watchdog.Extent.Text
function Start-Sleep { param($Seconds) }
function Write-LauncherLog { param($message) }
function New-HostFixture {
    $hostFixture = [pscustomobject]@{ HasExited = $false; Id = 123; Ticks = 0 }
    $hostFixture | Add-Member ScriptMethod Refresh {
        $this.Ticks++
        [WallpaperDesktop]::Tick = $this.Ticks
        $this.HasExited = $this.Ticks -ge 6
    }
    return $hostFixture
}
$bounds = [Windows.Forms.SystemInformation]::VirtualScreen
[WallpaperDesktop]::Reset(3, 0, -1)
Watch-WallpaperHost (New-HostFixture) ([IntPtr]100) $bounds $false
if (![WallpaperDesktop]::Attached -or [WallpaperDesktop]::Attempts -ne 3) { throw 'Initial attachment failure was not retried until success' }
Write-Output 'PASS Initial attachment failure recovers after Explorer becomes ready'
[WallpaperDesktop]::Reset(3, 3, -1)
Watch-WallpaperHost (New-HostFixture) ([IntPtr]::Zero) $bounds $false
if (![WallpaperDesktop]::Attached -or [WallpaperDesktop]::Found -ne 3 -or [WallpaperDesktop]::Attempts -ne 1) { throw 'Delayed host window did not recover' }
Write-Output 'PASS A host window that appears late is rediscovered and attached'
[WallpaperDesktop]::Reset(1, 0, 3)
[WallpaperDesktop]::Attached = $true
Watch-WallpaperHost (New-HostFixture) ([IntPtr]100) $bounds $true
if (![WallpaperDesktop]::Attached -or [WallpaperDesktop]::Attempts -ne 1) { throw 'Lost parent did not recover or stable attachment was repeatedly changed' }
Write-Output 'PASS Lost parent recovers and stable attachment stays unchanged'
$exited = New-HostFixture
$exited.HasExited = $true
[WallpaperDesktop]::Reset(0, 0, -1)
Watch-WallpaperHost $exited ([IntPtr]100) $bounds $false
if ([WallpaperDesktop]::Attempts -ne 0) { throw 'Watchdog attached after host exited' }
Write-Output 'PASS Host exit stops attachment retries'

# Check that failed initial attachment reaches the tested watchdog instead of returning or blocking on a dialog.
$failure = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.IfStatementAst] -and $node.Clauses[0].Item1.Extent.Text -eq '!$attached'
}, $true)
if (!$failure) { throw 'Initial failure branch missing' }
$blocked = $failure.Clauses[0].Item2.Find({ param($node)
    $node -is [Management.Automation.Language.ReturnStatementAst] -or $node.Extent.Text -match '^\[Windows\.Forms\.MessageBox\]::Show'
}, $true)
if ($blocked) { throw 'Initial failure blocks continuing recovery' }
Write-Output 'PASS Initial failure path does not exit or block the watchdog'
