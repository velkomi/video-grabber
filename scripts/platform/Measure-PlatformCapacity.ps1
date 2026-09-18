param(
  [Parameter(Mandatory=$true)][string]$EnvironmentName,
  [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
  [int]$DurationSeconds=120
)

$ErrorActionPreference='Stop'

if($EnvironmentName -notmatch '^vg-stage-[a-z0-9-]{1,32}$'){
  throw 'EnvironmentName must match vg-stage-*'
}
if(-not [IO.Path]::IsPathRooted($EvidenceDirectory)){
  throw 'EvidenceDirectory must be absolute.'
}
if($DurationSeconds -lt 5 -or $DurationSeconds -gt 600){
  throw 'DurationSeconds must be between 5 and 600.'
}

$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $evidence | Out-Null
$started=[DateTimeOffset]::UtcNow

$os=$null
$cpu=$null
try{
  $os=Get-CimInstance Win32_OperatingSystem
  $cpu=Get-CimInstance Win32_Processor |
    Measure-Object -Property LoadPercentage -Average
}catch{}

$totalMemoryBytes=if($os){[int64]$os.TotalVisibleMemorySize*1024}else{0}
$freeMemoryBytes=if($os){[int64]$os.FreePhysicalMemory*1024}else{0}
$freeMemoryPercent=if($totalMemoryBytes -gt 0){
  [Math]::Round(100.0*$freeMemoryBytes/$totalMemoryBytes,2)
}else{$null}

$capacityPath=$env:VG_CAPACITY_PATH
if([string]::IsNullOrWhiteSpace($capacityPath)){
  $capacityPath='D:\CODEX'
}
$fullCapacityPath=[IO.Path]::GetFullPath($capacityPath)
$driveName=[IO.Path]::GetPathRoot($fullCapacityPath).Substring(0,1)
$drive=Get-PSDrive -Name $driveName
$diskTotal=[int64]($drive.Used+$drive.Free)
$diskFreePercent=if($diskTotal -gt 0){
  [Math]::Round(100.0*$drive.Free/$diskTotal,2)
}else{0}

$inode=[ordered]@{status='UNKNOWN';freePercent=$null;raw=$null}
try{
  $driveLower=$driveName.ToLowerInvariant()
  $raw=(wsl.exe sh -lc "df -Pi /mnt/$driveLower 2>/dev/null | tail -1" | Select-Object -Last 1)
  if($raw){
    $parts=($raw -split '\s+') | Where-Object {$_}
    if($parts.Count -ge 6 -and $parts[4] -match '^(\d+)%$'){
      $used=[double]$Matches[1]
      $inode.status='MEASURED'
      $inode.freePercent=100-$used
      $inode.raw=$raw
    }
  }
}catch{
  $inode.status='UNAVAILABLE'
}

$neighbors=@()
try{
  $lines=wsl.exe docker ps --format '{{json .}}'
  foreach($line in $lines){
    if($line -and $line -notmatch '^wsl:'){
      $item=$line | ConvertFrom-Json
      $neighbors += [ordered]@{
        name=$item.Names
        image=$item.Image
        status=$item.Status
        ports=$item.Ports
      }
    }
  }
}catch{}

$dockerStats=@()
try{
  $lines=wsl.exe docker stats --no-stream --format '{{json .}}'
  foreach($line in $lines){
    if($line -and $line -notmatch '^wsl:'){
      $item=$line | ConvertFrom-Json
      $dockerStats += [ordered]@{
        name=$item.Name
        cpu=$item.CPUPerc
        memory=$item.MemUsage
        pids=$item.PIDs
      }
    }
  }
}catch{}
$healthLoad=[ordered]@{
  status='BLOCKED'
  reason='VG_CAPACITY_LOAD_AUTHORIZED must equal YES and VG_CAPACITY_API_BASE must be an allowed stage URL'
  requests=0
  successes=0
  failures=0
  durationSeconds=0
}
$apiBase=$env:VG_CAPACITY_API_BASE
$loadAuthorized=$env:VG_CAPACITY_LOAD_AUTHORIZED -eq 'YES'
if($loadAuthorized -and -not [string]::IsNullOrWhiteSpace($apiBase)){
  $uri=$null
  if([Uri]::TryCreate($apiBase,[UriKind]::Absolute,[ref]$uri) -and
     ($uri.Scheme -eq 'https' -or
      ($uri.Scheme -eq 'http' -and $uri.IsLoopback))){
    $healthLoad.status='RUNNING'
    $healthLoad.reason=$null
    $loadStart=[DateTimeOffset]::UtcNow
    $client=New-Object System.Net.Http.HttpClient
    try{
      $client.Timeout=[TimeSpan]::FromSeconds(5)
      $deadline=[DateTimeOffset]::UtcNow.AddSeconds($DurationSeconds)
      while([DateTimeOffset]::UtcNow -lt $deadline -and $healthLoad.requests -lt 500){
        try{
          $response=$client.GetAsync(
            [Uri]::new($uri,'/health/live')).GetAwaiter().GetResult()
          $healthLoad.requests++
          if($response.IsSuccessStatusCode){$healthLoad.successes++}else{$healthLoad.failures++}
          $response.Dispose()
        }catch{
          $healthLoad.requests++
          $healthLoad.failures++
        }
        Start-Sleep -Milliseconds 100
      }
      $healthLoad.durationSeconds=[Math]::Round(
        ([DateTimeOffset]::UtcNow-$loadStart).TotalSeconds,2)
      $healthLoad.status=if($healthLoad.failures -eq 0){'PASS'}else{'DEGRADED'}
    }finally{
      $client.Dispose()
    }
  }
}

$jobLoad=[ordered]@{
  status='BLOCKED'
  reason='No authorized stage job driver configured; concurrent downloader+ASR load was not invented or executed'
}
if($env:VG_CAPACITY_JOB_DRIVER_AUTHORIZED -eq 'YES' -and
   -not [string]::IsNullOrWhiteSpace($env:VG_CAPACITY_JOB_DRIVER_EVIDENCE)){
  $jobLoad.status='EXTERNAL_EVIDENCE_REQUIRED'
  $jobLoad.reason='Review the explicitly authorized job-driver evidence file before increasing concurrency'
}

$forecast=[ordered]@{
  diskGrowthBytesPerHour=$null
  hoursUntilTwentyPercentFree=$null
  status='BLOCKED_NO_TIME_SERIES'
}
$growth=0L
if([int64]::TryParse($env:VG_CAPACITY_DISK_GROWTH_BYTES_PER_HOUR,[ref]$growth) -and
   $growth -gt 0 -and $diskTotal -gt 0){
  $targetFree=[int64]($diskTotal*0.20)
  $headroom=[Math]::Max([int64]0,[int64]$drive.Free-$targetFree)
  $forecast.diskGrowthBytesPerHour=$growth
  $forecast.hoursUntilTwentyPercentFree=[Math]::Round($headroom/$growth,2)
  $forecast.status='MEASURED_FROM_PROVIDED_TIME_SERIES_RATE'
}

$safeCapacity=
  $diskFreePercent -ge 20 -and
  ($null -eq $freeMemoryPercent -or $freeMemoryPercent -ge 20) -and
  $healthLoad.status -eq 'PASS' -and
  $jobLoad.status -notlike 'BLOCKED*'

$result=[ordered]@{
  schemaVersion=1
  environment=$EnvironmentName
  startedUtc=$started.ToString('O')
  completedUtc=[DateTimeOffset]::UtcNow.ToString('O')
  sourceCommit=(git -C (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path rev-parse HEAD).Trim()
  host=[ordered]@{
    cpuLoadPercent=if($cpu){[Math]::Round([double]$cpu.Average,2)}else{$null}
    totalMemoryBytes=$totalMemoryBytes
    freeMemoryBytes=$freeMemoryBytes
    freeMemoryPercent=$freeMemoryPercent
    capacityPath=$fullCapacityPath
    diskFreeBytes=[int64]$drive.Free
    diskFreePercent=$diskFreePercent
    inode=$inode
  }
  neighborContainers=$neighbors
  dockerStats=$dockerStats
  healthSyntheticLoad=$healthLoad
  concurrentMediaJobLoad=$jobLoad
  diskForecast=$forecast
  recommendation=if($safeCapacity){
    'NO_AUTOMATIC_CHANGE_REVIEW_EVIDENCE'
  }else{
    'DO_NOT_INCREASE_CONCURRENCY'
  }
  writesHostLimits=$false
}
$out=Join-Path $evidence 'capacity-report.json'
$result | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 $out
Write-Host $out
