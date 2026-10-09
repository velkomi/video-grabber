using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;
using VideoGrabber.ClientReleasePublisher;
using Xunit;

namespace VideoGrabber.ClientReleasePublisher.Tests;

public sealed class ReleasePublisherTests
{
    [Fact]
    public async Task Generate_key_writes_matching_files_without_printing_private_material()
    {
        using var fixture = new PublisherFixture();
        var result = await fixture.RunAsync("generate-key", "--private-key", fixture.PrivatePath,
            "--public-key", fixture.PublicPath, "--key-id", "ephemeral-test");
        Assert.Equal(0, result);
        var privatePem = await File.ReadAllTextAsync(fixture.PrivatePath);
        var publicPem = await File.ReadAllTextAsync(fixture.PublicPath);
        Assert.DoesNotContain("PRIVATE KEY", fixture.Output.ToString());
        Assert.DoesNotContain("PRIVATE KEY", fixture.Error.ToString());
        if (OperatingSystem.IsWindows())
        {
            var security = new FileInfo(fixture.PrivatePath).GetAccessControl(AccessControlSections.Access);
            Assert.True(security.AreAccessRulesProtected);
            var rule = Assert.Single(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
            Assert.Equal(WindowsIdentity.GetCurrent().User, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        }
        else
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(fixture.PrivatePath));
        using var privateKey = RSA.Create();
        privateKey.ImportFromPem(privatePem);
        using var publicKey = RSA.Create();
        publicKey.ImportFromPem(publicPem);
        Assert.True(publicKey.VerifyData(new byte[] { 1, 2, 3 }, privateKey.SignData(new byte[] { 1, 2, 3 }, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    [Fact]
    public async Task Generate_key_refuses_existing_output_without_changing_files()
    {
        using var fixture = new PublisherFixture();
        await File.WriteAllTextAsync(fixture.PublicPath, "existing-public-output");
        var result = await fixture.RunAsync("generate-key", "--private-key", fixture.PrivatePath,
            "--public-key", fixture.PublicPath, "--key-id", "ephemeral-test");
        Assert.NotEqual(0, result);
        Assert.False(File.Exists(fixture.PrivatePath));
        Assert.Equal("existing-public-output", await File.ReadAllTextAsync(fixture.PublicPath));
    }

    [Fact]
    public async Task Sign_emits_verifiable_envelope_and_preserves_unsigned_draft()
    {
        using var fixture = new PublisherFixture();
        await fixture.GenerateKeyAsync();
        var input = Path.Combine(fixture.DirectoryPath, "draft.json");
        var destination = Path.Combine(fixture.DirectoryPath, "envelope.json");
        var draft = Draft();
        var draftBytes = JsonSerializer.SerializeToUtf8Bytes(draft, ClientReleaseSigner.JsonOptions);
        await File.WriteAllBytesAsync(input, draftBytes);
        Assert.Equal(0, await fixture.RunAsync("sign", "--input", input, "--private-key", fixture.PrivatePath,
            "--key-id", "ephemeral-test", "--output", destination));
        var envelope = ClientReleaseSigner.DeserializeEnvelope(await File.ReadAllBytesAsync(destination));
        var verifier = new ClientReleaseVerifier(new Dictionary<string, string>
        {
            ["ephemeral-test"] = await File.ReadAllTextAsync(fixture.PublicPath)
        });
        Assert.Equal(draft, verifier.Verify(envelope));
        Assert.Equal(draftBytes, await File.ReadAllBytesAsync(input));
        Assert.DoesNotContain("PRIVATE KEY", fixture.Output.ToString());
        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath, "*.tmp"));
    }

    [Theory]
    [InlineData("realm")]
    [InlineData("size")]
    [InlineData("duplicate")]
    public async Task Invalid_draft_never_creates_signed_output(string failure)
    {
        using var fixture = new PublisherFixture();
        await fixture.GenerateKeyAsync();
        var input = Path.Combine(fixture.DirectoryPath, "draft.json");
        var destination = Path.Combine(fixture.DirectoryPath, "envelope.json");
        var draft = Draft();
        var contents = failure switch
        {
            "size" => new string(' ', 32769),
            "duplicate" => "{\"schemaVersion\":1,\"schemaVersion\":1}",
            _ => JsonSerializer.Serialize(draft with { AccountRealm = "foreign-account-realm" }, ClientReleaseSigner.JsonOptions)
        };
        await File.WriteAllTextAsync(input, contents);
        Assert.NotEqual(0, await fixture.RunAsync("sign", "--input", input, "--private-key", fixture.PrivatePath,
            "--key-id", "ephemeral-test", "--output", destination));
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public async Task Sign_refuses_existing_output_without_replacing_signed_catalog()
    {
        using var fixture = new PublisherFixture();
        await fixture.GenerateKeyAsync();
        var input = Path.Combine(fixture.DirectoryPath, "draft.json");
        var destination = Path.Combine(fixture.DirectoryPath, "envelope.json");
        await File.WriteAllTextAsync(input, JsonSerializer.Serialize(Draft(), ClientReleaseSigner.JsonOptions));
        await File.WriteAllTextAsync(destination, "existing-signed-catalog");
        Assert.NotEqual(0, await fixture.RunAsync("sign", "--input", input, "--private-key", fixture.PrivatePath,
            "--key-id", "ephemeral-test", "--output", destination));
        Assert.Equal("existing-signed-catalog", await File.ReadAllTextAsync(destination));
    }

    private static ClientReleaseManifest Draft()
    {
        var now = DateTimeOffset.UtcNow;
        const string version = "0.1.0-preview.62";
        return new(1, "videograbber", "preview", 7, now, now.AddDays(7), "videograbber-main",
            new(new Uri("https://api.example.com/"), new Uri("https://www.example.com/")),
            new(version, now, "Ephemeral test release.",
                new(version, new Uri("https://api.example.com/download/windows/releases/0.1.0-preview.62/setup"), 4, new string('a', 64)), null));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("generate-key", "--private-key", "missing")]
    [InlineData("generate-key", "--private-key", "a", "--private-key", "b")]
    public async Task Invalid_commands_return_usage_error(params string[] args)
    {
        using var fixture = new PublisherFixture();
        Assert.Equal(2, await fixture.RunAsync(args));
    }

    private sealed class PublisherFixture : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "VideoGrabber-PublisherTests", Guid.NewGuid().ToString("N"));
        public string PrivatePath => Path.Combine(DirectoryPath, "ephemeral-private.pem");
        public string PublicPath => Path.Combine(DirectoryPath, "public.pem");
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public PublisherFixture() => Directory.CreateDirectory(DirectoryPath);
        public Task<int> RunAsync(params string[] args) => ReleasePublisher.RunAsync(args, Output, Error);
        public async Task GenerateKeyAsync() => Assert.Equal(0, await RunAsync("generate-key", "--private-key", PrivatePath,
            "--public-key", PublicPath, "--key-id", "ephemeral-test"));
        public void Dispose() => Directory.Delete(DirectoryPath, true);
    }
}
