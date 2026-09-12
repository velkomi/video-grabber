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
foreach($name in @('Analyze-Logs.ps1','Install-DiagnosticsTasks.ps1')){
    $tokens=$null;$errors=$null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $name),[ref]$tokens,[ref]$errors)
    if($errors.Count){throw ($name+': invalid PowerShell syntax')}
}
'PASS: daily/weekly/monthly counts; corrupted JSON; no-data; private-data exclusion; 30-day retention; PowerShell syntax.'
('Evidence: '+$fixture)
'PASS' | Set-Content (Join-Path $fixture 'RESULT.txt')
