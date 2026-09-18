param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Emulator","Live")]
    [string]$Mode,

    [Parameter(Mandatory=$true)]
    [string]$EvidenceDirectory,

    [Parameter(Mandatory=$true)]
    [uri]$BotApiBaseUri
)

$ErrorActionPreference = "Stop"
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $evidence | Out-Null

if ($BotApiBaseUri.Scheme -notin @("http","https")) {
    throw "BotApiBaseUri must be http/https."
}

if ($Mode -eq "Live") {
    if ($env:VG_TELEGRAM_LIVE_TRANSPORT_AUTHORIZED -ne "YES") {
        $blocked = [ordered]@{
            mode = "Live"
            status = "BLOCKED"
            reason = "explicit_live_transport_authorization_missing"
            timestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
        }
        $blocked | ConvertTo-Json -Depth 4 |
            Set-Content -Encoding UTF8 (Join-Path $evidence "telegram-transport-live.json")
        throw "Live transport is blocked without VG_TELEGRAM_LIVE_TRANSPORT_AUTHORIZED=YES."
    }
    if ([string]::IsNullOrWhiteSpace($env:VG_TELEGRAM_TEST_CHAT_ID) -or
        [string]::IsNullOrWhiteSpace($env:VG_TELEGRAM_TEST_BOT_TOKEN)) {
        throw "Live mode requires VG_TELEGRAM_TEST_CHAT_ID and VG_TELEGRAM_TEST_BOT_TOKEN."
    }
}

$probe = [ordered]@{
    mode = $Mode
    baseUri = $BotApiBaseUri.AbsoluteUri
    status = "READY_FOR_AUTHORIZED_PROBE"
    checks = @(
        "small_file_hash_and_size",
        "hosted_limit_boundary",
        "local_bot_api_large_file_boundary",
        "no_hidden_transcoding",
        "split_reassembly_hash_if_selected"
    )
    liveAuthorized = ($Mode -eq "Live" -and $env:VG_TELEGRAM_LIVE_TRANSPORT_AUTHORIZED -eq "YES")
    timestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
}

if ($Mode -eq "Emulator") {
    $probe.status = "EMULATOR_CONFIG_VALID"
}

$out = Join-Path $evidence ("telegram-transport-" + $Mode.ToLowerInvariant() + ".json")
$probe | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 $out
Write-Host "Evidence:" $out
