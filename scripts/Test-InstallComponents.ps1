[CmdletBinding()]
param([string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path ([IO.Path]::GetTempPath()) ('VG-components-test-' + [guid]::NewGuid().ToString('N'))
}
$root = Join-Path $EvidenceDirectory ('install-' + [guid]::NewGuid().ToString('N'))
$tools = $env:VIDEOGRABBER_INTEGRATION_TOOLS
if ([string]::IsNullOrWhiteSpace($tools)) { $tools = 'D:\CODEX\Portable\VideoGrabber-tools' }
$required = @('yt-dlp.exe','ffmpeg.exe','ffprobe.exe','deno.exe')
New-Item -ItemType Directory -Force $root | Out-Null
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $tools $name) -PathType Leaf)) { throw "Missing fixture tool: $name" }
}
$pointerPath = Join-Path $root 'components-current.json'
$pointerSentinel = '{"fixture":"must-remain-unchanged"}'
Set-Content -LiteralPath $pointerPath -Value $pointerSentinel -NoNewline

function New-Manifest([string]$Path, [string]$BadName = '', [string]$MissingName = '') {
    $assets = @()
    foreach ($name in $required) {
        if ($name -eq $MissingName) { continue }
        $source = Join-Path $tools $name
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($name -eq $BadName) { $hash = ('0' * 64) }
        $assets += [ordered]@{name=$name;path=$source;sha256=$hash}
    }
    @{schemaVersion=1;assets=$assets} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path -Encoding UTF8
}

$manifest = Join-Path $root 'assets.json'
New-Manifest $manifest
& (Join-Path $PSScriptRoot 'Install-Components.ps1') -Phase Prepare -Destination $root -AssetManifestPath $manifest
$preparedPath = Join-Path $root 'components-prepared.json'
if (-not (Test-Path -LiteralPath $preparedPath -PathType Leaf)) { throw 'Prepare manifest missing' }
$prepared = Get-Content -LiteralPath $preparedPath -Raw | ConvertFrom-Json
$generation = [IO.Path]::GetFullPath((Join-Path $root ([string]$prepared.generation)))
foreach ($name in $required) {
    $path = Join-Path $generation $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Prepared generation missing $name" }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne [string]$prepared.sha256.$name) { throw "Prepared hash mismatch: $name" }
}
if ((Get-Content -LiteralPath $pointerPath -Raw) -ne $pointerSentinel) { throw 'Prepare mutated live pointer' }
$generationCount = @(Get-ChildItem -LiteralPath (Join-Path $root 'generations') -Directory).Count

$bad = Join-Path $root 'bad-digest.json'
New-Manifest $bad 'ffmpeg.exe'
$failed = $false
try { & (Join-Path $PSScriptRoot 'Install-Components.ps1') -Phase Prepare -Destination $root -AssetManifestPath $bad }
catch { $failed = $true }
if (-not $failed) { throw 'Wrong digest did not fail' }
if ((Get-Content -LiteralPath $pointerPath -Raw) -ne $pointerSentinel) { throw 'Digest failure mutated live pointer' }
if (@(Get-ChildItem -LiteralPath (Join-Path $root 'generations') -Directory).Count -ne $generationCount) {
    throw 'Failed digest left a staged generation'
}

$missing = Join-Path $root 'missing.json'
New-Manifest $missing '' 'ffprobe.exe'
$failed = $false
try { & (Join-Path $PSScriptRoot 'Install-Components.ps1') -Phase Prepare -Destination $root -AssetManifestPath $missing }
catch { $failed = $true }
if (-not $failed) { throw 'Missing ffprobe did not fail' }
if ((Get-Content -LiteralPath $pointerPath -Raw) -ne $pointerSentinel) { throw 'Missing asset mutated live pointer' }
if (@(Get-ChildItem -LiteralPath (Join-Path $root 'generations') -Directory).Count -ne $generationCount) {
    throw 'Missing asset left a staged generation'
}

$lockedSource = Join-Path $root 'locked-ffprobe.exe'
Copy-Item -LiteralPath (Join-Path $tools 'ffprobe.exe') -Destination $lockedSource
$lockedManifest = Join-Path $root 'locked-copy.json'
$assets = @()
foreach ($name in $required) {
    $source = if ($name -eq 'ffprobe.exe') { $lockedSource } else { Join-Path $tools $name }
    $assets += [ordered]@{
        name = $name
        path = $source
        sha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
@{schemaVersion=1;assets=$assets} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $lockedManifest -Encoding UTF8
$held = [IO.File]::Open($lockedSource,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
try {
    $failed = $false
    try { & (Join-Path $PSScriptRoot 'Install-Components.ps1') -Phase Prepare -Destination $root -AssetManifestPath $lockedManifest }
    catch { $failed = $true }
    if (-not $failed) { throw 'Exclusive source lock did not fail staging copy' }
} finally { $held.Dispose() }
if ((Get-Content -LiteralPath $pointerPath -Raw) -ne $pointerSentinel) { throw 'Copy failure mutated live pointer' }
if (@(Get-ChildItem -LiteralPath (Join-Path $root 'generations') -Directory).Count -ne $generationCount) {
    throw 'Copy failure left a staged generation'
}
foreach ($name in @('Install-Components.ps1','Test-InstallComponents.ps1')) {
    $tokens=$null; $errors=$null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $name),[ref]$tokens,[ref]$errors)
    if ($errors.Count) { throw ($name + ': invalid PowerShell syntax') }
}
'PASS: local trusted prepare; wrong digest; missing asset; exclusive-copy failure; pointer unchanged; no failed generation leak.'
('Evidence: ' + $root)
'PASS' | Set-Content -LiteralPath (Join-Path $root 'RESULT.txt')
