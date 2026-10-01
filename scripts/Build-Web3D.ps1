param(
    [string]$Npx = 'npx.cmd',
    [string]$EsbuildVersion = '0.28.2',
    [string]$ThreeVersion = '0.186.1'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$webRoot = Join-Path $repositoryRoot 'src\VideoGrabber.Platform.Api\wwwroot\web'
$entry = Join-Path $webRoot 'hero-three.js'
$output = Join-Path $webRoot 'hero-three.bundle.js'
$license = Join-Path $webRoot 'vendor\three.LICENSE.txt'
$threeVersionFile = Join-Path $webRoot 'vendor\three.version.txt'

if (-not (Test-Path -LiteralPath $entry -PathType Leaf)) {
    throw "Missing Three.js hero source: $entry"
}
if (-not (Test-Path -LiteralPath $license -PathType Leaf)) {
    throw "Missing Three.js MIT license: $license"
}
if (-not (Test-Path -LiteralPath $threeVersionFile -PathType Leaf)) {
    throw "Missing Three.js version marker: $threeVersionFile"
}
$actualThreeVersion = (Get-Content -LiteralPath $threeVersionFile -Raw).Trim()
if ($actualThreeVersion -ne $ThreeVersion) {
    throw "Three.js version mismatch: expected $ThreeVersion, found $actualThreeVersion."
}

$arguments = @(
    '--yes',
    "esbuild@$EsbuildVersion",
    $entry,
    '--bundle',
    '--format=esm',
    '--minify',
    '--tree-shaking=true',
    '--legal-comments=none',
    "--outfile=$output"
)

& $Npx @arguments
if ($LASTEXITCODE -ne 0) {
    throw "esbuild failed with exit code $LASTEXITCODE."
}

$sourceText = [IO.File]::ReadAllText($entry)
$canonicalSource = $sourceText.Replace("`r`n", "`n").Replace("`r", "`n")
$canonicalBytes = [Text.UTF8Encoding]::new($false).GetBytes($canonicalSource)
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $hashBytes = $sha256.ComputeHash($canonicalBytes)
}
finally {
    $sha256.Dispose()
}
$sourceHash = ([BitConverter]::ToString($hashBytes)).Replace("-", "").ToLowerInvariant()
$bundleContent = Get-Content -LiteralPath $output -Raw
$marker = "// VideoGrabber 3D source-sha256:$sourceHash" + [Environment]::NewLine
[IO.File]::WriteAllText(
    $output,
    $marker + $bundleContent,
    [Text.UTF8Encoding]::new($false)
)

$file = Get-Item -LiteralPath $output
if ($file.Length -lt 300000 -or $file.Length -gt 1000000) {
    throw "Unexpected hero-three.bundle.js size: $($file.Length) bytes."
}

$content = Get-Content -LiteralPath $output -Raw
foreach ($needle in @(
    'three.js r',
    'videograbber-planet-map.webp',
    'videograbber:hero-accent'
)) {
    if ($content.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
        throw "Bundled Three.js hero is missing marker: $needle"
    }
}

[ordered]@{
    bundle = $output
    bytes = $file.Length
    sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
    esbuild = $EsbuildVersion
    three = $ThreeVersion
    sourceSha256 = $sourceHash
} | ConvertTo-Json
