$ErrorActionPreference = 'Stop'
Start-Process -FilePath (Join-Path $PSScriptRoot 'HudWall.exe') -ArgumentList '--settings' -WindowStyle Hidden
