param([ValidateSet('Stop','EnableStartup','DisableStartup')][string]$Action)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'HudWall.exe'
$marker = Join-Path $env:LOCALAPPDATA 'HudWall\startup.disabled'
$startup = Join-Path ([Environment]::GetFolderPath('Startup')) 'HUD Live Standalone.lnk'
switch ($Action) {
    'Stop' {
        Get-Process HudWall -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process
    }
    'EnableStartup' {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($startup)
        $shortcut.TargetPath = Join-Path $PSScriptRoot 'Start Wallpaper.cmd'
        $shortcut.WindowStyle = 7
        $shortcut.IconLocation = (Join-Path $PSScriptRoot 'hud.ico') + ',0'
        $shortcut.WorkingDirectory = $PSScriptRoot
        $shortcut.Description = 'HUD Live standalone desktop wallpaper'
        $shortcut.Save()
        if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
    }
    'DisableStartup' {
        New-Item -ItemType Directory -Force -Path (Split-Path $marker) | Out-Null
        [IO.File]::WriteAllText($marker, 'disabled')
        if (Test-Path -LiteralPath $startup) { Remove-Item -LiteralPath $startup }
    }
}
