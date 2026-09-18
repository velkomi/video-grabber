[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime,
    [string]$DotNet = 'dotnet',
    [ValidateSet('Local', 'Managed')]
    [string]$Edition = 'Local',
    [ValidateSet('Local', 'Managed', 'Api', 'Worker')]
    [string]$Target,
    [string]$Version,
    [string]$SourceCommit,
    [string]$ReleaseRoot,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Target)) { $Target = $Edition }
if ([string]::IsNullOrWhiteSpace($Runtime)) {
    $Runtime = if ($Target -in @('Local', 'Managed')) { 'win-x64' } else { 'portable' }
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath (Join-Path $repositoryRoot 'VERSION') -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) { throw 'Version is required.' }
$head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($head)) { throw 'Cannot resolve source commit.' }
if ([string]::IsNullOrWhiteSpace($SourceCommit)) { $SourceCommit = $head }
if ($SourceCommit -ne $head) { throw "SourceCommit $SourceCommit does not match HEAD $head." }

$project = $null
$artifactName = $null
$selfContained = $false
$extra = @()
switch ($Target) {
    'Local' {
        $project = 'src\VideoGrabber.App\VideoGrabber.App.csproj'
        $artifactName = "VideoGrabber.Local-$Runtime"
        $selfContained = $true
        $extra = @('-p:VideoGrabberEdition=Local')
    }
    'Managed' {
        $project = 'src\VideoGrabber.App\VideoGrabber.App.csproj'
        $artifactName = "VideoGrabber.Managed-$Runtime"
        $selfContained = $true
        $extra = @('-p:VideoGrabberEdition=Managed')
    }
    'Api' {
        $project = 'src\VideoGrabber.Platform.Api\VideoGrabber.Platform.Api.csproj'
        $artifactName = "VideoGrabber.Platform.Api-$Runtime"
    }
    'Worker' {
        $project = 'src\VideoGrabber.Platform.Worker\VideoGrabber.Platform.Worker.csproj'
        $artifactName = "VideoGrabber.Platform.Worker-$Runtime"
    }
}

if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    $ReleaseRoot = Join-Path $repositoryRoot "artifacts\release-$Version"
} else {
    $ReleaseRoot = [System.IO.Path]::GetFullPath($ReleaseRoot)
}
$output = Join-Path $ReleaseRoot $artifactName
if (Test-Path -LiteralPath $output) {
    throw "Release folder already exists: $output"
}
New-Item -ItemType Directory -Force -Path $ReleaseRoot | Out-Null

& $DotNet restore (Join-Path $repositoryRoot 'VideoGrabber.slnx') --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }
if (-not $SkipTests) {
    & $DotNet test (Join-Path $repositoryRoot 'VideoGrabber.slnx') -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed' }
}

$publishArgs = @(
    'publish',
    (Join-Path $repositoryRoot $project),
    '-c', $Configuration,
    '-o', $output,
    '--no-restore',
    "-p:Version=$Version"
)
if ($Runtime -ne 'portable') {
    $publishArgs += @(
        '-r', $Runtime,
        '--self-contained', $selfContained.ToString().ToLowerInvariant()
    )
}
$publishArgs += $extra
& $DotNet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Target" }

foreach ($file in @('README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $file) -Destination $output
}
if ($Target -in @('Local', 'Managed')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\MEDIA_WORKFLOWS.md') -Destination $output
}
$entries = @()
foreach ($file in Get-ChildItem -LiteralPath $output -File -Recurse | Sort-Object FullName) {
    $relative = $file.FullName.Substring($output.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')
    $entries += [ordered]@{
        path = $relative
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        bytes = $file.Length
    }
}
$manifest = [ordered]@{
    schema = 1
    product = 'VideoGrabber'
    target = $Target
    version = $Version
    runtime = $Runtime
    configuration = $Configuration
    sourceCommit = $SourceCommit
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    files = $entries
}
$manifestPath = Join-Path $output 'manifest.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

$shaLines = @()
foreach ($file in Get-ChildItem -LiteralPath $output -File -Recurse | Sort-Object FullName) {
    if ($file.Name -eq 'release-files.sha256') { continue }
    $relative = $file.FullName.Substring($output.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $shaLines += "$hash *$relative"
}
$shaPath = Join-Path $output 'release-files.sha256'
$shaLines | Set-Content -LiteralPath $shaPath -Encoding ascii

$archive = Join-Path $ReleaseRoot "$artifactName.zip"
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -Force
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[ordered]@{
    target = $Target
    sourceCommit = $SourceCommit
    output = $output
    archive = $archive
    archiveSha256 = $archiveHash
    manifest = $manifestPath
    fileHashes = $shaPath
} | ConvertTo-Json -Depth 4
