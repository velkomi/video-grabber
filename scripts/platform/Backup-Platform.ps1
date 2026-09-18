param(
  [Parameter(Mandatory=$true)][string]$EnvironmentName,
  [Parameter(Mandatory=$true)][string]$BackupRoot,
  [Parameter(Mandatory=$true)][string]$EvidenceDirectory
)

$ErrorActionPreference='Stop'

if($EnvironmentName -notmatch '^vg-stage-[a-z0-9-]{1,32}$'){
  throw 'Only vg-stage-* environments may be backed up by this script.'
}
if([string]::IsNullOrWhiteSpace($env:VG_BACKUP_DSN_FILE) -or -not (Test-Path $env:VG_BACKUP_DSN_FILE)){
  throw 'VG_BACKUP_DSN_FILE is required.'
}
if([string]::IsNullOrWhiteSpace($env:VG_RESTIC_PASSWORD_FILE) -or -not (Test-Path $env:VG_RESTIC_PASSWORD_FILE)){
  throw 'VG_RESTIC_PASSWORD_FILE is required.'
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
  if(-not $db){throw 'Database missing in DSN.'}
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

$backup=[IO.Path]::GetFullPath($BackupRoot)
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$staging=Join-Path $backup 'staging'
$repository=Join-Path $backup 'repository'
New-Item -ItemType Directory -Force $backup,$staging,$repository,$evidence | Out-Null

$dsn=Parse-Dsn (Get-Content $env:VG_BACKUP_DSN_FILE -Raw)
$containerHost=if($dsn.Host -in @('127.0.0.1','localhost','::1')){'host.docker.internal'}else{$dsn.Host}
$stamp=[DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$dumpName="$EnvironmentName-$stamp.dump"
$dumpPath=Join-Path $staging $dumpName
$wslStaging=Convert-ToWslPath $staging

$postgres='postgres@sha256:051f7b7b3abdd564d5d1bd1e8c4b9c1b6e77087d1dd22020ede611c096a272e0'
$dumpArgs=@(
  'docker','run','--rm',
  '--add-host','host.docker.internal:host-gateway',
  '-v',($wslStaging+':/backup'),
  $postgres,
  'pg_dump',
  '-h',$containerHost,
  '-p',$dsn.Port,
  '-U',$dsn.User,
  '-d',$dsn.Database,
  '-Fc',
  '-f',('/backup/'+$dumpName)
)
wsl.exe @dumpArgs
if($LASTEXITCODE -ne 0){throw 'pg_dump failed.'}
if(-not (Test-Path $dumpPath)){throw 'pg_dump produced no file.'}

$wslBackup=Convert-ToWslPath $backup
$wslPassword=Convert-ToWslPath $env:VG_RESTIC_PASSWORD_FILE
$restic='restic/restic@sha256:4cf4a61ef9786f4de53e9de8c8f5c040f33830eb0a10bf3d614410ee2fcb6120'
$resticCommon=@(
  'docker','run','--rm',
  '-v',($wslBackup+':/backup'),
  '-v',($wslPassword+':/run/secrets/restic_password:ro'),
  '-e','RESTIC_PASSWORD_FILE=/run/secrets/restic_password',
  $restic,
  '-r','/backup/repository'
)

wsl.exe @($resticCommon + @('snapshots')) | Out-Null
if($LASTEXITCODE -ne 0){
  wsl.exe @($resticCommon + @('init')) | Out-Null
  if($LASTEXITCODE -ne 0){throw 'restic init failed.'}
}

wsl.exe @($resticCommon + @(
  'backup',
  ('/backup/staging/'+$dumpName),
  '--tag',$EnvironmentName,
  '--tag','videograbber-platform'
)) | Out-Null
if($LASTEXITCODE -ne 0){throw 'restic backup failed.'}

wsl.exe @($resticCommon + @('check')) | Out-Null
if($LASTEXITCODE -ne 0){throw 'restic check failed.'}

$list=Join-Path $evidence 'pg_restore-list.txt'
$wslDump=Convert-ToWslPath $dumpPath
$listArgs=@(
  'docker','run','--rm',
  '-v',($wslDump+':/backup.dump:ro'),
  $postgres,
  'pg_restore','--list','/backup.dump'
)
wsl.exe @listArgs | Set-Content -Encoding utf8 $list
if($LASTEXITCODE -ne 0){throw 'pg_restore --list failed.'}

$manifest=[ordered]@{
  environment=$EnvironmentName
  backupStartedUtc=$stamp
  completedUtc=[DateTimeOffset]::UtcNow.ToString('O')
  database=$dsn.Database
  dumpFile=$dumpName
  dumpSha256=(Get-FileHash $dumpPath -Algorithm SHA256).Hash.ToLowerInvariant()
  dumpBytes=(Get-Item $dumpPath).Length
  resticRepository='repository'
  encryption='restic'
  sourceCommit=(git -C (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path rev-parse HEAD).Trim()
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $evidence 'backup-manifest.json')
Write-Host $dumpPath
