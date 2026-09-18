using VideoGrabber.Platform.Worker;

var builder = Host.CreateApplicationBuilder(args);

var apiUrl = Environment.GetEnvironmentVariable("VG_PLATFORM_API_URL");
var workerToken = Environment.GetEnvironmentVariable("VG_SERVER_WORKER_TOKEN");
var workerIdRaw = Environment.GetEnvironmentVariable("VG_WORKER_ID");
var proxyRaw = Environment.GetEnvironmentVariable("VG_EGRESS_PROXY_URI");
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

builder.Services.AddSingleton(new WorkerToolLocator());
builder.Services.AddSingleton<BoundedProcessRunner>();
builder.Services.AddHttpClient("platform", client =>
{
    client.BaseAddress = apiUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton(sp => new WorkerApiClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("platform"),
    workerId,
    workerToken));
builder.Services.AddSingleton<IWorkerSourceResolver>(
    sp => sp.GetRequiredService<WorkerApiClient>());
builder.Services.AddSingleton<ArtifactVerifier>();
builder.Services.AddSingleton<IMediaJobExecutor>(sp => new MediaJobExecutor(
    sp.GetRequiredService<BoundedProcessRunner>(),
    sp.GetRequiredService<WorkerToolLocator>(),
    sp.GetRequiredService<ArtifactVerifier>(),
    sp.GetRequiredService<IWorkerSourceResolver>(),
    jobRoot,
    proxyUri));
builder.Services.AddHostedService<ServerWorkerService>();

await builder.Build().RunAsync();
