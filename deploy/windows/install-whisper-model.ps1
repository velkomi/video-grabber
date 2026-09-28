param(
  [Parameter(Mandatory=$true)]
  [ValidateSet('small','medium')]
  [string]$Profile
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$catalog = @{
  small = @{
    File = 'ggml-small-q5_1.bin'
    Url = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin'
    Bytes = 190085487
    Sha256 = 'ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb'
  }
  medium = @{
    File = 'ggml-medium-q5_0.bin'
    Url = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium-q5_0.bin'
    Bytes = 539212467
    Sha256 = '19fea4b380c3a618ec4723c3eef2eb785ffba0d0538cf43f8f235e7b3b34220f'
  }
}

$item = $catalog[$Profile]
$root = Join-Path $env:LOCALAPPDATA 'VideoGrabber\models\whisper'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$target = Join-Path $root $item.File
$marker = $target + '.verified.sha256'
$tmp = $target + '.download'

function Test-Verified {
  if (-not (Test-Path -LiteralPath $target)) { return $false }
  $file = Get-Item -LiteralPath $target
  if ($file.Length -ne [int64]$item.Bytes) { return $false }
  if (Test-Path -LiteralPath $marker) {
    $known = (Get-Content -LiteralPath $marker -Raw).Trim().ToLowerInvariant()
    if ($known -eq $item.Sha256) { return $true }
  }
  $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($actual -ne $item.Sha256) { return $false }
  [IO.File]::WriteAllText($marker, $item.Sha256, (New-Object Text.UTF8Encoding($false)))
  return $true
}

if (Test-Verified) {
  Write-Host "VideoGrabber model '$Profile' is already verified. Reusing local cache."
  exit 0
}

Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $target) {
  Remove-Item -LiteralPath $target -Force
}
Remove-Item -LiteralPath $marker -Force -ErrorAction SilentlyContinue

Write-Host "Downloading VideoGrabber model '$Profile'..."
Invoke-WebRequest -UseBasicParsing -Uri $item.Url -OutFile $tmp

$file = Get-Item -LiteralPath $tmp
if ($file.Length -ne [int64]$item.Bytes) {
  throw "Model size mismatch: $($file.Length) != $($item.Bytes)"
}
$actual = (Get-FileHash -LiteralPath $tmp -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $item.Sha256) {
  throw "Model checksum mismatch."
}

Move-Item -LiteralPath $tmp -Destination $target -Force
[IO.File]::WriteAllText($marker, $item.Sha256, (New-Object Text.UTF8Encoding($false)))
Write-Host "Model '$Profile' installed and verified."
