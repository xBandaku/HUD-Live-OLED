# Builds "HUD Live JARVIS Local v<version>.zip" next to this folder: a self-contained publish, the scripts,
# guides, voices and saved settings from dist\, and this source tree under source\.
param([string]$Output)
$ErrorActionPreference = 'Stop'
if (-not $Output) {
    $version = ([xml](Get-Content (Join-Path $PSScriptRoot 'HudWall.csproj'))).Project.PropertyGroup.Version
    $Output = Join-Path (Split-Path $PSScriptRoot -Parent) "HUD Live JARVIS Local v$version.zip"
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('hudwall-package-' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $work 'HUD Live JARVIS Local'
try {
    dotnet publish (Join-Path $PSScriptRoot 'HudWall.csproj') -c Release -r win-x64 --self-contained true -o $stage -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

    Copy-Item -Path (Join-Path $PSScriptRoot 'dist\*') -Destination $stage -Recurse -Force

    # The bundled upstream source/runtime lives alongside the HudWall source tree.
    $globe = Join-Path (Split-Path $PSScriptRoot -Parent) 'gods-eye'
    if (!(Test-Path -LiteralPath $globe)) { throw 'Keep the gods-eye folder beside source when rebuilding.' }
    Copy-Item -LiteralPath $globe -Destination (Join-Path $stage 'gods-eye') -Recurse -Force

    $source = Join-Path $stage 'source'
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    Get-ChildItem $PSScriptRoot -Force | Where-Object { $_.Name -notin 'bin', 'obj' } |
        Copy-Item -Destination $source -Recurse -Force

    if (Test-Path $Output) { Remove-Item $Output -Force }
    # Entries are written one by one: Compress-Archive and ZipFile on Windows PowerShell 5.1 store backslashes,
    # which other unzip tools turn into flat file names.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($Output, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem $work -Recurse -File) {
            $name = $file.FullName.Substring($work.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $archive.Dispose()
    }
    Write-Host "Wrote $Output"
}
finally {
    $resolvedWork = [IO.Path]::GetFullPath($work)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolvedWork.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $resolvedWork -Leaf).StartsWith('hudwall-package-')) { throw 'Unexpected package cleanup target.' }
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
}
