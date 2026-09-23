using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DeploymentAssetTests
{
    [Fact]
    public void Compose_keeps_runtime_db_roles_proxy_and_secrets_explicit()
    {
        var root = FindRepoRoot();
        var compose = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "compose.staging.yml"));

        Assert.Contains("role-provisioner:", compose, StringComparison.Ordinal);
        Assert.Contains(
            "exec bash /opt/videograbber/provision_runtime_roles.sh",
            compose,
            StringComparison.Ordinal);
        Assert.Contains("egress-proxy:", compose, StringComparison.Ordinal);
        Assert.Contains("VG_PLATFORM_IDENTITY_DSN", compose, StringComparison.Ordinal);
        Assert.Contains("VG_PLATFORM_DEVICE_DSN", compose, StringComparison.Ordinal);
        Assert.Contains("VG_PLATFORM_SESSION_SIGNING_KEY", compose, StringComparison.Ordinal);
        Assert.Contains("VG_OPERATIONS_TOKEN", compose, StringComparison.Ordinal);
        Assert.Contains("VG_SOURCE_ENCRYPTION_KEY", compose, StringComparison.Ordinal);
        Assert.Contains("telegram_webhook_secret", compose, StringComparison.Ordinal);
        Assert.Contains("telegram_inbox_key", compose, StringComparison.Ordinal);
        Assert.Contains("telegram_bot_user_id", compose, StringComparison.Ordinal);
        Assert.Contains(
            "workerjobs:/var/lib/videograbber/jobs",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "VG_ARTIFACT_UPLOAD_ROOT: /var/lib/videograbber/jobs/uploads",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "VG_RETENTION_ROOT: /var/lib/videograbber/jobs",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "127.0.0.1:${VG_STAGE_HTTP_PORT:-19230}:8080",
            compose,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "VG_SESSION_JWT_SIGNING_KEY=",
            compose,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "VG_TELEGRAM_BOT_API_BASE:",
            compose,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Egress_proxy_is_digest_pinned_and_blocks_private_networks()
    {
        var root = FindRepoRoot();
        var lockText = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "dependency-lock.json"));
        var squid = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "squid.conf"));

        Assert.Contains(
            "ubuntu/squid@sha256:6a097f68bae708cedbabd6188d68c7e2e7a38cedd05a176e1cc0ba29e3bbe029",
            lockText,
            StringComparison.Ordinal);
        foreach (var cidr in new[]
        {
            "127.0.0.0/8",
            "10.0.0.0/8",
            "100.64.0.0/10",
            "172.16.0.0/12",
            "192.168.0.0/16",
            "169.254.0.0/16",
            "fc00::/7",
            "fe80::/10"
        })
            Assert.Contains(cidr, squid, StringComparison.Ordinal);
        Assert.Contains("http_access deny forbidden_dst", squid, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_runtime_contains_pinned_ytdlp_and_curl_health_dependency()
    {
        var root = FindRepoRoot();
        var dockerfile = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "api.Dockerfile"));

        Assert.Contains("YTDLP_VERSION=2026.09.16.232951", dockerfile, StringComparison.Ordinal);
        Assert.Contains(
            "YTDLP_SHA256=f8ca14db511702a5dbfc5a527056312907ddd0914d0b4036f108d6849e17ef61",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains("apt-get install -y --no-install-recommends ca-certificates curl", dockerfile, StringComparison.Ordinal);
        Assert.Contains("VG_YTDLP_PATH=/usr/local/bin/yt-dlp", dockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_role_provisioning_never_reuses_postgres_for_api()
    {
        var root = FindRepoRoot();
        var prepare = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "prepare_runtime_secrets.sh"));
        var provision = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "provision_runtime_roles.sh"));

        Assert.Contains(
            "for role in api identity admin ledger device operations",
            prepare,
            StringComparison.Ordinal);
        Assert.Contains(
            "write_once \"${role}_password\"",
            prepare,
            StringComparison.Ordinal);
        foreach (var role in new[] { "api", "identity", "admin", "ledger", "device", "operations" })
            Assert.Contains(
                "vg_" + role + "_login",
                prepare + provision,
                StringComparison.Ordinal);
        Assert.Contains("NOINHERIT", provision, StringComparison.Ordinal);
        Assert.Contains("REVOKE CONNECT ON DATABASE videograbber FROM PUBLIC", provision, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_reads_root_only_secrets_then_drops_to_uid_10001()
    {
        var root = FindRepoRoot();
        var compose = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "compose.staging.yml"));
        var api = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "api.Dockerfile"));
        var worker = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "worker.Dockerfile"));

        Assert.Contains("user: \"0:0\"", compose, StringComparison.Ordinal);
        Assert.Contains(
            "exec gosu 10001:10001 dotnet VideoGrabber.Platform.Api.dll",
            compose,
            StringComparison.Ordinal);
        Assert.Contains("volume-init:", compose, StringComparison.Ordinal);
        Assert.Contains(
            "chmod 0700 /data; chown 10001:10001 /data",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "exec gosu 10001:10001 sh -ec 'mkdir -p -m 0700 /var/lib/videograbber/jobs/uploads; exec dotnet VideoGrabber.Platform.Api.dll'",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "exec gosu 10001:10001 dotnet VideoGrabber.Platform.Worker.dll",
            compose,
            StringComparison.Ordinal);
        Assert.Contains("gosu", api, StringComparison.Ordinal);
        Assert.Contains("gosu", worker, StringComparison.Ordinal);
        Assert.Contains("groupadd --system --gid 10001", api, StringComparison.Ordinal);
        Assert.Contains("groupadd --system --gid 10001", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_services_have_only_caps_required_for_uid_and_volume_setup()
    {
        var root = FindRepoRoot();
        var compose = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "compose.staging.yml"));

        Assert.Contains(
            "cap_add: [\"CHOWN\",\"DAC_OVERRIDE\",\"FOWNER\",\"SETGID\",\"SETUID\"]",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "cap_add: [\"CHOWN\"]",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "cap_add: [\"SETGID\",\"SETUID\"]",
            compose,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Public_edge_does_not_log_one_time_auth_query_tokens()
    {
        var root = FindRepoRoot();
        var nginx = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "videograbber-public-nginx.conf"));
        var publisher = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "hostinger-publish-videograbber.sh"));

        Assert.Contains("access_log off;", nginx, StringComparison.Ordinal);
        Assert.Contains("Referrer-Policy \"no-referrer\"", nginx, StringComparison.Ordinal);
        Assert.Contains("SCRIPT_DIR=", publisher, StringComparison.Ordinal);
        Assert.Contains(
            "BACKEND_NETWORK=\"vg-stage-videograbber_edge\"",
            publisher,
            StringComparison.Ordinal);
        Assert.Contains(
            "docker network connect \"$BACKEND_NETWORK\" \"$NAME\"",
            publisher,
            StringComparison.Ordinal);
        Assert.Contains(
            "proxy_pass http://api:8080;",
            nginx,
            StringComparison.Ordinal);
        Assert.Contains(
            "videograbber.srv1902378.hstgr.cloud",
            publisher,
            StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VideoGrabber.slnx")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}
