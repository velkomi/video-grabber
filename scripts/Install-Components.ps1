[CmdletBinding()]
param(
    [Parameter()]
    [string]$Destination = (Join-Path $PSScriptRoot '..\tools')
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$resolvedDestination = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $resolvedDestination -Force | Out-Null
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("VideoGrabber-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

function Get-ReleaseAsset {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$AssetPattern
    )

    $headers = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'VideoGrabber-Installer' }
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    $asset = @($release.assets) | Where-Object { $_.name -match $AssetPattern } | Select-Object -First 1
    if ($null -eq $asset) {
        throw "В последнем релизе $Repository не найден файл $AssetPattern"
    }
    return $asset
}

function Save-VerifiedAsset {
    param(
        [Parameter(Mandatory)]$Asset,
        [Parameter(Mandatory)][string]$Path
    )

    Write-Host "Скачиваю $($Asset.name)…"
    Invoke-WebRequest -Uri $Asset.browser_download_url -OutFile $Path -Headers @{ 'User-Agent' = 'VideoGrabber-Installer' }
    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($Asset.digest -and $Asset.digest -match '^sha256:(.+)$') {
        $expectedHash = $Matches[1].ToLowerInvariant()
        if ($actualHash -ne $expectedHash) {
            throw "Контрольная сумма $($Asset.name) не совпала."
        }
        Write-Host "SHA-256 подтверждён GitHub: $actualHash"
    } else {
        Write-Warning "GitHub API не вернул эталон SHA-256. Полученный SHA-256: $actualHash"
    }
}

try {
    $ytDlpAsset = Get-ReleaseAsset -Repository 'yt-dlp/yt-dlp' -AssetPattern '^yt-dlp\.exe$'
    $ytDlpPath = Join-Path $temporaryDirectory 'yt-dlp.exe'
    Save-VerifiedAsset -Asset $ytDlpAsset -Path $ytDlpPath
    Copy-Item -LiteralPath $ytDlpPath -Destination (Join-Path $resolvedDestination 'yt-dlp.exe') -Force

    $ffmpegAsset = Get-ReleaseAsset -Repository 'BtbN/FFmpeg-Builds' -AssetPattern '^ffmpeg-master-latest-win64-gpl\.zip$'
    $ffmpegArchive = Join-Path $temporaryDirectory 'ffmpeg.zip'
    $ffmpegFolder = Join-Path $temporaryDirectory 'ffmpeg'
    Save-VerifiedAsset -Asset $ffmpegAsset -Path $ffmpegArchive
    Expand-Archive -LiteralPath $ffmpegArchive -DestinationPath $ffmpegFolder

    foreach ($toolName in 'ffmpeg.exe', 'ffprobe.exe') {
        $tool = Get-ChildItem -LiteralPath $ffmpegFolder -Recurse -File -Filter $toolName | Select-Object -First 1
        if ($null -eq $tool) { throw "В архиве FFmpeg отсутствует $toolName" }
        Copy-Item -LiteralPath $tool.FullName -Destination (Join-Path $resolvedDestination $toolName) -Force
    }

    $denoAsset = Get-ReleaseAsset -Repository 'denoland/deno' -AssetPattern '^deno-x86_64-pc-windows-msvc\.zip$'
    $denoArchive = Join-Path $temporaryDirectory 'deno.zip'
    $denoFolder = Join-Path $temporaryDirectory 'deno'
    Save-VerifiedAsset -Asset $denoAsset -Path $denoArchive
    Expand-Archive -LiteralPath $denoArchive -DestinationPath $denoFolder
    $deno = Get-ChildItem -LiteralPath $denoFolder -Recurse -File -Filter 'deno.exe' | Select-Object -First 1
    if ($null -eq $deno) { throw 'В архиве Deno отсутствует deno.exe' }
    Copy-Item -LiteralPath $deno.FullName -Destination (Join-Path $resolvedDestination 'deno.exe') -Force

    Write-Host ''
    Write-Host "Компоненты установлены: $resolvedDestination" -ForegroundColor Green
    Write-Host 'Вернитесь в VideoGrabber и нажмите «Обновить статус».'
}
finally {
    $safeTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $resolvedTemp = [System.IO.Path]::GetFullPath($temporaryDirectory)
    if ($resolvedTemp.StartsWith($safeTempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemp).StartsWith('VideoGrabber-', [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
