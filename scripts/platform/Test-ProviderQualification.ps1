param(
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [Parameter(Mandatory = $true)][ValidateSet('Emulator','Live')][string]$Mode,
    [Parameter(Mandatory = $true)][ValidateSet('google','apple','yandex','telegram','email')][string]$Provider
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$configPath = Join-Path $repoRoot 'deploy\platform\auth-partitions.example.json'
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$partition = $config.BrokerPartitions.$Provider
if (-not $partition) { throw "Provider configuration is missing: $Provider" }

$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $issuerBytes = [Text.Encoding]::UTF8.GetBytes([string]$partition.Issuer)
    $issuerHash = ([BitConverter]::ToString($sha.ComputeHash($issuerBytes))).Replace("-", "").ToLowerInvariant()
}
finally { $sha.Dispose() }

$record = [ordered]@{
    provider = $Provider
    mode = $Mode
    issuerHash = $issuerHash
    apiVersion = 1
    callbackValidation = 'BLOCKED'
    positiveLogin = 'BLOCKED'
    collision = 'BLOCKED'
    replay = 'BLOCKED'
    expiry = 'BLOCKED'
    status = 'BLOCKED'
}
$outputPath = Join-Path $EvidenceDirectory ("provider-{0}-{1}.json" -f $Provider, $Mode.ToLowerInvariant())

if ($Mode -eq 'Live') {
    $record.blockedReason = 'Live qualification requires an explicitly configured test app and an already-authorized test login. No credentials are collected by this script.'
    $record | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $outputPath
    Write-Host "BLOCKED: live provider qualification requires explicit authorized test evidence."
    exit 2
}

if ([string]::IsNullOrWhiteSpace($env:VG_TEST_POSTGRES_DSN)) {
    $record.blockedReason = 'VG_TEST_POSTGRES_DSN is not configured for the disposable PostgreSQL test target.'
    $record | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $outputPath
    Write-Host 'BLOCKED: disposable PostgreSQL test target is not configured.'
    exit 2
}

$dotnet = $env:VG_DOTNET_EXE
if ([string]::IsNullOrWhiteSpace($dotnet)) {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { $dotnet = $command.Source }
}
if ([string]::IsNullOrWhiteSpace($dotnet) -or -not (Test-Path $dotnet)) {
    $record.blockedReason = 'A .NET SDK executable is required.'
    $record | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $outputPath
    exit 2
}
$testProject = Join-Path $repoRoot 'tests\VideoGrabber.Platform.Tests\VideoGrabber.Platform.Tests.csproj'
$trxName = "provider-{0}-emulator.trx" -f $Provider
$filter = 'FullyQualifiedName~ProviderAssertionTests|FullyQualifiedName~BrokerTokenValidatorTests'
Push-Location $repoRoot
try {
    & $dotnet test $testProject -c Release --filter $filter `
        --logger "trx;LogFileName=$trxName" --results-directory $EvidenceDirectory
    $testExitCode = $LASTEXITCODE
}
finally { Pop-Location }

$record.testExitCode = $testExitCode
$record.coverage = 'Signed assertion, fixed callback, positive roundtrip, collision isolation, replay and expiry controls.'
if ($testExitCode -eq 0) {
    $record.callbackValidation = 'PASS'
    $record.positiveLogin = 'PASS'
    $record.collision = 'PASS'
    $record.replay = 'PASS'
    $record.expiry = 'PASS'
    $record.status = 'PASS'
}
else {
    $record.callbackValidation = 'FAIL'
    $record.positiveLogin = 'FAIL'
    $record.collision = 'FAIL'
    $record.replay = 'FAIL'
    $record.expiry = 'FAIL'
    $record.status = 'FAIL'
}
$record | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $outputPath
Write-Host ("{0}: provider qualification for {1}; evidence: {2}" -f $record.status, $Provider, $outputPath)
exit $testExitCode
