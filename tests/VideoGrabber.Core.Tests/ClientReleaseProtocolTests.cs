using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Core.Tests;

public sealed class ClientReleaseProtocolTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("1.0.0-preview.9", "1.0.0-preview.10", -1)]
    [InlineData("1.0.0-preview.10", "1.0.0", -1)]
    [InlineData("1.0.0+first", "1.0.0+second", 0)]
    [InlineData("1.0.0-2", "1.0.0-beta", -1)]
    [InlineData("1.0.0-beta.1", "1.0.0-beta", 1)]
    [InlineData("10.0.0", "2.99.99", 1)]
    [InlineData("999999999999999999999.0.0", "999999999999999999998.0.0", 1)]
    public void Semver_compares_numeric_identifiers_and_ignores_build(string left, string right, int expected)
        => Assert.Equal(expected, Math.Sign(ClientReleaseVersion.Parse(left).CompareTo(ClientReleaseVersion.Parse(right))));

    [Theory]
    [InlineData("0.1.10-preview.9-rc.1", "0.1.10-preview.10-rc.1", -1)]
    [InlineData("0.1.10-preview.99-rc.1", "0.1.10-preview.100-rc.1", -1)]
    [InlineData("0.1.10-preview.10-rc.9", "0.1.10-preview.10-rc.10", -1)]
    [InlineData("0.1.10-preview.100-rc.1", "0.1.10", -1)]
    [InlineData("0.1.10-preview.10-rc.1+one", "0.1.10-preview.10.rc.1+two", 0)]
    [InlineData("0.1.10-beta.9-rc.1", "0.1.10-beta.10-rc.1", 1)]
    [InlineData("0.1.10-preview.9-rc.1.extra", "0.1.10-preview.10-rc.1.extra", 1)]
    public void Project_comparison_normalizes_only_exact_legacy_preview_rc_format(string left, string right, int expected)
        => Assert.Equal(expected, Math.Sign(ClientReleaseVersion.CompareForVideoGrabber(left, right)));

    [Fact]
    public void Standard_semver_keeps_legacy_alphanumeric_identifier_lexical()
        => Assert.True(ClientReleaseVersion.Parse("0.1.10-preview.9-rc.1").CompareTo(ClientReleaseVersion.Parse("0.1.10-preview.10-rc.1")) > 0);

    [Theory]
    [InlineData("0.1.10-preview.10-rc.1", "0.1.10-preview.9-rc.1", true)]
    [InlineData("0.1.10-preview.9-rc.1", "0.1.10-preview.10-rc.1", false)]
    [InlineData("0.1.10-preview.100-rc.1", "0.1.10-preview.99-rc.1", true)]
    [InlineData("0.1.10-preview.99-rc.1", "0.1.10-preview.100-rc.1", false)]
    public void Signed_rollback_uses_project_preview_number_order(string current, string rollback, bool accepted)
    {
        using var key = RSA.Create(2048);
        var m = Manifest();
        m = m with { Release = m.Release with { Version = current, Installer = m.Release.Installer with { Version = current }, RollbackInstaller = m.Release.RollbackInstaller! with { Version = rollback } } };
        if (accepted) Assert.Equal(rollback, Verifier(key).Verify(RawSign(m, key)).Release.RollbackInstaller!.Version);
        else Rejected("version", () => Verifier(key).Verify(RawSign(m, key)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-preview.01")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0+a..b")]
    [InlineData("1.0.0 \n")]
    public void Semver_rejects_invalid_forms(string value)
        => Assert.Throws<FormatException>(() => ClientReleaseVersion.Parse(value));

    [Fact]
    public void Signed_roundtrip_returns_manifest_and_preserves_raw_payload_signature()
    {
        using var key = RSA.Create(2048);
        var envelope = ClientReleaseSigner.Sign(Manifest(), key, "test", new FixedClock());
        var bytes = Convert.FromBase64String(envelope.Payload);
        Assert.True(key.VerifyData(bytes, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var restored = ClientReleaseSigner.DeserializeEnvelope(ClientReleaseSigner.SerializeEnvelope(envelope));
        var result = Verifier(key).Verify(restored);
        Assert.Equal("0.1.0-preview.63", result.Release.Version);
        Assert.Equal(new Uri("https://api.example.com/"), result.Services.ApiBase);
        Assert.Equal(10, result.Sequence);
    }

    [Fact]
    public void Signature_tamper_payload_tamper_and_unknown_key_fail_closed()
    {
        using var key = RSA.Create(2048);
        var envelope = RawSign(Manifest(), key);
        var sig = Convert.FromBase64String(envelope.Signature); sig[0] ^= 1;
        Rejected("signature", () => Verifier(key).Verify(envelope with { Signature = Convert.ToBase64String(sig) }));
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(envelope.Payload)).Replace("api.example.com", "evil.example.com", StringComparison.Ordinal);
        Rejected("signature", () => Verifier(key).Verify(envelope with { Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)) }));
        Rejected("key", () => Verifier(key).Verify(envelope with { KeyId = "unknown" }));
    }

    [Fact]
    public void Rsa_pkcs1_signature_cannot_select_an_alternate_algorithm()
    {
        using var key = RSA.Create(2048);
        var envelope = RawSign(Manifest(), key, RSASignaturePadding.Pkcs1);
        Rejected("signature", () => Verifier(key).Verify(envelope));
    }

    [Theory]
    [InlineData("schema", "contract")]
    [InlineData("product", "contract")]
    [InlineData("realm", "contract")]
    [InlineData("channel", "contract")]
    [InlineData("zero-sequence", "sequence")]
    [InlineData("expired", "time")]
    [InlineData("future", "time")]
    [InlineData("inverted", "time")]
    [InlineData("long-validity", "time")]
    [InlineData("notes", "notes")]
    [InlineData("installer-version", "version")]
    [InlineData("rollback-newer", "version")]
    [InlineData("rollback-equal", "version")]
    [InlineData("size-zero", "artifact")]
    [InlineData("size-huge", "artifact")]
    [InlineData("hash", "artifact")]
    public void Signed_invalid_manifest_is_rejected(string mutation, string code)
    {
        using var key = RSA.Create(2048);
        var m = Manifest();
        m = mutation switch
        {
            "schema" => m with { SchemaVersion = 2 },
            "product" => m with { Product = "other" },
            "realm" => m with { AccountRealm = "other" },
            "channel" => m with { Channel = "stable" },
            "zero-sequence" => m with { Sequence = 0 },
            "expired" => m with { ExpiresAt = Now },
            "future" => m with { IssuedAt = Now.AddMinutes(5).AddTicks(1) },
            "inverted" => m with { ExpiresAt = m.IssuedAt },
            "long-validity" => m with { ExpiresAt = m.IssuedAt.AddDays(366).AddTicks(1) },
            "notes" => m with { Release = m.Release with { Notes = new string('a', 1501) } },
            "installer-version" => m with { Release = m.Release with { Installer = m.Release.Installer with { Version = "0.1.0-preview.62" } } },
            "rollback-newer" => m with { Release = m.Release with { RollbackInstaller = m.Release.Installer with { Version = "0.1.0-preview.64" } } },
            "rollback-equal" => m with { Release = m.Release with { RollbackInstaller = m.Release.Installer } },
            "size-zero" => m with { Release = m.Release with { Installer = m.Release.Installer with { SizeBytes = 0 } } },
            "size-huge" => m with { Release = m.Release with { Installer = m.Release.Installer with { SizeBytes = 1073741825 } } },
            "hash" => m with { Release = m.Release with { Installer = m.Release.Installer with { Sha256 = new string('z', 64) } } },
            _ => throw new InvalidOperationException()
        };
        Rejected(code, () => Verifier(key).Verify(RawSign(m, key)));
    }

    [Theory]
    [InlineData("http://api.example.com/")]
    [InlineData("https://user:pass@api.example.com/")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://[2001:4860:4860::8888]/")]
    [InlineData("https://localhost/")]
    [InlineData("https://server/")]
    [InlineData("https://api.localhost/")]
    [InlineData("https://api.example.com/#fragment")]
    [InlineData("https://api.example.com/?query=1")]
    [InlineData("https://api.example.com/path")]
    public void Signed_unsafe_api_endpoint_is_rejected(string value)
    {
        using var key = RSA.Create(2048);
        var m = Manifest();
        Rejected("url", () => Verifier(key).Verify(RawSign(m with { Services = m.Services with { ApiBase = new Uri(value) } }, key)));
    }

    [Fact]
    public void Website_path_and_artifact_query_are_allowed_but_site_query_and_artifact_fragment_are_not()
    {
        using var key = RSA.Create(2048);
        var m = Manifest();
        var valid = m with { Services = m.Services with { WebsiteBase = new Uri("https://www.example.com/app/") }, Release = m.Release with { Installer = m.Release.Installer with { Url = new Uri("https://cdn.example.com/setup.exe?download=1") } } };
        Assert.Equal(valid.Services.WebsiteBase, Verifier(key).Verify(RawSign(valid, key)).Services.WebsiteBase);
        Rejected("url", () => Verifier(key).Verify(RawSign(m with { Services = m.Services with { WebsiteBase = new Uri("https://www.example.com/?x=1") } }, key)));
        Rejected("url", () => Verifier(key).Verify(RawSign(m with { Release = m.Release with { Installer = m.Release.Installer with { Url = new Uri("https://cdn.example.com/setup.exe#x") } } }, key)));
    }

    [Fact]
    public void Sequence_below_high_water_is_rejected_and_equal_sequence_is_available_to_cache()
    {
        using var key = RSA.Create(2048);
        var e = RawSign(Manifest(), key);
        Rejected("sequence", () => Verifier(key).Verify(e, 11));
        Assert.Equal(10, Verifier(key).Verify(e, 10).Sequence);
    }

    [Fact]
    public void Malformed_and_oversized_envelopes_are_rejected_with_neutral_codes()
    {
        using var key = RSA.Create(2048);
        var e = RawSign(Manifest(), key);
        Rejected("format", () => Verifier(key).Verify(e with { Payload = "%%%" }));
        Rejected("format", () => Verifier(key).Verify(e with { Signature = "%%%" }));
        Rejected("size", () => Verifier(key).Verify(e with { Payload = Convert.ToBase64String(new byte[32769]) }));
        Rejected("size", () => ClientReleaseSigner.DeserializeEnvelope(new byte[65537]));
        Rejected("format", () => ClientReleaseSigner.DeserializeEnvelope(Encoding.UTF8.GetBytes("null")));
    }

    [Fact]
    public void Missing_nested_claims_and_duplicate_claims_cannot_be_accepted()
    {
        using var key = RSA.Create(2048);
        var json = Encoding.UTF8.GetString(ClientReleaseSigner.SerializePayload(Manifest()));
        Rejected("contract", () => Verifier(key).Verify(RawSignJson(json.Replace("\"services\":{\"apiBase\":\"https://api.example.com/\",\"websiteBase\":\"https://www.example.com/\"}", "\"services\":null", StringComparison.Ordinal), key)));
        Rejected("format", () => Verifier(key).Verify(RawSignJson(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":2", StringComparison.Ordinal), key)));
    }

    [Fact]
    public void Signer_validates_draft_before_signing()
    {
        using var key = RSA.Create(2048);
        Rejected("contract", () => ClientReleaseSigner.Sign(Manifest() with { AccountRealm = "other" }, key, "test", new FixedClock()));
    }

    [Fact]
    public void Cached_expired_manifest_can_be_classified_but_is_not_a_network_update()
    {
        using var key = RSA.Create(2048);
        var m = Manifest() with { IssuedAt = Now.AddDays(-30), ExpiresAt = Now.AddDays(-1) };
        var e = RawSign(m, key);
        Rejected("time", () => Verifier(key).Verify(e));
        Assert.Equal(Now.AddDays(-1), Verifier(key).VerifyCached(e).ExpiresAt);
        Rejected("sequence", () => Verifier(key).VerifyCached(e, 11));
        Rejected("signature", () => Verifier(key).VerifyCached(e with { Signature = new string('A', 344) }));
    }

    [Fact]
    public void Cached_manifest_cannot_bypass_contract_future_clock_or_url_policy()
    {
        using var key = RSA.Create(2048);
        var m = Manifest();
        Rejected("contract", () => Verifier(key).VerifyCached(RawSign(m with { AccountRealm = "other" }, key)));
        Rejected("time", () => Verifier(key).VerifyCached(RawSign(m with { IssuedAt = Now.AddMinutes(6) }, key)));
        Rejected("url", () => Verifier(key).VerifyCached(RawSign(m with { Services = m.Services with { ApiBase = new Uri("http://api.example.com/") } }, key)));
    }

    [Fact]
    public void Exact_bounds_and_rollback_lower_version_are_accepted()
    {
        using var key = RSA.Create(2048);
        var m = Manifest();
        m = m with { IssuedAt = Now.AddMinutes(5), ExpiresAt = Now.AddMinutes(5).AddDays(366),
            Release = m.Release with { Notes = new string('a', 1500), Installer = m.Release.Installer with { SizeBytes = 1073741824, Sha256 = new string('F', 64) } } };
        Assert.Equal(1073741824, Verifier(key).Verify(RawSign(m, key)).Release.Installer.SizeBytes);
        Assert.Equal("1.2.3-preview.10", ClientReleaseVersion.Parse("1.2.3-preview.10+build.01").ToString());
    }

    [Fact]
    public void Raw_payload_whitespace_remains_part_of_signed_bytes()
    {
        using var key = RSA.Create(2048);
        var json = JsonSerializer.Serialize(Manifest(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        Assert.Equal(10, Verifier(key).Verify(RawSignJson(json, key)).Sequence);
    }

    [Fact]
    public void Known_rotated_keys_are_accepted_and_mismatched_key_id_is_rejected()
    {
        using var oldKey = RSA.Create(2048); using var newKey = RSA.Create(2048);
        var v = new ClientReleaseVerifier(new Dictionary<string, string> { ["old"] = oldKey.ExportSubjectPublicKeyInfoPem(), ["new"] = newKey.ExportSubjectPublicKeyInfoPem() }, clock: new FixedClock());
        Assert.Equal(10, v.Verify(RawSign(Manifest(), oldKey) with { KeyId = "old" }).Sequence);
        var newer = RawSign(Manifest(), newKey) with { KeyId = "new" };
        Assert.Equal(10, v.Verify(newer).Sequence);
        Rejected("signature", () => v.Verify(newer with { KeyId = "old" }));
    }

    private static ClientReleaseManifest Manifest() => new(1, "videograbber", "preview", 10,
        Now.AddMinutes(-1), Now.AddDays(30), "videograbber-main",
        new(new("https://api.example.com/"), new("https://www.example.com/")),
        new("0.1.0-preview.63", Now.AddDays(-1), "Обновление", new("0.1.0-preview.63", new("https://cdn.example.com/setup.exe"), 1234, new string('a', 64)),
            new("0.1.0-preview.62", new("https://cdn.example.com/previous.exe"), 1234, new string('b', 64))));

    private static ClientReleaseVerifier Verifier(RSA key) => new(new Dictionary<string, string> { ["test"] = key.ExportSubjectPublicKeyInfoPem() }, clock: new FixedClock());
    private static SignedClientRelease RawSign(ClientReleaseManifest manifest, RSA key, RSASignaturePadding? padding = null)
        => RawSignJson(JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)), key, padding);
    private static SignedClientRelease RawSignJson(string json, RSA key, RSASignaturePadding? padding = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return new("test", Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, padding ?? RSASignaturePadding.Pss)));
    }
    private static void Rejected(string code, Action action) => Assert.Equal(code, Assert.Throws<ClientReleaseRejectedException>(action).Code);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
