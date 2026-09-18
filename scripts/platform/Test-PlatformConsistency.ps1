param(
  [Parameter(Mandatory=$true)][string]$EvidenceDirectory
)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($env:VG_CONSISTENCY_DSN_FILE) -or -not (Test-Path $env:VG_CONSISTENCY_DSN_FILE)){throw 'VG_CONSISTENCY_DSN_FILE is required.'}
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$env:VG_PLATFORM_CONSISTENCY_DSN=(Get-Content $env:VG_CONSISTENCY_DSN_FILE -Raw).Trim()
$dll=Join-Path $repo 'src\VideoGrabber.Platform.Api\bin\Release\net10.0\VideoGrabber.Platform.Api.dll'
$report=& dotnet $dll --consistency-report
$code=$LASTEXITCODE
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $evidence | Out-Null
$report | Set-Content -Encoding utf8 (Join-Path $evidence 'consistency-report.json')
exit $code
