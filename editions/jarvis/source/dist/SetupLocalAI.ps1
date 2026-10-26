$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$ollamaPath = Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama.exe'
if (-not (Test-Path -LiteralPath $ollamaPath)) {
    if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) {
        throw 'Install Ollama from https://ollama.com/download/windows, then run this setup again.'
    }
    winget.exe install --id Ollama.Ollama --exact --source winget --scope user --silent --accept-source-agreements --accept-package-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $ollamaPath)) { throw 'Ollama installation did not finish. Install it from ollama.com and retry.' }
}
try { $null = Invoke-RestMethod 'http://127.0.0.1:11434/api/version' -TimeoutSec 3 }
catch {
    Start-Process -FilePath $ollamaPath -ArgumentList 'serve' -WindowStyle Hidden
    Start-Sleep -Seconds 3
}
Write-Host 'Downloading/checking local Qwen3 8B model (about 5.2 GB)...'
& $ollamaPath pull qwen3:8b
if ($LASTEXITCODE -ne 0) { throw 'The local model download failed. Retry setup when the connection is available.' }

$modelDirectory = Join-Path $env:LOCALAPPDATA 'HudWall\Models'
New-Item -ItemType Directory -Path $modelDirectory -Force | Out-Null
$modelPath = Join-Path $modelDirectory 'kokoro.onnx'
$expectedHash = '0CFD5E79AAB70A3D8C1A57DC639835110DDB32C9F5FF4FDD1F4DB202EA43BB05'
$valid = (Test-Path -LiteralPath $modelPath) -and ((Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash -eq $expectedHash)
if (-not $valid) {
    Write-Host 'Downloading local neural voice model (about 326 MB)...'
    $downloadPath = Join-Path $modelDirectory 'kokoro.onnx.download'
    Invoke-WebRequest -UseBasicParsing 'https://github.com/Lyrcaxis/KokoroSharpBinaries/releases/download/v2.0.0/kokoro.onnx' -OutFile $downloadPath
    if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Voice model integrity check failed. Retry setup.' }
    Move-Item -LiteralPath $downloadPath -Destination $modelPath -Force
}
$speechPath = Join-Path $modelDirectory 'ggml-base.en.bin'
$speechHash = 'A03779C86DF3323075F5E796CB2CE5029F00EC8869EEE3FDFB897AFE36C6D002'
if (-not ((Test-Path -LiteralPath $speechPath) -and ((Get-FileHash -LiteralPath $speechPath -Algorithm SHA256).Hash -eq $speechHash))) {
    Write-Host 'Downloading local Whisper speech recognition model (about 148 MB)...'
    $speechDownload = Join-Path $modelDirectory 'ggml-base.en.bin.download'
    Invoke-WebRequest -UseBasicParsing 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin' -OutFile $speechDownload
    if ((Get-FileHash -LiteralPath $speechDownload -Algorithm SHA256).Hash -ne $speechHash) { throw 'Speech model integrity check failed. Retry setup.' }
    Move-Item -LiteralPath $speechDownload -Destination $speechPath -Force
}
$settingsPath = Join-Path $PSScriptRoot 'assets\ai.json'
$settings = @{ Enabled = $true; Provider = 'Ollama'; LocalModel = 'qwen3:8b'; Model = 'gpt-5.4-mini'; PcTools = $true; WebSearch = $true }
if (Test-Path -LiteralPath $settingsPath) {
    $saved = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    foreach ($property in $saved.PSObject.Properties) { $settings[$property.Name] = $property.Value }
}
$settings.Provider = 'Ollama'
$settings.LocalModel = 'qwen3:8b'
$settings.Enabled = $true
[IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Compress))
Write-Host 'Local AI, Whisper recognition and British neural speech are ready. Start or restart JARVIS.'
