param(
  [Parameter(Mandatory=$true)][string]$EvidenceDirectory
)

$ErrorActionPreference='Stop'

if(-not [IO.Path]::IsPathRooted($EvidenceDirectory)){
  throw 'EvidenceDirectory must be absolute.'
}
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $evidence | Out-Null
$log=Join-Path $evidence 'alert-receiver.jsonl'
if(Test-Path $log){Clear-Content $log}

$port=Get-Random -Minimum 24000 -Maximum 32000
$job=Start-Job -ArgumentList $port,$log -ScriptBlock {
  param($port,$log)
  $listener=[System.Net.Sockets.TcpListener]::new(
    [System.Net.IPAddress]::Loopback,
    $port)
  $listener.Start()
  try{
    for($i=0;$i -lt 3;$i++){
      $client=$listener.AcceptTcpClient()
      try{
        $stream=$client.GetStream()
        $reader=New-Object IO.StreamReader($stream,[Text.Encoding]::UTF8,$false,4096,$true)
        $contentLength=0
        while($true){
          $line=$reader.ReadLine()
          if($null -eq $line -or $line.Length -eq 0){break}
          if($line -match '^Content-Length:\s*(\d+)$'){
            $contentLength=[int]$Matches[1]
          }
        }
        $chars=New-Object char[] $contentLength
        $read=0
        while($read -lt $contentLength){
          $n=$reader.Read($chars,$read,$contentLength-$read)
          if($n -le 0){break}
          $read+=$n
        }
        $body=if($read -gt 0){-join $chars[0..($read-1)]}else{''}
        Add-Content -Encoding utf8 $log $body
        $crlf=[string][char]13+[string][char]10
        $responseText='HTTP/1.1 204 No Content'+$crlf+'Content-Length: 0'+$crlf+'Connection: close'+$crlf+$crlf
        $bytes=[Text.Encoding]::ASCII.GetBytes($responseText)
        $stream.Write($bytes,0,$bytes.Length)
        $stream.Flush()
      }finally{
        $client.Dispose()
      }
    }
  }finally{
    $listener.Stop()
  }
}

Start-Sleep -Milliseconds 700
$uri="http://127.0.0.1:$port/"
$notifications=@(
  [ordered]@{
    code='disk_low'
    state='firing'
    severity='critical'
    message='Free disk is below 20 percent.'
    at=[DateTimeOffset]::UtcNow.ToString('O')
  },
  [ordered]@{
    code='disk_low'
    state='firing'
    severity='critical'
    message='Free disk is below 20 percent.'
    at=[DateTimeOffset]::UtcNow.AddMinutes(61).ToString('O')
  },
  [ordered]@{
    code='disk_low'
    state='resolved'
    severity='info'
    message='Operational alert resolved.'
    at=[DateTimeOffset]::UtcNow.AddMinutes(62).ToString('O')
  }
)

foreach($notification in $notifications){
  $json=$notification | ConvertTo-Json -Compress
  $response=Invoke-WebRequest -UseBasicParsing -Method Post -Uri $uri -ContentType 'application/json' -Body $json
  if($response.StatusCode -ne 204){throw 'Alert emulator did not return 204.'}
}

Wait-Job $job -Timeout 10 | Out-Null
if($job.State -ne 'Completed'){
  Stop-Job $job -ErrorAction SilentlyContinue
  throw 'Alert receiver did not complete.'
}
Receive-Job $job -ErrorAction Stop | Out-Null
Remove-Job $job
$lines=Get-Content $log
if($lines.Count -ne 3){throw 'Alert receiver did not log exactly three transitions.'}
foreach($line in $lines){
  $body=$line | ConvertFrom-Json
  if($body.code -ne 'disk_low'){throw 'Unexpected alert code.'}
  if($line -match '(?i)account_id|email|token|https?://'){
    throw 'Alert body contains forbidden sensitive routing data.'
  }
}
$states=@($lines | ForEach-Object {($_ | ConvertFrom-Json).state})
if(($states -join ',') -ne 'firing,firing,resolved'){
  throw 'Alert transition order is incorrect.'
}
[ordered]@{
  status='PASS'
  receiver="127.0.0.1:$port"
  received=$lines.Count
  states=$states
  sensitiveDataPresent=$false
  completedUtc=[DateTimeOffset]::UtcNow.ToString('O')
} | ConvertTo-Json -Depth 6 |
  Set-Content -Encoding utf8 (Join-Path $evidence 'alert-test.json')
Write-Host 'PASS'
