param()
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = $PSScriptRoot
$destination = Join-Path $root 'gods-eye'
if (Test-Path -LiteralPath $destination) { throw 'gods-eye already exists. Keep it, or rename it before initializing a fresh checkout.' }
$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (!$git -and (Test-Path 'C:\Program Files\Git\cmd\git.exe')) { $git = 'C:\Program Files\Git\cmd\git.exe' }
if (!$git) { throw 'Install Git before initializing.' }
$revision = 'a3a19883b4f9ebb879040e2a6969c689355c3008'
$nodeVersion = '24.22.1'
$nodeSha256 = 'd2046d28c2c3dcd211ac5145c0bf30e2047ef9dfb306693c8c6e2eccd8b95fa5'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('hudwall-source-init-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$previousPath = $env:PATH
$previousPuppeteer = $env:PUPPETEER_SKIP_DOWNLOAD
try {
    $sourceZip = Join-Path $temporary 'source.zip'
    Invoke-WebRequest "https://codeload.github.com/bilawalsidhu/gods-eye-view/zip/$revision" -OutFile $sourceZip
    Expand-Archive -LiteralPath $sourceZip -DestinationPath (Join-Path $temporary 'source')
    $checkout = Join-Path $temporary "source\gods-eye-view-$revision"
    Move-Item -LiteralPath $checkout -Destination $destination
    & $git -C $destination apply --check (Join-Path $root 'patches\gods-eye-hudwall.patch')
    if ($LASTEXITCODE -ne 0) { throw 'Pinned upstream patch validation failed.' }
    & $git -C $destination apply (Join-Path $root 'patches\gods-eye-hudwall.patch')
    if ($LASTEXITCODE -ne 0) { throw 'Could not apply the integration patch.' }
    Copy-Item -LiteralPath (Join-Path $root 'integration\hudwall-server.mjs') -Destination $destination
    $nodeZip = Join-Path $temporary 'node.zip'
    Invoke-WebRequest "https://nodejs.org/dist/v$nodeVersion/node-v$nodeVersion-win-x64.zip" -OutFile $nodeZip
    if ((Get-FileHash -LiteralPath $nodeZip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $nodeSha256) { throw 'Node download checksum mismatch.' }
    Expand-Archive -LiteralPath $nodeZip -DestinationPath (Join-Path $temporary 'runtime')
    $nodeRoot = Join-Path $temporary "runtime\node-v$nodeVersion-win-x64"
    Copy-Item -LiteralPath (Join-Path $nodeRoot 'node.exe') -Destination $destination
    Copy-Item -LiteralPath (Join-Path $nodeRoot 'LICENSE') -Destination (Join-Path $destination 'NODE-LICENSE.txt')
    $env:PATH = $nodeRoot + ';' + $env:PATH
    $env:PUPPETEER_SKIP_DOWNLOAD = 'true'
    & (Join-Path $nodeRoot 'npm.cmd') ci --prefix $destination --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Gods Eye dependency installation failed.' }
    & (Join-Path $nodeRoot 'npm.cmd') run build --prefix $destination
    if ($LASTEXITCODE -ne 0) { throw 'Gods Eye production build failed.' }
    Write-Host 'Gods Eye is initialized and built.'
}
finally {
    $env:PATH = $previousPath
    $env:PUPPETEER_SKIP_DOWNLOAD = $previousPuppeteer
    $resolved = [IO.Path]::GetFullPath($temporary)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $resolved -Leaf).StartsWith('hudwall-source-init-')) { throw 'Unexpected temporary cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
