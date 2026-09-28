using System.Text.Json;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class SupabaseAuthTests
{
    private static readonly Uri Supabase =
        new("https://project.supabase.co/");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 22, 20, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("google")]
    [InlineData("email")]
    public void Confirmed_google_or_email_user_becomes_verified_identity(string provider)
    {
        var id = Guid.NewGuid();
        using var json = JsonDocument.Parse($$"""
            {
              "id":"{{id:D}}",
              "email":"User@Example.test",
              "email_confirmed_at":"2026-09-22T19:59:00Z",
              "app_metadata":{"provider":"{{provider}}"}
            }
            """);

        var identity = SupabaseAuthService.ParseVerifiedIdentity(
            json.RootElement, Supabase, Now);

        Assert.Equal(provider, identity.Provider);
        Assert.Equal(id.ToString("D"), identity.Subject);
        Assert.Equal("user@example.test", identity.VerifiedEmail);
        Assert.Equal("supabase_auth", identity.Assurance);
        Assert.True(identity.AllowProviderCoalescing);
        Assert.Equal("https://project.supabase.co/auth/v1/", identity.Issuer);
    }

    [Fact]
    public void Unconfirmed_email_is_rejected()
    {
        using var json = JsonDocument.Parse($$"""
            {
              "id":"{{Guid.NewGuid():D}}",
              "email":"user@example.test",
              "app_metadata":{"provider":"email"}
            }
            """);

        Assert.Throws<UnauthorizedAccessException>(() =>
            SupabaseAuthService.ParseVerifiedIdentity(
                json.RootElement, Supabase, Now));
    }

    [Fact]
    public void Unsupported_provider_is_rejected()
    {
        using var json = JsonDocument.Parse($$"""
            {
              "id":"{{Guid.NewGuid():D}}",
              "email":"user@example.test",
              "email_confirmed_at":"2026-09-22T19:59:00Z",
              "app_metadata":{"provider":"github"}
            }
            """);

        Assert.Throws<UnauthorizedAccessException>(() =>
            SupabaseAuthService.ParseVerifiedIdentity(
                json.RootElement, Supabase, Now));
    }

    [Fact]
    public async Task Same_supabase_user_google_then_email_resolves_one_videograbber_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var resolver = f.Service<IIdentityAccountResolver>();
        var subject = Guid.NewGuid().ToString("D");
        const string issuer = "https://project.supabase.co/auth/v1/";

        var google = await resolver.ResolveAsync(
            new VerifiedIdentity(
                issuer, "google", subject, "same@example.test",
                f.Clock.GetUtcNow(), "supabase_auth")
            {
                AllowProviderCoalescing = true
            },
            CancellationToken.None);

        var email = await resolver.ResolveAsync(
            new VerifiedIdentity(
                issuer, "email", subject, "same@example.test",
                f.Clock.GetUtcNow(), "supabase_auth")
            {
                AllowProviderCoalescing = true
            },
            CancellationToken.None);

        Assert.Equal(google.AccountId, email.AccountId);
        Assert.Equal("google", email.PrimaryAuthProvider);
        Assert.Contains("google", email.LinkedProviders);
        Assert.Contains("email", email.LinkedProviders);
    }

    [Fact]
    public async Task Same_email_but_different_supabase_user_ids_never_merge()
    {
        await using var f = await ApiFixture.StartAsync();
        var resolver = f.Service<IIdentityAccountResolver>();
        const string issuer = "https://project.supabase.co/auth/v1/";

        var a = await resolver.ResolveAsync(
            new VerifiedIdentity(
                issuer, "google", Guid.NewGuid().ToString("D"),
                "same@example.test", f.Clock.GetUtcNow(), "supabase_auth")
            {
                AllowProviderCoalescing = true
            },
            CancellationToken.None);
        var b = await resolver.ResolveAsync(
            new VerifiedIdentity(
                issuer, "email", Guid.NewGuid().ToString("D"),
                "same@example.test", f.Clock.GetUtcNow(), "supabase_auth")
            {
                AllowProviderCoalescing = true
            },
            CancellationToken.None);

        Assert.NotEqual(a.AccountId, b.AccountId);
    }
}
