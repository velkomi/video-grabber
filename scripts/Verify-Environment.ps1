[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Find-Command([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) { return 'не найден' }
    return $command.Source
}

$os = Get-CimInstance Win32_OperatingSystem
$drive = Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($PSScriptRoot).TrimEnd(':', '\'))

[pscustomobject]@{
    Windows       = "$($os.Caption) $($os.Version) build $($os.BuildNumber)"
    Architecture  = $env:PROCESSOR_ARCHITECTURE
    DotNet        = Find-Command 'dotnet'
    Git           = Find-Command 'git'
    YtDlp         = Find-Command 'yt-dlp'
    FFmpeg        = Find-Command 'ffmpeg'
    FFprobe       = Find-Command 'ffprobe'
    FreeDiskGB    = [math]::Round($drive.Free / 1GB, 1)
}

