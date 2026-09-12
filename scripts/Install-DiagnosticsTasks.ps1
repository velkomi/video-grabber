[CmdletBinding(SupportsShouldProcess=$true)]
param([string]$AnalyzerPath=(Join-Path $PSScriptRoot 'Analyze-Logs.ps1'))
$ErrorActionPreference='Stop'
if (-not (Test-Path -LiteralPath $AnalyzerPath -PathType Leaf)) { throw 'Не найден Analyze-Logs.ps1.' }
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if ($identity.IsSystem) { throw 'Расписание нужно включать от обычного пользователя, не от SYSTEM.' }
$user=$identity.Name
$exe=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 5) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$definitions=@(
    @{suffix='Daily';period='daily';trigger=(New-ScheduledTaskTrigger -Daily -At '09:00')},
    @{suffix='Weekly';period='weekly';trigger=(New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At '09:10')},
    @{suffix='Monthly';period='monthly';trigger=(New-ScheduledTaskTrigger -Daily -At '09:20')}
)
foreach($definition in $definitions){
    $name='VideoGrabber-Diagnostics-'+$definition.suffix
    $arguments='-NoProfile -NonInteractive -File "'+[IO.Path]::GetFullPath($AnalyzerPath)+'" -Period '+$definition.period+' -Scheduled'
    $existing=Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
    if($existing){
        $action=@($existing.Actions)
        if($action.Count -ne 1 -or $action[0].Arguments -ne $arguments -or $action[0].Execute -ne $exe){throw ('Уже есть другая задача с именем '+$name+'. Она не изменена.')}
        Write-Output ($name+': already configured.');continue
    }
    if($PSCmdlet.ShouldProcess($name,'Создать локальную проверку журналов от текущего пользователя')){
        $action=New-ScheduledTaskAction -Execute $exe -Argument $arguments
        $task=New-ScheduledTask -Action $action -Trigger $definition.trigger -Settings $settings -Principal $principal -Description 'VideoGrabber: local aggregate diagnostics, no cloud upload and no code changes. Monthly report only on day 1.'
        Register-ScheduledTask -TaskName $name -InputObject $task | Out-Null
        Write-Output ($name+': registered.')
    }
}
Write-Output 'Times use Windows local time. The PC and interactive user session must be available; missed starts run when available. Telegram and automatic code changes are disabled.'
