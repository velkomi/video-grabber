[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceCommit,
    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory,
    [ValidateSet('Local', 'Stage')]
    [string]$Mode = 'Local',
    [string]$DotNet = 'dotnet',
    [string]$ManualEvidencePath,
    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not [System.IO.Path]::IsPathRooted($EvidenceDirectory)) {
    throw 'EvidenceDirectory must be an absolute path.'
}
$EvidenceDirectory = [System.IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$logRoot = Join-Path $EvidenceDirectory 'logs'
$trxRoot = Join-Path $EvidenceDirectory 'trx'
New-Item -ItemType Directory -Force -Path $logRoot, $trxRoot | Out-Null
$env:VIDEOGRABBER_EVIDENCE = $EvidenceDirectory
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result {
    param([string]$Name, [string]$Status, [string]$Detail, [string]$Evidence)
    $results.Add([ordered]@{ name = $Name; status = $Status; detail = $Detail; evidence = $Evidence })
}
function Invoke-ExternalCheck {
    param([string]$Name, [string]$File, [string[]]$Arguments)
    $safe = ($Name -replace '[^A-Za-z0-9_.-]', '_')
    $log = Join-Path $logRoot "$safe.log"
    try {
        & $File @Arguments *> $log
        $code = $LASTEXITCODE
        if ($code -eq 0) {
            Add-Result $Name 'PASS' (($File + ' ' + ($Arguments -join ' ')).Trim()) $log
        } else {
            Add-Result $Name 'FAIL' "exit=$code; $File $($Arguments -join ' ')" $log
        }
    } catch {
        $_ | Out-String | Set-Content -LiteralPath $log -Encoding utf8
        Add-Result $Name 'FAIL' $_.Exception.Message $log
    }
}

$head = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'git rev-parse failed' }
if ($head -eq $SourceCommit) {
    Add-Result 'source_sha' 'PASS' $head $null
} else {
    Add-Result 'source_sha' 'FAIL' "HEAD=$head expected=$SourceCommit" $null
}
$status = (& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'git status failed' }
if ([string]::IsNullOrWhiteSpace(($status -join ''))) {
    Add-Result 'clean_source_snapshot' 'PASS' 'worktree clean' $null
} else {
    Add-Result 'clean_source_snapshot' 'FAIL' ($status -join '; ') $null
}
Invoke-ExternalCheck 'locked_restore' $DotNet @(
    'restore', (Join-Path $root 'VideoGrabber.slnx'), '--locked-mode', '--nologo'
)

$tests = @(
    'tests\VideoGrabber.Core.Tests\VideoGrabber.Core.Tests.csproj',
    'tests\VideoGrabber.Infrastructure.Tests\VideoGrabber.Infrastructure.Tests.csproj',
    'tests\VideoGrabber.Platform.Tests\VideoGrabber.Platform.Tests.csproj',
    'tests\VideoGrabber.Platform.Worker.Tests\VideoGrabber.Platform.Worker.Tests.csproj'
)
foreach ($project in $tests) {
    $name = 'test_' + ([IO.Path]::GetFileNameWithoutExtension($project))
    Invoke-ExternalCheck $name $DotNet @(
        'test', (Join-Path $root $project), '-c', 'Release', '--no-restore', '--nologo',
        '--logger', "trx;LogFileName=$name.trx", '--results-directory', $trxRoot
    )
}

$skipped = 0
foreach ($trx in Get-ChildItem -LiteralPath $trxRoot -Filter '*.trx' -File -ErrorAction SilentlyContinue) {
    $raw = Get-Content -LiteralPath $trx.FullName -Raw
    if ($raw -match 'notExecuted="(?<count>\d+)"') {
        $skipped += [int]$Matches['count']
    }
}
if ($skipped -eq 0) {
    Add-Result 'test_skips' 'PASS' 'no skipped tests in release TRX' $trxRoot
} else {
    Add-Result 'test_skips' 'BLOCKED' "skipped=$skipped; mandatory fixtures/live tools are incomplete" $trxRoot
}

Invoke-ExternalCheck 'build_app_local' $DotNet @(
    'build', (Join-Path $root 'src\VideoGrabber.App\VideoGrabber.App.csproj'),
    '-c', 'Release', '--no-restore', '--nologo', '-p:VideoGrabberEdition=Local'
)
Invoke-ExternalCheck 'build_app_managed' $DotNet @(
    'build', (Join-Path $root 'src\VideoGrabber.App\VideoGrabber.App.csproj'),
    '-c', 'Release', '--no-restore', '--nologo', '-p:VideoGrabberEdition=Managed'
)
Invoke-ExternalCheck 'build_platform_api' $DotNet @(
    'build', (Join-Path $root 'src\VideoGrabber.Platform.Api\VideoGrabber.Platform.Api.csproj'),
    '-c', 'Release', '--no-restore', '--nologo'
)
Invoke-ExternalCheck 'build_platform_worker' $DotNet @(
    'build', (Join-Path $root 'src\VideoGrabber.Platform.Worker\VideoGrabber.Platform.Worker.csproj'),
    '-c', 'Release', '--no-restore', '--nologo'
)

try {
    Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -Filter '*.ps1' -Recurse |
        ForEach-Object { [void][ScriptBlock]::Create((Get-Content -LiteralPath $_.FullName -Raw)) }
    Add-Result 'powershell_syntax' 'PASS' 'all scripts parsed' $null
} catch {
    Add-Result 'powershell_syntax' 'FAIL' $_.Exception.Message $null
}

try {
    Get-ChildItem -LiteralPath (Join-Path $root 'deploy') -Filter '*.json' -Recurse |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json | Out-Null }
    Get-ChildItem -LiteralPath $root -Filter '*.csproj' -Recurse |
        ForEach-Object { [xml](Get-Content -LiteralPath $_.FullName -Raw) | Out-Null }
    Add-Result 'json_xml_syntax' 'PASS' 'deployment JSON and project XML parsed' $null
} catch {
    Add-Result 'json_xml_syntax' 'FAIL' $_.Exception.Message $null
}
$node = Get-Command node -ErrorAction SilentlyContinue
if ($null -eq $node) {
    Add-Result 'miniapp_javascript_syntax' 'BLOCKED' 'node is unavailable' $null
} else {
    $scripts = Get-ChildItem -LiteralPath (Join-Path $root 'src\VideoGrabber.Platform.Api\wwwroot\miniapp') -Filter '*.js' -File
    foreach ($script in $scripts) {
        Invoke-ExternalCheck ('node_check_' + $script.BaseName) $node.Source @('--check', $script.FullName)
    }
}

$packageRoot = Join-Path $EvidenceDirectory 'packages'
foreach ($target in @('Local', 'Managed', 'Api', 'Worker')) {
    $name = 'package_' + $target.ToLowerInvariant()
    $args = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', (Join-Path $root 'scripts\Build-Release.ps1'),
        '-Target', $target,
        '-DotNet', $DotNet,
        '-SourceCommit', $SourceCommit,
        '-ReleaseRoot', $packageRoot,
        '-SkipTests'
    )
    Invoke-ExternalCheck $name 'powershell.exe' $args
}

$manifestFiles = Get-ChildItem -LiteralPath $packageRoot -Filter 'manifest.json' -Recurse -ErrorAction SilentlyContinue
if ($manifestFiles.Count -eq 4) {
    $bad = @($manifestFiles | Where-Object {
        (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).sourceCommit -ne $SourceCommit
    })
    if ($bad.Count -eq 0) {
        Add-Result 'package_manifests' 'PASS' 'four manifests match exact source SHA' $packageRoot
    } else {
        Add-Result 'package_manifests' 'FAIL' 'one or more manifests have wrong source SHA' $packageRoot
    }
} else {
    Add-Result 'package_manifests' 'FAIL' "expected=4 actual=$($manifestFiles.Count)" $packageRoot
}
$requiredManual = @(
    'cleanWindowsLocalManaged',
    'authorizedCourseDownload',
    'telegramMiniApp',
    'providerCollisionFlows',
    'sandboxPaymentsRecurringRefunds'
)
if ($Mode -eq 'Stage') {
    $requiredManual += @('stageDeployment', 'recoveryDrill', 'alertDelivery', 'capacityBaseline')
}if ([string]::IsNullOrWhiteSpace($ManualEvidencePath) -or -not (Test-Path -LiteralPath $ManualEvidencePath)) {
    Add-Result 'manual_acceptance' 'BLOCKED' ('missing evidence for: ' + ($requiredManual -join ', ')) $ManualEvidencePath
} else {
    try {
        $manual = Get-Content -LiteralPath $ManualEvidencePath -Raw | ConvertFrom-Json
        $missing = @()
        foreach ($key in $requiredManual) {
            if ($manual.PSObject.Properties.Name -notcontains $key -or $manual.$key -ne $true) {
                $missing += $key
            }
        }
        if ($missing.Count -eq 0) {
            Add-Result 'manual_acceptance' 'PASS' 'all required live checks explicitly confirmed' $ManualEvidencePath
        } else {
            Add-Result 'manual_acceptance' 'BLOCKED' ('not confirmed: ' + ($missing -join ', ')) $ManualEvidencePath
        }
    } catch {
        Add-Result 'manual_acceptance' 'FAIL' $_.Exception.Message $ManualEvidencePath
    }
}

$ready = @($results | Where-Object { $_.status -ne 'PASS' }).Count -eq 0
$map = [ordered]@{
    schema = 1
    sourceCommit = $SourceCommit
    mode = $Mode
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    overall = if ($ready) { 'READY' } else { 'NOT_READY' }
    checks = $results
}
$out = Join-Path $EvidenceDirectory 'release-readiness.json'
$map | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $out -Encoding utf8
$map | ConvertTo-Json -Depth 8
if (-not $ready -and -not $ReportOnly) { exit 1 }