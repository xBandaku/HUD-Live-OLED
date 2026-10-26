param([switch]$Preview)
$ErrorActionPreference = 'Stop'
$logDir = Join-Path $env:LOCALAPPDATA 'HudWall'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$logPath = Join-Path $logDir 'launcher.log'
function Write-LauncherLog([string]$message) {
    Add-Content -LiteralPath $logPath -Value ("$(Get-Date -Format o) $message")
}
trap {
    $message = $_.Exception.Message
    Write-LauncherLog ("FAILED: " + ($_ | Out-String))
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show("HUD Live could not start:`r`n$message`r`n`r`nDetails: $logPath", 'HUD Live startup error') | Out-Null
    exit 1
}
function Register-AutomaticStartup {
    try {
        & (Join-Path $PSScriptRoot 'Manage.ps1') -Action EnableStartup
        Write-LauncherLog "Automatic Windows startup registered for $PSScriptRoot"
    }
    catch {
        Write-LauncherLog ("Automatic startup registration failed: " + $_.Exception.Message)
        Add-Type -AssemblyName System.Windows.Forms
        [Windows.Forms.MessageBox]::Show("HUD Live is running, but automatic startup could not be registered:`r`n" + $_.Exception.Message, 'HUD Live automatic startup') | Out-Null
    }
}
Write-LauncherLog "Starting launcher v41 from $PSScriptRoot; preview=$Preview"
$exe = Join-Path $PSScriptRoot 'HudWall.exe'
if (!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'assets\index.html'))) {
    throw 'Extract the entire ZIP into a folder before starting. Keep HudWall.exe, the DLL files, and assets together.'
}
if (!$Preview) { Register-AutomaticStartup }
$username = ConvertTo-Json -InputObject ([Environment]::UserName) -Compress
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'assets\user.js'), "window.HUD_USERNAME = $username;")
if (Get-Process HudWall -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) { if (!$Preview) { Register-AutomaticStartup }; Write-LauncherLog 'This copy is already running'; return }
if (Get-Process HudWall -ErrorAction SilentlyContinue) {
    throw 'Another HUD Live copy is running. Exit it from its tray icon before starting this copy.'
}
if ($Preview) { Start-Process -FilePath $exe -ArgumentList '--preview'; return }
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class WallpaperDesktop {
 [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
 [DllImport("user32.dll")] public static extern int MapWindowPoints(IntPtr from,IntPtr to,ref POINT p,uint count);
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 public static bool Covers(IntPtr h,int x,int y,int w,int height) {RECT r;return GetWindowRect(h,out r)&&r.Left==x&&r.Top==y&&r.Right-r.Left==w&&r.Bottom-r.Top==height;}
 public delegate bool Callback(IntPtr h, IntPtr p);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder text,int length);
 public static IntPtr FindHost(int pid) {
   IntPtr found=IntPtr.Zero;
   EnumWindows((h,p)=>{uint id;GetWindowThreadProcessId(h,out id); if(id==pid) {var text=new StringBuilder(256);GetWindowText(h,text,256);if(text.ToString()=="HUD Live") {found=h;return false;}}return true;},IntPtr.Zero);
   return found;
 }
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c,string t);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p,IntPtr a,string c,string t);
 [DllImport("user32.dll")] public static extern bool EnumWindows(Callback c,IntPtr p);
 [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint f,uint t,out IntPtr r);
 [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetParent(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h,int n);
 [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr h,int n,IntPtr v);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int w,int z,uint f);
 public static IntPtr Worker() {
   var progman=FindWindow("Progman",null);
   // Windows 11 can put the wallpaper WorkerW inside Progman.
   // Prefer that existing host instead of requesting another shell layer.
   var nested=FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);
   if(progman!=IntPtr.Zero && nested!=IntPtr.Zero) return nested;
   IntPtr worker=IntPtr.Zero;
   EnumWindows((h,p)=>{if(FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero) { var candidate=FindWindowEx(IntPtr.Zero,h,"WorkerW",null); if(candidate!=IntPtr.Zero) {worker=candidate;return false;} }return true;},IntPtr.Zero);
   return worker;
 }
 public static bool Attach(IntPtr h,int x,int y,int w,int height) {
   var worker=Worker();
   if(worker==IntPtr.Zero) {
     IntPtr r; var progman=FindWindow("Progman",null);
     SendMessageTimeout(progman,0x052C,new IntPtr(0xD),IntPtr.Zero,2,1000,out r);
     SendMessageTimeout(progman,0x052C,new IntPtr(0xD),new IntPtr(1),2,1000,out r);
     worker=Worker();
     if(worker==IntPtr.Zero) worker=FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);
   }
   if(worker==IntPtr.Zero) return false;
   long style=GetWindowLongPtr(h,-16).ToInt64();
   SetWindowLongPtr(h,-16,new IntPtr((style & ~0x80000000L) | 0x40000000L));
   long ex=GetWindowLongPtr(h,-20).ToInt64();
   SetWindowLongPtr(h,-20,new IntPtr((ex & ~0x40000L) | 0x08000080L));
   SetParent(h,worker);
   var origin=new POINT {X=x,Y=y}; MapWindowPoints(IntPtr.Zero,worker,ref origin,1);
   SetWindowPos(h,IntPtr.Zero,origin.X,origin.Y,w,height,0x0074);
   return GetParent(h)==worker;
 }
}
'@
$process = Start-Process -FilePath $exe -ArgumentList '--preview' -WindowStyle Hidden -PassThru
$hwnd = [IntPtr]::Zero
for ($i=0; $i -lt 100; $i++) {
    Start-Sleep -Milliseconds 200
    $process.Refresh()
    if ($process.HasExited) { throw "The HUD host exited before its window opened (exit code $($process.ExitCode)). Check hudwall.log beside launcher.log." }
    $hwnd = [WallpaperDesktop]::FindHost($process.Id)
    if ($hwnd -ne [IntPtr]::Zero) { break }
}
[WallpaperDesktop]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$bounds = [Windows.Forms.SystemInformation]::VirtualScreen
Write-LauncherLog "Host PID=$($process.Id); window=$hwnd; desktop=$bounds"
$attached = $false
if ($hwnd -ne [IntPtr]::Zero) {
    for ($attempt=0; $attempt -lt 15; $attempt++) {
        $process.Refresh()
        if ($process.HasExited) { break }
        if ([WallpaperDesktop]::Attach($hwnd,$bounds.X,$bounds.Y,$bounds.Width,$bounds.Height)) { $attached=$true; break }
        Start-Sleep -Seconds 1
    }
}
if (!$attached) {
    Write-LauncherLog "Attachment pending: host=$hwnd; retrying until Explorer is ready or HUD Live exits"
} else {
    Write-LauncherLog "Attached to desktop parent=$([WallpaperDesktop]::GetParent($hwnd))"
}
Register-AutomaticStartup

function Watch-WallpaperHost($HostProcess, [IntPtr]$HostWindow, $DesktopBounds, [bool]$InitiallyAttached) {
    $isAttached = $InitiallyAttached
    while (!$HostProcess.HasExited) {
        Start-Sleep -Seconds 3
        $HostProcess.Refresh()
        if ($HostProcess.HasExited) { break }
        if (![WallpaperDesktop]::IsWindow($HostWindow)) {
            $HostWindow = [WallpaperDesktop]::FindHost($HostProcess.Id)
            $isAttached = $false
        }
        if ($HostWindow -eq [IntPtr]::Zero) { continue }
        $next = [Windows.Forms.SystemInformation]::VirtualScreen
        if (!$isAttached -or ![WallpaperDesktop]::IsWindow([WallpaperDesktop]::GetParent($HostWindow)) -or $next -ne $DesktopBounds -or ![WallpaperDesktop]::Covers($HostWindow,$next.X,$next.Y,$next.Width,$next.Height)) {
            $DesktopBounds = $next
            $isAttached = [WallpaperDesktop]::Attach($HostWindow,$DesktopBounds.X,$DesktopBounds.Y,$DesktopBounds.Width,$DesktopBounds.Height)
            if ($isAttached) { Write-LauncherLog "Desktop attachment recovered for host=$HostWindow" }
        }
    }
}
Watch-WallpaperHost $process $hwnd $bounds $attached
Write-LauncherLog "Host exited with code $($process.ExitCode)"
