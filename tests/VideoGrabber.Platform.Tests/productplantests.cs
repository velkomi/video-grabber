using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Access;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class ProductPlanTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Catalog_has_requested_stable_plan_semantics()
    {
        Assert.Collection(
            ProductPlans.All,
            plan =>
            {
                Assert.Equal(ProductPlans.FreeId, plan.Id);
                Assert.Equal(0, plan.MonthlyRubMinor);
                Assert.Equal(10, plan.LifetimeDownloadLimit);
                Assert.Null(plan.DailyDownloadLimit);
                Assert.False(plan.UnlimitedIndividualDownloads);
                Assert.False(plan.CanDownloadCourse);
            },
            plan =>
            {
                Assert.Equal(ProductPlans.StartId, plan.Id);
                Assert.Equal(150_000, plan.MonthlyRubMinor);
                Assert.Null(plan.LifetimeDownloadLimit);
                Assert.Equal(10, plan.DailyDownloadLimit);
                Assert.False(plan.UnlimitedIndividualDownloads);
                Assert.False(plan.CanDownloadCourse);
            },
            plan =>
            {
                Assert.Equal(ProductPlans.UnlimitedVideoId, plan.Id);
                Assert.Equal(250_000, plan.MonthlyRubMinor);
                Assert.Null(plan.LifetimeDownloadLimit);
                Assert.Null(plan.DailyDownloadLimit);
                Assert.True(plan.UnlimitedIndividualDownloads);
                Assert.False(plan.CanDownloadCourse);
            },
            plan =>
            {
                Assert.Equal(ProductPlans.FullCourseId, plan.Id);
                Assert.Equal(500_000, plan.MonthlyRubMinor);
                Assert.Null(plan.LifetimeDownloadLimit);
                Assert.Null(plan.DailyDownloadLimit);
                Assert.True(plan.UnlimitedIndividualDownloads);
                Assert.True(plan.CanDownloadCourse);
            });
    }

    [Fact]
    public void Owner_admin_is_unlimited_and_can_download_course()
    {
        var account = new AccountProfile(Guid.NewGuid(), "owner_admin", false, [], null);

        var access = AccessEvaluator.Evaluate(account, [], Now);

        Assert.True(access.CanDownload);
        Assert.True(access.Unlimited);
        Assert.True(access.CanDownloadCourse);
        Assert.Null(access.PlanId);
    }

    [Fact]
    public void Legacy_timed_grant_preserves_download_but_does_not_gain_course()
    {
        var account = new AccountProfile(Guid.NewGuid(), "user", false, [], null);
        var grant = TimedGrant(planId: null);

        var access = AccessEvaluator.Evaluate(account, [grant], Now);

        Assert.True(access.CanDownload);
        Assert.False(access.Unlimited);
        Assert.False(access.CanDownloadCourse);
        Assert.Null(access.PlanId);
        Assert.Equal("time_access", access.Reason);
    }

    [Fact]
    public void Free_plan_exposes_lifetime_remaining_and_denies_course()
    {
        var account = new AccountProfile(Guid.NewGuid(), "guest", false, [], null);
        var grant = new Grant(
            Guid.NewGuid(), "credits", "adjustment", Now, null,
            10, 0, false, Now)
        {
            PlanId = ProductPlans.FreeId
        };

        var access = AccessEvaluator.Evaluate(account, [grant], Now);

        Assert.Equal(ProductPlans.FreeId, access.PlanId);
        Assert.Equal(10, access.RemainingDownloads);
        Assert.True(access.CanDownload);
        Assert.False(access.CanDownloadCourse);
        Assert.False(access.Unlimited);
    }

    [Fact]
    public void Start_plan_has_daily_quota_semantics_and_denies_course()
    {
        var account = new AccountProfile(Guid.NewGuid(), "user", false, [], null);

        var access = AccessEvaluator.Evaluate(
            account, [TimedGrant(ProductPlans.StartId)], Now);

        Assert.Equal(ProductPlans.StartId, access.PlanId);
        Assert.True(access.CanDownload);
        Assert.False(access.Unlimited);
        Assert.False(access.CanDownloadCourse);
        Assert.Equal("start_daily_quota_online", access.Reason);
    }

    [Fact]
    public void Unlimited_video_plan_is_unlimited_but_denies_course()
    {
        var account = new AccountProfile(Guid.NewGuid(), "user", false, [], null);

        var access = AccessEvaluator.Evaluate(
            account, [TimedGrant(ProductPlans.UnlimitedVideoId)], Now);

        Assert.Equal(ProductPlans.UnlimitedVideoId, access.PlanId);
        Assert.True(access.Unlimited);
        Assert.False(access.CanDownloadCourse);
    }

    [Fact]
    public void Full_course_plan_is_unlimited_and_allows_course()
    {
        var account = new AccountProfile(Guid.NewGuid(), "user", false, [], null);

        var access = AccessEvaluator.Evaluate(
            account, [TimedGrant(ProductPlans.FullCourseId)], Now);

        Assert.Equal(ProductPlans.FullCourseId, access.PlanId);
        Assert.True(access.Unlimited);
        Assert.True(access.CanDownloadCourse);
    }

    private static Grant TimedGrant(string? planId)
        => new(
            Guid.NewGuid(), "time", "purchase", Now,
            Now.AddDays(30), 0, 0, false, Now)
        {
            PlanId = planId
        };
}
