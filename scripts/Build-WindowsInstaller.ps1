param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDir,
    [string]$OutputDir,
    [string]$Version,
    [string]$Makensis = 'makensis'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$installerScript = Join-Path $repositoryRoot 'deploy\windows\videograbber-setup.nsi'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath (Join-Path $repositoryRoot 'VERSION') -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw 'Installer version is required.'
}

$ReleaseDir = [IO.Path]::GetFullPath($ReleaseDir)
if (-not (Test-Path -LiteralPath (Join-Path $ReleaseDir 'VideoGrabber.Managed.exe') -PathType Leaf)) {
    throw 'Managed release directory must contain VideoGrabber.Managed.exe.'
}
if (-not (Test-Path -LiteralPath (Join-Path $ReleaseDir 'Assets\VideoGrabber.ico') -PathType Leaf)) {
    throw 'Managed release directory must contain Assets\VideoGrabber.ico.'
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repositoryRoot "artifacts\installer-$Version"
} else {
    $OutputDir = [IO.Path]::GetFullPath($OutputDir)
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$sourceBytes = [IO.File]::ReadAllBytes($installerScript)
if ($sourceBytes.Length -lt 3 -or $sourceBytes[0] -ne 0xEF -or $sourceBytes[1] -ne 0xBB -or $sourceBytes[2] -ne 0xBF) {
    throw 'videograbber-setup.nsi must be UTF-8 with BOM.'
}

if ($Version -match '^(\d+)\.(\d+)\.(\d+)-preview\.(\d+)') {
    $numericVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).$($Matches[4])"
}
elseif ($Version -match '^(\d+)\.(\d+)\.(\d+)(?:$|[-+])') {
    $numericVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
}
else {
    throw "Cannot derive Windows numeric version from '$Version'."
}

$arguments = @(
    '/WX',
    '/INPUTCHARSET', 'UTF8',
    "/DRELEASE_DIR=$ReleaseDir",
    "/DOUTPUT_DIR=$OutputDir",
    "/DAPP_VERSION_TEXT=$Version",
    "/DAPP_VERSION_NUMERIC=$numericVersion",
    $installerScript
)

& $Makensis @arguments
if ($LASTEXITCODE -ne 0) {
    throw "makensis failed with exit code $LASTEXITCODE."
}

$setup = Join-Path $OutputDir 'VideoGrabber-Setup.exe'
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) {
    throw 'VideoGrabber-Setup.exe was not created.'
}
$info = Get-Item -LiteralPath $setup
if ($info.Length -le 0) {
    throw 'VideoGrabber-Setup.exe is empty.'
}
$stream = [IO.File]::OpenRead($setup)
try {
    $first = $stream.ReadByte()
    $second = $stream.ReadByte()
}
finally {
    $stream.Dispose()
}
if ($first -ne 0x4D -or $second -ne 0x5A) {
    throw 'VideoGrabber-Setup.exe is not a valid PE executable.'
}
if (-not $info.VersionInfo.ProductVersion.StartsWith($Version, [StringComparison]::Ordinal)) {
    throw "Installer ProductVersion '$($info.VersionInfo.ProductVersion)' does not match '$Version'."
}

[ordered]@{
    setup = $setup
    version = $Version
    numericVersion = $numericVersion
    bytes = $info.Length
    sha256 = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 3
