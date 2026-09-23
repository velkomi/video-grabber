namespace VideoGrabber.Platform.Core.Access;

public sealed record ProductPlan(
    string Id,
    long MonthlyRubMinor,
    long? LifetimeDownloadLimit,
    long? DailyDownloadLimit,
    bool UnlimitedIndividualDownloads,
    bool CanDownloadCourse);

public static class ProductPlans
{
    public const string FreeId = "free";
    public const string StartId = "start";
    public const string UnlimitedVideoId = "unlimited_video";
    public const string FullCourseId = "full_course";

    public static readonly ProductPlan Free =
        new(FreeId, 0, 10, null, false, false);

    public static readonly ProductPlan Start =
        new(StartId, 150_000, null, 10, false, false);

    public static readonly ProductPlan UnlimitedVideo =
        new(UnlimitedVideoId, 250_000, null, null, true, false);

    public static readonly ProductPlan FullCourse =
        new(FullCourseId, 500_000, null, null, true, true);

    public static IReadOnlyList<ProductPlan> All { get; } =
        [Free, Start, UnlimitedVideo, FullCourse];

    public static ProductPlan? Find(string? id)
        => id switch
        {
            FreeId => Free,
            StartId => Start,
            UnlimitedVideoId => UnlimitedVideo,
            FullCourseId => FullCourse,
            _ => null
        };

    public static int Rank(string? id)
        => id switch
        {
            FreeId => 0,
            StartId => 1,
            UnlimitedVideoId => 2,
            FullCourseId => 3,
            _ => -1
        };
}
