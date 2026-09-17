param(
  [Parameter(Mandatory=$true)][Guid]$AccountId,
  [Parameter(Mandatory=$true)][ValidateSet('development','staging','production')][string]$EnvironmentName,
  [Parameter(Mandatory=$true)][string]$AuthorizationFile
)
$ErrorActionPreference='Stop'
if (-not (Test-Path -LiteralPath $AuthorizationFile -PathType Leaf)) { throw 'Authorization file not found.' }
$authorization = Get-Content -LiteralPath $AuthorizationFile -Raw | ConvertFrom-Json
if ($authorization.approved -ne $true) { throw 'Provisioning authorization is not approved.' }
if ([string]$authorization.accountId -ne $AccountId.ToString('D')) { throw 'Authorization account mismatch.' }
if ([string]$authorization.environment -ne $EnvironmentName) { throw 'Authorization environment mismatch.' }
$issued = [DateTimeOffset]::Parse([string]$authorization.issuedAt)
$expires = [DateTimeOffset]::Parse([string]$authorization.expiresAt)
$now = [DateTimeOffset]::UtcNow
if ($issued -gt $now.AddSeconds(30) -or $issued -lt $now.AddMinutes(-5) -or $expires -le $now) {
  throw 'Fresh MFA provisioning authorization is required.'
}
$psql = $env:VG_PLATFORM_PSQL
$service = $env:VG_PLATFORM_PSQL_SERVICE
if ([string]::IsNullOrWhiteSpace($psql) -or -not (Test-Path -LiteralPath $psql -PathType Leaf)) { throw 'VG_PLATFORM_PSQL must point to psql.' }
if ([string]::IsNullOrWhiteSpace($service)) { throw 'VG_PLATFORM_PSQL_SERVICE is required.' }
$sql = @'
\set ON_ERROR_STOP on
begin;
update licensing.accounts
set base_role='owner_admin'
where account_id=:'account_id'::uuid and blocked_at is null;
\if :ROW_COUNT = 0
\echo 'Account was not found or is blocked.'
rollback;
\quit 3
\endif
insert into licensing.audit_events(
  event_id,account_id,actor_account_id,event_type,details)
values(gen_random_uuid(),:'account_id'::uuid,null,'platform_owner_bootstrap',
  jsonb_build_object('environment',:'environment','authorization','external_mfa'));
commit;
'@
$sql | & $psql "service=$service" -v account_id=$AccountId -v environment=$EnvironmentName
if ($LASTEXITCODE -ne 0) { throw "Owner bootstrap failed with exit code $LASTEXITCODE." }
Write-Host "Owner role assigned to $AccountId in $EnvironmentName with audit evidence."
