using VideoGrabber.Platform.Core.Operations;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DeploymentConfigurationTests
{
    [Theory]
    [InlineData("postgres:latest")]
    [InlineData("videograbber-api:dev")]
    [InlineData("caddy:2.10.0")]
    [InlineData("")]
    public void Stage_rejects_unpinned_images(string image)
        => Assert.False(DeploymentConfiguration.IsPinnedImage(image));

    [Fact]
    public void Stage_accepts_digest_pinned_image()
        => Assert.True(DeploymentConfiguration.IsPinnedImage(
            "postgres@sha256:" + new string('a', 64)));

    [Theory]
    [InlineData("vg-stage-a", true)]
    [InlineData("vg-stage-test-123", true)]
    [InlineData("prod", false)]
    [InlineData("vg-prod-a", false)]
    [InlineData("vg-stage-UPPER", false)]
    public void Stage_name_is_strict(string name, bool expected)
        => Assert.Equal(expected, DeploymentConfiguration.ValidateStageName(name));

    [Fact]
    public void Secure_stage_config_has_no_errors()
    {
        var config = Secure();

        Assert.Empty(DeploymentConfiguration.Validate(config));
    }

    [Theory]
    [InlineData("DatabaseBind")]
    [InlineData("BotApiBind")]
    [InlineData("AdminBind")]
    [InlineData("AllowedOrigins")]
    [InlineData("AuthIssuer")]
    [InlineData("AuthDevelopmentIssuerEnabled")]
    [InlineData("SigningKeyFile")]
    [InlineData("LiveCatalogPath")]
    [InlineData("TelegramBotApiBaseUri")]
    [InlineData("CallbackUrls")]
    [InlineData("PublicHttpsHost")]
    public void Insecure_field_is_reported_by_name_only(string field)
    {
        var secure = Secure();
        var bad = field switch
        {
            "DatabaseBind" => secure with { DatabaseBind = "0.0.0.0" },
            "BotApiBind" => secure with { BotApiBind = "0.0.0.0" },
            "AdminBind" => secure with { AdminBind = "0.0.0.0" },
            "AllowedOrigins" => secure with { AllowedOrigins = ["*"] },
            "AuthIssuer" => secure with { AuthIssuer = "http://issuer.test" },
            "AuthDevelopmentIssuerEnabled" => secure with { AuthDevelopmentIssuerEnabled = true },
            "SigningKeyFile" => secure with { SigningKeyFile = "" },
            "LiveCatalogPath" => secure with { PaymentsLiveEnabled = true, LiveCatalogPath = null },
            "TelegramBotApiBaseUri" => secure with { TelegramBotApiBaseUri = "https://api.telegram.org" },
            "CallbackUrls" => secure with { CallbackUrls = ["https://evil.example.test/callback"] },
            "PublicHttpsHost" => secure with { PublicHttpsHost = "prod.example.test" },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var errors = DeploymentConfiguration.Validate(bad);

        Assert.Contains(field, errors);
    }

    private static StageConfiguration Secure()
        => new(
            "vg-stage-test",
            "registry.example/vg-api@sha256:" + new string('1', 64),
            "registry.example/vg-worker@sha256:" + new string('2', 64),
            "postgres@sha256:" + new string('3', 64),
            "caddy@sha256:" + new string('4', 64),
            "telegram-bot-api@sha256:" + new string('5', 64),
            "stage.example.test",
            "10.77.0.2",
            "10.77.0.3",
            "127.0.0.1",
            ["https://stage.example.test"],
            "https://auth.stage.example.test",
            false,
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "vg-signing-key")),
            false,
            null,
            "http://10.77.0.3:8081",
            ["https://stage.example.test/auth/callback"]);
}
