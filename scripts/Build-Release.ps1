[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$DotNet = 'dotnet',
    [ValidateSet('Local', 'Managed')]
    [string]$Edition = 'Local',
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath (Join-Path $repositoryRoot 'VERSION') -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw 'Версия не указана и не найдена в VERSION.'
}
$releaseRoot = Join-Path $repositoryRoot "artifacts\release-$Version"
$artifactName = if ($Edition -eq 'Managed') { "VideoGrabber.Managed-$Runtime" } else { "VideoGrabber-$Runtime" }
$output = Join-Path $releaseRoot $artifactName
if (Test-Path -LiteralPath $output) {
    throw "Папка релиза уже существует: $output. Укажите новую версию, чтобы не смешивать файлы сборок."
}

& $DotNet restore (Join-Path $repositoryRoot 'VideoGrabber.slnx')
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }
& $DotNet test (Join-Path $repositoryRoot 'VideoGrabber.slnx') -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed' }
& $DotNet publish (Join-Path $repositoryRoot 'src\VideoGrabber.App\VideoGrabber.App.csproj') -c $Configuration -r $Runtime --self-contained true -o $output --no-restore -p:Version=$Version -p:VideoGrabberEdition=$Edition
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $output
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $output
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD_PARTY_NOTICES.md') -Destination $output
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\MEDIA_WORKFLOWS.md') -Destination $output

$archive = Join-Path $releaseRoot "$artifactName.zip"
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive -Algorithm SHA256
