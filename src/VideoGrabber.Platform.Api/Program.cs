using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using VideoGrabber.Platform.Api.Accounts;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

var builder = WebApplication.CreateBuilder(args);
var sessionJwt = SessionJwtOptions.FromConfiguration(builder.Configuration);

builder.Services.AddSingleton(sessionJwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = sessionJwt.Issuer,
            ValidateAudience = true,
            ValidAudience = sessionJwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(sessionJwt.SigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IReadOnlyDictionary<string, BrokerPartitionOptions>>(sp =>
    BrokerPartitionConfiguration.Load(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<IBrokerCodeExchange, UnavailableBrokerCodeExchange>();
builder.Services.AddSingleton<IBrokerSigningKeySource, HttpBrokerSigningKeySource>();
builder.Services.AddSingleton<IBrokerUserInfoSource, HttpBrokerUserInfoSource>();
builder.Services.AddSingleton<IBrokerBindingStore>(sp => sp.GetRequiredService<SessionStore>());
builder.Services.AddSingleton<IBrokerTokenValidator, BrokerTokenValidator>();
builder.Services.AddSingleton<ProviderFlow>();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var dsn = configuration.GetConnectionString("PlatformApi")
        ?? configuration["VG_PLATFORM_API_DSN"]
        ?? throw new InvalidOperationException("Platform API database DSN is not configured.");
    return NpgsqlDataSource.Create(dsn);
});
builder.Services.AddSingleton<IAccountStore, AccountStore>();
builder.Services.AddSingleton<IdentityLinkService>();
builder.Services.AddSingleton<IIdentityAccountResolver, IdentityAccountResolver>();

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
app.MapIdentityEndpoints();
app.MapSessionEndpoints();
app.Run();

public partial class Program;
