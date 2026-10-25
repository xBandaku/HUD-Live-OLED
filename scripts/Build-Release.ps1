param([string]$Version)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputRoot = Join-Path $projectRoot 'artifacts'
$packageRoot = Join-Path $outputRoot ('package-' + [Guid]::NewGuid().ToString('N'))
$applicationRoot = Join-Path $packageRoot 'HUD-Live-OLED'
New-Item -ItemType Directory -Path $applicationRoot -Force | Out-Null
$publishArgs = @('publish', (Join-Path $projectRoot 'source/HudWall.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $applicationRoot)
if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Version must use the form 16.1.0 or 16.1.0-beta.1' }
    $publishArgs += "-p:Version=$Version"
}
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
Get-ChildItem -LiteralPath $projectRoot -File | Where-Object { $_.Extension -in '.cmd', '.ps1' -or $_.Name -in 'README.md', 'README.txt', 'README-v16.txt', 'CHANGELOG.md' } |
    Copy-Item -Destination $applicationRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'source/hud.ico') -Destination $applicationRoot
foreach ($required in @('HudWall.exe', 'assets/index.html', 'assets/edition.js', 'hud.ico', 'Start Wallpaper.cmd', 'Launch.ps1')) {
    if (!(Test-Path -LiteralPath (Join-Path $applicationRoot $required))) { throw "Package is missing $required" }
}
$archive = Join-Path $outputRoot 'HUD-Live-OLED-win-x64.zip'
Compress-Archive -LiteralPath $applicationRoot -DestinationPath $archive -Force
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($archive + '.sha256', "$checksum  HUD-Live-OLED-win-x64.zip`n")
Write-Output "Release package: $archive"
