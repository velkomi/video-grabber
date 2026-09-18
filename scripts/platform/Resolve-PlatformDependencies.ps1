param(
  [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
  [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceCommit
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $evidence | Out-Null
if((git -C $root rev-parse $SourceCommit) -ne $SourceCommit){ throw 'SourceCommit is not present in this repository.' }

$images=[ordered]@{
  dotnetSdk='mcr.microsoft.com/dotnet/sdk:10.0.400'
  dotnetAspNet='mcr.microsoft.com/dotnet/aspnet:10.0'
  dotnetRuntime='mcr.microsoft.com/dotnet/runtime:10.0'
  postgres='postgres:17-bookworm'
  caddy='caddy:2.10.0-alpine'
  restic='restic/restic:0.18.0'
  optionalBotApi='aiogram/telegram-bot-api:latest'
}
$resolved=[ordered]@{}
foreach($name in $images.Keys){
  $tag=$images[$name]
  wsl.exe docker pull $tag | Out-Null
  if($LASTEXITCODE -ne 0){ throw "docker pull failed: $tag" }
  $raw=(wsl.exe docker image inspect $tag --format '{{json .RepoDigests}}' | Where-Object {$_ -match '^\['} | Select-Object -Last 1)
  $refs=$raw | ConvertFrom-Json
  if(-not $refs -or $refs.Count -ne 1 -or $refs[0] -notmatch '@sha256:[0-9a-f]{64}$'){ throw "No unique repo digest for $tag" }
  $resolved[$name]=$refs[0]
}

$lockFiles=Get-ChildItem $root -Recurse -Filter packages.lock.json -File |
  Where-Object {$_.FullName -notmatch '\\(bin|obj)\\'} |
  Sort-Object FullName
$lockHashes=@()
foreach($file in $lockFiles){
  $lockHashes += [ordered]@{
    path=$file.FullName.Substring($root.Length+1).Replace('\','/')
    sha256=(Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
  }
}
$advisoryFile=Join-Path $evidence 'nuget-vulnerable.txt'
& dotnet list (Join-Path $root 'VideoGrabber.slnx') package --vulnerable --include-transitive *> $advisoryFile
$advisoryExit=$LASTEXITCODE

$result=[ordered]@{
  schemaVersion=1
  generatedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
  sourceCommit=$SourceCommit
  images=$resolved
  packageLocks=$lockHashes
  nugetAdvisoryCommandExit=$advisoryExit
  applicationImages=[ordered]@{
    api=$null
    worker=$null
    status='BLOCKED_UNTIL_AUTHORIZED_REGISTRY_DIGESTS_EXIST'
  }
  mediaTools=[ordered]@{
    ytDlpVersion='2026.09.16.232951'
    ytDlpSha256='f8ca14db511702a5dbfc5a527056312907ddd0914d0b4036f108d6849e17ef61'
  }
}
$out=Join-Path $evidence 'dependency-lock.resolved.json'
$result | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 $out
if($advisoryExit -ne 0){ throw 'NuGet vulnerability query failed; see evidence.' }
Write-Host $out
