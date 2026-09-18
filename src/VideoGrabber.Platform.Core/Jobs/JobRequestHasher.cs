using System.Security.Cryptography;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Core.Jobs;

public static class JobRequestHasher
{
    public static string Hash(CreateJob request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("intentId", request.IntentId.ToString("D").ToLowerInvariant());
            writer.WriteString("kind", request.Kind);
            writer.WriteString("executor", request.Executor);
            if (request.DeviceId is Guid deviceId)
                writer.WriteString("deviceId", deviceId.ToString("D").ToLowerInvariant());
            else writer.WriteNull("deviceId");
            writer.WriteString("sourceId", request.SourceId);
            writer.WriteString("quality", request.Quality);
            writer.WritePropertyName("inputArtifactIds");
            writer.WriteStartArray();
            foreach (var artifactId in request.InputArtifactIds ?? [])
                writer.WriteStringValue(artifactId.ToString("D").ToLowerInvariant());
            writer.WriteEndArray();
            if (request.TrimStartMs is long trimStart) writer.WriteNumber("trimStartMs", trimStart);
            else writer.WriteNull("trimStartMs");
            if (request.TrimDurationMs is long trimDuration) writer.WriteNumber("trimDurationMs", trimDuration);
            else writer.WriteNull("trimDurationMs");
            writer.WriteEndObject();
        }
        return Convert.ToHexString(SHA256.HashData(memory.ToArray())).ToLowerInvariant();
    }
}