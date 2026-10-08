using System.Text.Json;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using VideoGrabber.Platform.Api.Operations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VideoGrabber.Platform.Core.Operations;

namespace VideoGrabber.Platform.Tests;

public sealed class TelemetryTests
{
    [Fact]
    public async Task Middleware_resolves_route_after_real_routing_pipeline()
    {
        var logger = new CapturingLogger();
        using var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services => services.AddRouting())
            .Configure(app =>
            {
                app.Use((context, next) => RequestTelemetry.InvokeAsync(context, next, logger));
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapGet("/test/{value}", context =>
                {
                    context.Response.StatusCode = 503;
                    return Task.CompletedTask;
                }));
            })).StartAsync();
        using var client = host.GetTestClient();
        using var response = await client.GetAsync("/test/private-id?token=secret");
        Assert.Contains("/test/{value}", logger.Lines.Last());
        Assert.Contains("503", logger.Lines.Last());
        Assert.DoesNotContain("secret", string.Join("\n", logger.Lines));
        Assert.DoesNotContain("private-id", string.Join("\n", logger.Lines));
    }

    [Fact]
    public async Task Middleware_records_failed_route_template_and_does_not_log_request_secrets()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/v1/jobs/private-user-id";
        context.Request.QueryString = new QueryString("?token=secret");
        context.Request.Headers["Authorization"] = "Bearer secret";
        context.Request.Headers["X-Correlation-Id"] = "email@example.com";
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("/v1/jobs/{jobId:guid}"), 0, EndpointMetadataCollection.Empty, "Job"));
        var logger = new CapturingLogger();
        await Assert.ThrowsAsync<InvalidOperationException>(() => RequestTelemetry.InvokeAsync(context,
            () => throw new InvalidOperationException("secret"), logger));
        Assert.Equal(2, logger.Lines.Count);
        var text = string.Join("\n", logger.Lines);
        Assert.Contains("/v1/jobs/{jobId:guid}", text);
        Assert.Contains("failed", text);
        Assert.DoesNotContain("private-user-id", text);
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain("example.com", text);
        Assert.Equal(context.TraceIdentifier, context.Response.Headers["X-Correlation-Id"].ToString());
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    [Fact]
    public async Task Middleware_bounds_method_labels_and_links_only_typed_job_ids()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "CLIENT_UNIQUE_METHOD";
        var jobId = Guid.NewGuid();
        var logger = new CapturingLogger();
        await RequestTelemetry.InvokeAsync(context, () =>
        {
            context.Items[RequestTelemetry.JobIdKey] = jobId;
            return Task.CompletedTask;
        }, logger);
        Assert.Contains("OTHER", logger.Lines.Last());
        Assert.Contains(jobId.ToString(), logger.Lines.Last());
        Assert.DoesNotContain("CLIENT_UNIQUE_METHOD", string.Join("\n", logger.Lines));
    }
    [Fact]
    public void Correlation_rejects_untrusted_text_and_preserves_safe_id()
    {
        var id = Guid.NewGuid().ToString("N");
        Assert.Equal(id, PlatformTelemetry.Correlation(id));
        foreach (var text in new[] { "token=secret", "email@example.com", "bad\r\nvalue", new string('a', 65) })
            Assert.Matches("^[a-f0-9]{32}$", PlatformTelemetry.Correlation(text));
    }

    [Fact]
    public void Events_are_serializable_without_exception_message_or_url()
    {
        var item = PlatformTelemetry.Create("api", "http.request", "failed", Guid.NewGuid().ToString("N"),
            12, "POST", "/v1/jobs/{id}", 503, new InvalidOperationException("token=secret"));
        var json = JsonSerializer.Serialize(item);
        Assert.DoesNotContain("secret", json);
        Assert.Contains("InvalidOperationException", json);
        Assert.Equal("UNKNOWN", item.Revision);
    }
}
