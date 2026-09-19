param(
    [Parameter(Mandatory=$true)][string]$RuntimeRoot,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet("Local","Managed")][string]$Edition = "Local"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$runtime = [IO.Path]::GetFullPath($RuntimeRoot)
$out = [IO.Path]::GetFullPath($OutputDirectory)
$required = @(
    "yt-dlp.exe",
    "ffmpeg.exe",
    "ffprobe.exe",
    "deno.exe",
    "whisper\whisper-cli.exe",
    "whisper\whisper.dll",
    "whisper\ggml.dll",
    "whisper\ggml-base.dll",
    "whisper\ggml-base.bin"
)
foreach ($relative in $required) {
    $candidate = Join-Path $runtime $relative
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Bundled runtime is incomplete: $relative"
    }
}

if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
$dotnet = if (Test-Path 'D:\CODEX\Portable\dotnet-sdk-10\dotnet.exe') {
    'D:\CODEX\Portable\dotnet-sdk-10\dotnet.exe'
} else { 'dotnet' }

Push-Location $repo
try {
    & $dotnet publish 'src\VideoGrabber.App\VideoGrabber.App.csproj' `
        -c Release -r win-x64 --self-contained true --no-restore --nologo `
        "-p:VideoGrabberEdition=$Edition" `
        "-p:VideoGrabberBundledRuntimeRoot=$runtime" `
        -o $out
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
} finally { Pop-Location }

$exeName = if ($Edition -eq 'Managed') { 'VideoGrabber.Managed.exe' } else { 'VideoGrabber.exe' }
$exe = Join-Path $out $exeName
if (-not (Test-Path -LiteralPath $exe)) { throw "Published executable is missing: $exe" }
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $out (Join-Path 'tools' $relative)))) {
        throw "Published runtime is incomplete: tools\$relative"
    }
}
Write-Output "FULL_BUNDLE_READY=$out"
