[CmdletBinding()]
param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($EvidenceDirectory)){$EvidenceDirectory=Join-Path ([IO.Path]::GetTempPath()) ('VG-diagnostics-'+[guid]::NewGuid().ToString('N'))}
$fixture=Join-Path $EvidenceDirectory ('diagnostic-fixture-'+[guid]::NewGuid().ToString('N'))
$logs=Join-Path $fixture 'logs'; $reports=Join-Path $fixture 'reports'
New-Item -ItemType Directory -Force $logs,$reports | Out-Null
$now=[DateTimeOffset]::UtcNow
$lines=@(
    (@{timestamp=$now.AddMinutes(-2).ToString('o');stage='download';status='succeeded';message='Cookie: must-not-leak'}|ConvertTo-Json -Compress),
    (@{timestamp=$now.AddMinutes(-1).ToString('o');stage='download';status='failed';message='private-data-never-in-report'}|ConvertTo-Json -Compress),
    (@{timestamp=$now.AddDays(-2).ToString('o');stage='edit';status='succeeded'}|ConvertTo-Json -Compress),
    (@{timestamp=$now.AddDays(-10).ToString('o');stage='transcription';status='succeeded'}|ConvertTo-Json -Compress),
    '{bad-json'
)
$lines | Set-Content (Join-Path $logs 'vg-fixture.jsonl') -Encoding UTF8
$old=Join-Path $logs 'vg-old.jsonl'; '{}' | Set-Content $old; [IO.File]::SetLastWriteTimeUtc($old,[DateTime]::UtcNow.AddDays(-31))
foreach($period in @('daily','weekly','monthly')){
    & (Join-Path $PSScriptRoot 'Analyze-Logs.ps1') -Period $period -LogDirectory $logs -OutputDirectory $reports
    $path=Join-Path $reports ('vg-report-'+(Get-Date -Format 'yyyyMMdd')+'-'+$period+'.json')
    $result=Get-Content $path -Raw | ConvertFrom-Json
    $expected=@{daily=2;weekly=3;monthly=4}[$period]
    if($result.eventCount -ne $expected -or $result.failureEventCount -ne 1 -or $result.malformedLines -ne 1 -or $result.status -ne 'incomplete'){throw ('Incorrect '+$period+' aggregation')}
}
if(Test-Path $old){throw 'Expired application log was not pruned'}
$all=[string]::Join(' ',@(Get-ChildItem $reports -File | ForEach-Object {[IO.File]::ReadAllText($_.FullName)}))
if($all -match 'must-not-leak|private-data-never-in-report'){throw 'Private raw messages leaked into reports'}
$empty=Join-Path $fixture 'empty-reports'
& (Join-Path $PSScriptRoot 'Analyze-Logs.ps1') -LogDirectory (Join-Path $fixture 'missing-logs') -OutputDirectory $empty
$zero=Get-ChildItem $empty -Filter '*.json' | Select-Object -First 1
if((Get-Content $zero.FullName -Raw | ConvertFrom-Json).status -ne 'no_data'){throw 'No-data result is falsely successful'}
$countRoot=Join-Path $fixture ('count-cap-'+[guid]::NewGuid().ToString('N'))
$countLogs=Join-Path $countRoot 'logs'; $countReports=Join-Path $countRoot 'reports'
New-Item -ItemType Directory -Force $countLogs,$countReports | Out-Null
for($i=0;$i -lt 513;$i++){
    $status=if($i -eq 0){'failed'}else{'succeeded'}
    $event=@{timestamp=$now.AddMinutes(-5).ToString('o');stage='countcap';status=$status}|ConvertTo-Json -Compress
    $path=Join-Path $countLogs ('vg-cap-{0:D3}.jsonl' -f $i)
    $event | Set-Content -LiteralPath $path -Encoding UTF8
    [IO.File]::SetLastWriteTimeUtc($path,[DateTime]::UtcNow.AddMinutes(-20).AddSeconds($i))
}
& (Join-Path $PSScriptRoot 'Analyze-Logs.ps1') -Period daily -LogDirectory $countLogs -OutputDirectory $countReports
$countPath=Join-Path $countReports ('vg-report-'+(Get-Date -Format 'yyyyMMdd')+'-daily.json')
$countReport=Get-Content $countPath -Raw | ConvertFrom-Json
if($countReport.status -ne 'incomplete'){throw 'Count cap concealed incomplete coverage'}
if($countReport.skippedFiles -ne 1){throw 'Expected exactly one count-excluded file'}
if($countReport.eventCount -ne 512){throw 'Unexpected scanned event count'}
$singleRoot=Join-Path $fixture ('single-cap-'+[guid]::NewGuid().ToString('N'))
$singleLogs=Join-Path $singleRoot 'logs'; $singleReports=Join-Path $singleRoot 'reports'
New-Item -ItemType Directory -Force $singleLogs,$singleReports | Out-Null
$singleFile=Join-Path $singleLogs 'vg-oversize.jsonl'
$stream=[IO.File]::Open($singleFile,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{$stream.SetLength(4MB+1)}finally{$stream.Dispose()}
& (Join-Path $PSScriptRoot 'Analyze-Logs.ps1') -Period daily -LogDirectory $singleLogs -OutputDirectory $singleReports
$singleReport=Get-Content (Join-Path $singleReports ('vg-report-'+(Get-Date -Format 'yyyyMMdd')+'-daily.json')) -Raw | ConvertFrom-Json
if($singleReport.status -ne 'incomplete' -or $singleReport.skippedFiles -ne 1 -or $singleReport.eventCount -ne 0){throw 'Single-file cap did not report incomplete all-skipped coverage'}

$totalRoot=Join-Path $fixture ('total-cap-'+[guid]::NewGuid().ToString('N'))
$totalLogs=Join-Path $totalRoot 'logs'; $totalReports=Join-Path $totalRoot 'reports'
New-Item -ItemType Directory -Force $totalLogs,$totalReports | Out-Null
for($i=0;$i -lt 17;$i++){
    $path=Join-Path $totalLogs ('vg-total-{0:D2}.jsonl' -f $i)
    $stream=[IO.File]::Open($path,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.SetLength(4MB-1024)}finally{$stream.Dispose()}
}
& (Join-Path $PSScriptRoot 'Analyze-Logs.ps1') -Period daily -LogDirectory $totalLogs -OutputDirectory $totalReports
$totalReport=Get-Content (Join-Path $totalReports ('vg-report-'+(Get-Date -Format 'yyyyMMdd')+'-daily.json')) -Raw | ConvertFrom-Json
if($totalReport.status -ne 'incomplete' -or $totalReport.skippedFiles -ne 1){throw 'Aggregate 64 MiB cap was not reported as incomplete'}
foreach($name in @('Analyze-Logs.ps1','Install-DiagnosticsTasks.ps1','Test-Diagnostics.ps1')){
    $tokens=$null;$errors=$null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $name),[ref]$tokens,[ref]$errors)
    if($errors.Count){throw ($name+': invalid PowerShell syntax')}
}
'PASS: daily/weekly/monthly counts; corrupted JSON; no-data; private-data exclusion; 30-day retention; PowerShell syntax.'
('Evidence: '+$fixture)
'PASS' | Set-Content (Join-Path $fixture 'RESULT.txt')
