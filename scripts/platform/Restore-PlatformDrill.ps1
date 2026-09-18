param(
  [Parameter(Mandatory=$true)][string]$SnapshotId,
  [Parameter(Mandatory=$true)][string]$TargetDatabase,
  [Parameter(Mandatory=$true)][string]$EvidenceDirectory
)

$ErrorActionPreference='Stop'

if($TargetDatabase -notmatch '^vg_test_restore_[a-z0-9_]{1,40}$'){
  throw 'Restore target must match vg_test_restore_*.'
}
foreach($name in 'VG_RESTORE_ADMIN_DSN_FILE','VG_RESTIC_PASSWORD_FILE','VG_BACKUP_ROOT'){
  $value=[Environment]::GetEnvironmentVariable($name)
  if([string]::IsNullOrWhiteSpace($value)){throw "$name is required."}
}
if(-not (Test-Path $env:VG_RESTORE_ADMIN_DSN_FILE)){
  throw 'Restore admin DSN file not found.'
}
if(-not (Test-Path $env:VG_RESTIC_PASSWORD_FILE)){
  throw 'Restic password file not found.'
}

function Parse-Dsn([string]$text){
  $map=@{}
  foreach($item in $text.Split(';',[System.StringSplitOptions]::RemoveEmptyEntries)){
    $pair=$item.Split('=',2)
    if($pair.Count -eq 2){
      $map[$pair[0].Trim().ToLowerInvariant()]=$pair[1].Trim()
    }
  }
  $serverHost=$map['host']
  if(-not $serverHost){$serverHost='127.0.0.1'}
  $port=$map['port']
  if(-not $port){$port='5432'}
  $db=$map['database']
  if(-not $db){$db='postgres'}
  $user=$map['username']
  if(-not $user){$user=$map['user id']}
  if(-not $user){throw 'Username missing in DSN.'}
  return [ordered]@{
    Host=$serverHost
    Port=$port
    Database=$db
    User=$user
  }
}

function Convert-ToWslPath([string]$path){
  $full=[IO.Path]::GetFullPath($path)
  if($full -notmatch '^([A-Za-z]):\\(.*)$'){
    throw 'Only drive-letter Windows paths are supported for WSL mounts.'
  }
  $drive=$Matches[1].ToLowerInvariant()
  $rest=$Matches[2].Replace('\','/')
  return "/mnt/$drive/$rest"
}

$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$backup=[IO.Path]::GetFullPath($env:VG_BACKUP_ROOT)
$restore=Join-Path $evidence 'restored-files'
New-Item -ItemType Directory -Force $evidence,$restore | Out-Null

$wslBackup=Convert-ToWslPath $backup
$wslRestore=Convert-ToWslPath $restore
$wslPassword=Convert-ToWslPath $env:VG_RESTIC_PASSWORD_FILE
$restic='restic/restic@sha256:4cf4a61ef9786f4de53e9de8c8f5c040f33830eb0a10bf3d614410ee2fcb6120'
$resticArgs=@(
  'docker','run','--rm',
  '-v',($wslBackup+':/backup:ro'),
  '-v',($wslRestore+':/restore'),
  '-v',($wslPassword+':/run/secrets/restic_password:ro'),
  '-e','RESTIC_PASSWORD_FILE=/run/secrets/restic_password',
  $restic,
  '-r','/backup/repository',
  '--no-lock',
  'restore',$SnapshotId,
  '--target','/restore'
)
wsl.exe @resticArgs | Out-Null
if($LASTEXITCODE -ne 0){throw 'restic restore failed.'}

$dump=Get-ChildItem $restore -Recurse -Filter '*.dump' -File |
  Sort-Object LastWriteTimeUtc -Descending |
  Select-Object -First 1
if(-not $dump){throw 'No PostgreSQL dump found in restored snapshot.'}

$dsn=Parse-Dsn (Get-Content $env:VG_RESTORE_ADMIN_DSN_FILE -Raw)
$containerHost=if($dsn.Host -in @('127.0.0.1','localhost','::1')){'host.docker.internal'}else{$dsn.Host}
$postgres='postgres@sha256:051f7b7b3abdd564d5d1bd1e8c4b9c1b6e77087d1dd22020ede611c096a272e0'
$dockerBase=@(
  'docker','run','--rm',
  '--add-host','host.docker.internal:host-gateway',
  $postgres
)

wsl.exe @($dockerBase + @(
  'dropdb',
  '-h',$containerHost,
  '-p',$dsn.Port,
  '-U',$dsn.User,
  '--if-exists',
  $TargetDatabase
))
if($LASTEXITCODE -ne 0){throw 'dropdb failed.'}

wsl.exe @($dockerBase + @(
  'createdb',
  '-h',$containerHost,
  '-p',$dsn.Port,
  '-U',$dsn.User,
  $TargetDatabase
))
if($LASTEXITCODE -ne 0){throw 'createdb failed.'}

$wslDump=Convert-ToWslPath $dump.FullName
$restoreArgs=@(
  'docker','run','--rm',
  '--add-host','host.docker.internal:host-gateway',
  '-v',($wslDump+':/restore.dump:ro'),
  $postgres,
  'pg_restore',
  '-h',$containerHost,
  '-p',$dsn.Port,
  '-U',$dsn.User,
  '-d',$TargetDatabase,
  '--no-owner',
  '--no-privileges',
  '/restore.dump'
)
wsl.exe @restoreArgs
if($LASTEXITCODE -ne 0){throw 'pg_restore failed.'}

$targetDsn="Host=$($dsn.Host);Port=$($dsn.Port);Database=$TargetDatabase;Username=$($dsn.User);Pooling=false"
$env:VG_PLATFORM_CONSISTENCY_DSN=$targetDsn
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dll=Join-Path $repo 'src\VideoGrabber.Platform.Api\bin\Release\net10.0\VideoGrabber.Platform.Api.dll'
$report=& dotnet $dll --consistency-report
$consistencyExit=$LASTEXITCODE
$report | Set-Content -Encoding utf8 (Join-Path $evidence 'consistency-report.json')
if($consistencyExit -ne 0){throw 'Restored database consistency check failed.'}

[ordered]@{
  snapshot=$SnapshotId
  targetDatabase=$TargetDatabase
  restoredUtc=[DateTimeOffset]::UtcNow.ToString('O')
  dumpSha256=(Get-FileHash $dump.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
  consistency='PASS'
} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $evidence 'restore-drill.json')

Write-Host 'PASS'
