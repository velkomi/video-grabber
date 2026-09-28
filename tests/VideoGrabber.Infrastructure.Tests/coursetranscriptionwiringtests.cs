namespace VideoGrabber.Infrastructure.Tests;

public sealed class CourseTranscriptionWiringTests
{
    [Fact]
    public void Whole_course_uses_ordered_background_whisper_and_same_name_txt()
    {
        var root = FindRepoRoot();
        var transcription = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseTranscription.cs"));
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));
        var browser = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var shell = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));

        Assert.Contains("Queue<string>", transcription);
        Assert.Contains("Task.Run(", transcription);
        Assert.Contains("RunCourseTranscriptionWorkerAsync", transcription);
        Assert.Contains("_courseTranscriptionCurrentMedia", transcription);
        Assert.Contains("_courseTranscriptionActiveStopwatch", transcription);
        Assert.Contains("SetCourseTranscriptionPauseState", transcription);
        Assert.Contains("operation.pause", shell);
        Assert.Contains("Видео {Math.Max(1, activeOrdinal)} из {total}", transcription);
        Assert.Contains("Прошло: ", transcription);
        Assert.Contains("startWorker: false", transcription);
        Assert.Contains("ResolveCourseTranscriptionLanguage", transcription);
        Assert.Contains("? \"ru\"", transcription);
        Assert.Contains("Path.GetFileNameWithoutExtension(mediaPath) + \".txt\"", transcription);
        Assert.Contains("generatedSrt", transcription);
        Assert.Contains("requireSubtitles: false", transcription);
        Assert.Contains("File.Delete(path)", transcription);
        Assert.Contains("CourseTranscriptionSettingsCanChange", transcription);
        Assert.Contains("_courseTranscriptionItemCancellation", transcription);
        Assert.Contains("RequestCourseTranscriptionRestartForSettingsChange", transcription);
        Assert.Contains("CleanupCourseTranscriptionWorkingDirectories", transcription);
        Assert.Contains("CleanupStaleCourseTranscriptionWorkspaces(courseRoot)", transcription);
        Assert.Contains("Course mode never needs preserved Whisper evidence directories.", transcription);
        Assert.Contains("await WaitIfPausedAsync(token)", transcription);
        Assert.Contains("\".vg-asr-*\"", course);
        Assert.Contains("\".vg-course-transcript-*\"", course);
        Assert.Contains("OrderBy(", transcription);
        Assert.Contains("CourseTranscriptLooksReady", transcription);
        Assert.Contains("EnqueueCourseTranscription(path)", course);
        Assert.Contains("WaitForCourseTranscriptionAsync", course);
        Assert.Contains("ScheduleCompletionActionAfterDownloads", course);
        Assert.Contains("Транскрибировать видео курса в TXT", browser);
        Assert.Contains("CreateWhisperModelSelector", browser);
        var modelUi = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.TranscriptionModels.cs"));
        Assert.Contains("Источник модели", modelUi);
        Assert.Contains("CourseTranscriptionSettingsCanChange", modelUi);
        Assert.Contains("Модель транскрибации изменена.", modelUi);
        Assert.Contains("editableCourseSettings", course);
        var mediaActions = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.MediaActions.cs"));
        Assert.Contains("Язык транскрибации изменён.", mediaActions);
        Assert.Contains("EnsureWhisperModelAvailableAsync", transcription);
        Assert.Contains("IsChecked = _preferences.CourseAutoTranscription", browser);
        Assert.Contains("может увеличить общее время на несколько часов", browser);
        Assert.Contains("_courseTranscriptionEnabledForRun", course);
        Assert.Contains("Транскрибация была выключена", course);
        Assert.Contains("Фоновая транскрибация курса", browser);
        Assert.Contains("В очереди:", transcription);
        Assert.Contains("Развернуть дополнительные возможности", shell);
        Assert.Contains("advancedPanel.Visibility = Visibility.Collapsed", shell);
        Assert.Contains("Свернуть дополнительные возможности", shell);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VideoGrabber.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
