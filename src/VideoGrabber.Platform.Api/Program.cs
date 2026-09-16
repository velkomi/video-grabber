using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Npgsql;
using VideoGrabber.Platform.Api.Accounts;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication("RejectAll")
    .AddScheme<AuthenticationSchemeOptions, RejectAllAuthenticationHandler>(
        "RejectAll", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var dsn = configuration.GetConnectionString("PlatformApi")
        ?? configuration["VG_PLATFORM_API_DSN"]
        ?? throw new InvalidOperationException("Platform API database DSN is not configured.");
    return NpgsqlDataSource.Create(dsn);
});
builder.Services.AddSingleton<IAccountStore, AccountStore>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var version = context.Request.Headers["X-VideoGrabber-Api-Version"].ToString();
    if (!string.IsNullOrWhiteSpace(version) && version != "1")
    {
        context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Client upgrade required",
            status = StatusCodes.Status426UpgradeRequired,
            code = "client_upgrade_required"
        });
        return;
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapAccountEndpoints();
app.Run();

public partial class Program;

file sealed class RejectAllAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());
}
