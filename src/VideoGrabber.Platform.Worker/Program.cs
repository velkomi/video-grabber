using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Worker;

var builder = Host.CreateApplicationBuilder(args);

var apiUrl = Environment.GetEnvironmentVariable("VG_PLATFORM_API_URL");
var workerToken = Environment.GetEnvironmentVariable("VG_SERVER_WORKER_TOKEN");
var workerIdRaw = Environment.GetEnvironmentVariable("VG_WORKER_ID");
var proxyRaw = Environment.GetEnvironmentVariable("VG_EGRESS_PROXY_URI");
var socialProxyRaw = Environment.GetEnvironmentVariable("VG_SOCIAL_EGRESS_PROXY_URI");
var potProviderRaw = Environment.GetEnvironmentVariable("VG_YOUTUBE_POT_PROVIDER_URL");
var denoPath = Environment.GetEnvironmentVariable("VG_DENO_PATH")
    ?? "/usr/local/bin/deno";
var jobRoot = Environment.GetEnvironmentVariable("VG_WORKER_JOB_ROOT")
    ?? "/var/lib/videograbber/jobs";

if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri)
    || apiUri.Scheme is not ("http" or "https"))
    throw new InvalidOperationException("VG_PLATFORM_API_URL is required.");
if (string.IsNullOrWhiteSpace(workerToken))
    throw new InvalidOperationException("VG_SERVER_WORKER_TOKEN is required.");
if (!Guid.TryParse(workerIdRaw, out var workerId) || workerId == Guid.Empty)
    throw new InvalidOperationException("VG_WORKER_ID must be a non-empty UUID.");
if (!Uri.TryCreate(proxyRaw, UriKind.Absolute, out var proxyUri)
    || proxyUri.Scheme is not ("http" or "https")
    || !string.IsNullOrEmpty(proxyUri.UserInfo))
    throw new InvalidOperationException("VG_EGRESS_PROXY_URI is required.");

Uri? socialProxyUri = null;
if (!string.IsNullOrWhiteSpace(socialProxyRaw))
{
    if (!Uri.TryCreate(socialProxyRaw, UriKind.Absolute, out socialProxyUri)
        || socialProxyUri.Scheme is not ("http" or "https" or "socks5" or "socks5h")
        || !string.IsNullOrEmpty(socialProxyUri.UserInfo))
        throw new InvalidOperationException("VG_SOCIAL_EGRESS_PROXY_URI is invalid.");
}

Uri? youtubePotProviderUri = null;
if (!string.IsNullOrWhiteSpace(potProviderRaw))
{
    if (!Uri.TryCreate(potProviderRaw, UriKind.Absolute, out youtubePotProviderUri)
        || youtubePotProviderUri.Scheme is not ("http" or "https")
        || !string.IsNullOrEmpty(youtubePotProviderUri.UserInfo))
        throw new InvalidOperationException("VG_YOUTUBE_POT_PROVIDER_URL is invalid.");
}

builder.Services.AddSingleton(new WorkerToolLocator());
builder.Services.AddSingleton<BoundedProcessRunner>();
builder.Services.AddHttpClient("platform", client =>
{
    client.BaseAddress = apiUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
var workerOperations = PlatformProtocol.CurrentWorkerOperations(
    serverAsrAvailable: !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable("VG_WORKER_WHISPER_MODEL")));

builder.Services.AddSingleton(sp => new WorkerApiClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("platform"),
    workerId,
    workerToken,
    workerOperations));
builder.Services.AddSingleton<IWorkerSourceResolver>(
    sp => sp.GetRequiredService<WorkerApiClient>());
builder.Services.AddSingleton<IWorkerArtifactResolver>(
    sp => sp.GetRequiredService<WorkerApiClient>());
builder.Services.AddSingleton<ArtifactVerifier>();
builder.Services.AddSingleton<IMediaJobExecutor>(sp => new MediaJobExecutor(
    sp.GetRequiredService<BoundedProcessRunner>(),
    sp.GetRequiredService<WorkerToolLocator>(),
    sp.GetRequiredService<ArtifactVerifier>(),
    sp.GetRequiredService<IWorkerSourceResolver>(),
    sp.GetRequiredService<IWorkerArtifactResolver>(),
    jobRoot,
    proxyUri,
    socialProxyUri,
    Environment.GetEnvironmentVariable("VG_WORKER_WHISPER_MODEL"),
    youtubePotProviderUri,
    denoPath));
builder.Services.AddSingleton<ArtifactRetentionWorker>();
builder.Services.AddHostedService<ServerWorkerService>();
builder.Services.AddHostedService<ArtifactRetentionHostedService>();

await builder.Build().RunAsync();
