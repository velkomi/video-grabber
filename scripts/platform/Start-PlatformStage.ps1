param(
  [Parameter(Mandatory=$true)][string]$EnvironmentName,
  [Parameter(Mandatory=$true)][string]$DependencyLock,
  [Parameter(Mandatory=$true)][string]$ConfigPath,
  [Parameter(Mandatory=$true)][string]$EvidenceDirectory
)
$ErrorActionPreference='Stop'
if($EnvironmentName -notmatch '^vg-stage-[a-z0-9-]{1,32}$'){ throw 'EnvironmentName must match vg-stage-*.' }
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$lock=Get-Content ([IO.Path]::GetFullPath($DependencyLock)) -Raw | ConvertFrom-Json
$config=Get-Content ([IO.Path]::GetFullPath($ConfigPath)) -Raw | ConvertFrom-Json
if($config.environmentName -ne $EnvironmentName){ throw 'Config environmentName mismatch.' }
foreach($field in 'api','worker'){
  $value=$lock.applicationImages.$field
  if([string]::IsNullOrWhiteSpace($value) -or $value -notmatch '@sha256:[0-9a-f]{64}$'){
    throw "Application image $field has no qualified registry digest."
  }
}
$head=(git -C $root rev-parse HEAD).Trim()
if($lock.sourceCommit -ne $head){ throw 'Dependency lock source commit does not equal current HEAD.' }

$apiProject=Join-Path $root 'src\VideoGrabber.Platform.Api\VideoGrabber.Platform.Api.csproj'
dotnet run --project $apiProject --no-launch-profile -- --validate-stage-config ([IO.Path]::GetFullPath($ConfigPath))
if($LASTEXITCODE -ne 0){ throw 'Stage configuration rejected by API validator.' }

$env:VG_ENVIRONMENT_NAME=$EnvironmentName
$env:VG_API_IMAGE=$lock.applicationImages.api
$env:VG_WORKER_IMAGE=$lock.applicationImages.worker
$env:VG_POSTGRES_IMAGE=$lock.images.postgres
$env:VG_CADDY_IMAGE=$lock.images.caddy
$env:VG_BOT_API_IMAGE=$lock.images.optionalBotApi
$env:VG_STAGE_HOST=$config.publicHttpsHost
$env:VG_AUTH_ISSUER=$config.authIssuer
$compose=Join-Path $root 'deploy\platform\compose.staging.yml'
docker compose -f $compose config --quiet
if($LASTEXITCODE -ne 0){ throw 'Docker compose configuration invalid.' }

$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $evidence | Out-Null
$status=[ordered]@{
  environment=$EnvironmentName
  sourceCommit=$head
  validated=$true
  composeValidated=$true
  started=$false
  status='BLOCKED'
  reason='VG_STAGE_START_AUTHORIZED must equal YES'
}
if($env:VG_STAGE_START_AUTHORIZED -eq 'YES'){
  docker compose -f $compose up -d --remove-orphans
  if($LASTEXITCODE -ne 0){ throw 'Stage start failed.' }
  $status.started=$true
  $status.status='PASS'
  $status.reason='isolated stage started'
}
$status | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $evidence 'stage-start.json')
if(-not $status.started){ Write-Host 'BLOCKED: stage start not explicitly authorized.' }
