using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Jobs;

public static class SourceEndpoints
{
    public static IEndpointRouteBuilder MapSourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/sources/analyze", AnalyzeAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> AnalyzeAsync(
        AnalyzeSourceRequest request,
        HttpContext http,
        SourceAnalysisService analysis,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(http.User.FindFirst("account_id")?.Value, out var accountId))
            return Results.Unauthorized();
        try
        {
            return Results.Ok(await analysis.AnalyzeAsync(accountId, request.Source, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        { return Results.BadRequest(new { code = "source_target_rejected" }); }
        catch (InvalidDataException ex)
        { return Results.BadRequest(new { code = "source_analysis_failed", detail = ex.Message }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_source", detail = ex.Message }); }
    }
}