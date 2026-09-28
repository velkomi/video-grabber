using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class CourseVideoBlockEvidenceTests
{
    [Fact]
    public void Counts_unique_GetCourse_video_wrappers()
    {
        const string html = """
            <div class="lite-block-live-wrapper o-lt-lesson o-lt-lesson-video" data-block-id="100">
              <div class="lt-block lt-view lessonVid01 lt-lesson lt-lesson-video" data-block-id="100"></div>
            </div>
            <div data-block-id="101" class="lite-block-live-wrapper o-lt-lesson-video"></div>
            <div class="lite-block-live-wrapper o-lt-lesson-video" data-block-id="102"></div>
            """;

        Assert.Equal(3, CourseVideoBlockEvidence.CountDeclaredVideoBlocks(html));
    }

    [Fact]
    public void Ignores_non_video_blocks()
    {
        const string html = """
            <div class="lite-block-live-wrapper o-lt-lesson-text" data-block-id="100"></div>
            <div class="lt-block lt-view lessonVid01 lt-lesson lt-lesson-video" data-block-id="101"></div>
            """;

        Assert.Equal(0, CourseVideoBlockEvidence.CountDeclaredVideoBlocks(html));
    }
}
