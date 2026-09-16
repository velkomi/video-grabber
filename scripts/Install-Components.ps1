[CmdletBinding()]
param(
    [Parameter()][string]$Destination = (Join-Path $PSScriptRoot '..\tools'),
    [ValidateSet('Prepare')][string]$Phase = 'Prepare',
    [string]$AssetManifestPath
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root = [IO.Path]::GetFullPath($Destination)
$generations = Join-Path $root 'generations'
[IO.Directory]::CreateDirectory($generations) | Out-Null
$generationName = 'generation-' + (Get-Date -Format 'yyyyMMddHHmmss') + '-' + [guid]::NewGuid().ToString('N')
$generation = Join-Path $generations $generationName
$temp = Join-Path ([IO.Path]::GetTempPath()) ('VideoGrabber-components-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($generation) | Out-Null
[IO.Directory]::CreateDirectory($temp) | Out-Null
$required = @('yt-dlp.exe','ffmpeg.exe','ffprobe.exe','deno.exe')

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Copy-VerifiedLocalAssets([string]$ManifestPath) {
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw 'Asset manifest not found.' }
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $null -eq $manifest.assets) { throw 'Invalid asset manifest.' }
    foreach ($name in $required) {
        $asset = @($manifest.assets) | Where-Object { $_.name -eq $name } | Select-Object -First 1
        if ($null -eq $asset -or [string]::IsNullOrWhiteSpace([string]$asset.path) -or
            [string]::IsNullOrWhiteSpace([string]$asset.sha256)) { throw "Missing trusted asset: $name" }
        $source = [IO.Path]::GetFullPath([string]$asset.path)
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Asset file missing: $name" }
        $actual = Get-Sha256 $source
        if ($actual -ne ([string]$asset.sha256).ToLowerInvariant()) { throw "Digest mismatch: $name" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $generation $name)
    }
}

function Get-ReleaseAsset([string]$Repository, [string]$AssetPattern) {
    $headers = @{ Accept='application/vnd.github+json'; 'User-Agent'='VideoGrabber-Installer' }
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    $asset = @($release.assets) | Where-Object { $_.name -match $AssetPattern } | Select-Object -First 1
    if ($null -eq $asset) { throw "Release asset not found: $Repository / $AssetPattern" }
    if ([string]::IsNullOrWhiteSpace([string]$asset.digest) -or $asset.digest -notmatch '^sha256:(.+)$') {
        throw "Trusted SHA-256 digest unavailable for $($asset.name)."
    }
    return $asset
}

function Save-ReleaseAsset($Asset, [string]$Path) {
    Invoke-WebRequest -Uri $Asset.browser_download_url -OutFile $Path -Headers @{ 'User-Agent'='VideoGrabber-Installer' }
    $expected = ([string]$Asset.digest).Substring(7).ToLowerInvariant()
    if ((Get-Sha256 $Path) -ne $expected) { throw "Digest mismatch: $($Asset.name)" }
}

function Prepare-RemoteAssets {
    $yt = Get-ReleaseAsset 'yt-dlp/yt-dlp' '^yt-dlp\.exe$'
    $ytPath = Join-Path $temp 'yt-dlp.exe'
    Save-ReleaseAsset $yt $ytPath
    Copy-Item -LiteralPath $ytPath -Destination (Join-Path $generation 'yt-dlp.exe')

    $ff = Get-ReleaseAsset 'BtbN/FFmpeg-Builds' '^ffmpeg-master-latest-win64-gpl\.zip$'
    $ffArchive = Join-Path $temp 'ffmpeg.zip'
    $ffFolder = Join-Path $temp 'ffmpeg'
    Save-ReleaseAsset $ff $ffArchive
    Expand-Archive -LiteralPath $ffArchive -DestinationPath $ffFolder
    foreach ($name in @('ffmpeg.exe','ffprobe.exe')) {
        $tool = Get-ChildItem -LiteralPath $ffFolder -Recurse -File -Filter $name | Select-Object -First 1
        if ($null -eq $tool) { throw "FFmpeg archive missing $name" }
        Copy-Item -LiteralPath $tool.FullName -Destination (Join-Path $generation $name)
    }

    $deno = Get-ReleaseAsset 'denoland/deno' '^deno-x86_64-pc-windows-msvc\.zip$'
    $denoArchive = Join-Path $temp 'deno.zip'
    $denoFolder = Join-Path $temp 'deno'
    Save-ReleaseAsset $deno $denoArchive
    Expand-Archive -LiteralPath $denoArchive -DestinationPath $denoFolder
    $denoExe = Get-ChildItem -LiteralPath $denoFolder -Recurse -File -Filter 'deno.exe' | Select-Object -First 1
    if ($null -eq $denoExe) { throw 'Deno archive missing deno.exe' }
    Copy-Item -LiteralPath $denoExe.FullName -Destination (Join-Path $generation 'deno.exe')
}

function Write-PreparedManifest {
    $hashes = [ordered]@{}
    foreach ($name in $required) {
        $path = Join-Path $generation $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Prepared generation incomplete: $name" }
        $hashes[$name] = Get-Sha256 $path
    }
    $prepared = [ordered]@{
        schemaVersion = 1
        generation = ('generations/' + $generationName)
        sha256 = $hashes
    }
    $preparedPath = Join-Path $root 'components-prepared.json'
    $preparedTemp = $preparedPath + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    $prepared | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $preparedTemp -Encoding UTF8
    Move-Item -LiteralPath $preparedTemp -Destination $preparedPath -Force
}

try {
    if ($Phase -ne 'Prepare') { throw 'Only Prepare phase is supported by the child installer.' }
    if ([string]::IsNullOrWhiteSpace($AssetManifestPath)) { Prepare-RemoteAssets }
    else { Copy-VerifiedLocalAssets ([IO.Path]::GetFullPath($AssetManifestPath)) }
    Write-PreparedManifest
    Write-Output ("Prepared component generation: " + $generationName)
}
catch {
    if (Test-Path -LiteralPath $generation) {
        Remove-Item -LiteralPath $generation -Recurse -Force -ErrorAction SilentlyContinue
    }
    throw
}
finally {
    $safeTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolvedTemp = [IO.Path]::GetFullPath($temp)
    if ($resolvedTemp.StartsWith($safeTempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemp).StartsWith('VideoGrabber-components-', [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
