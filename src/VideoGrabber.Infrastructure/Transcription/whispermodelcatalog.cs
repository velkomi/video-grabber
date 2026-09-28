namespace VideoGrabber.Infrastructure.Transcription;

public sealed record WhisperModelProfile(
    string Id,
    string DisplayName,
    string FileName,
    long ExpectedBytes,
    string ExpectedSha256,
    string SourcePageUrl,
    string DownloadUrl,
    bool Bundled,
    string QualityLabel,
    string SpeedLabel,
    string Recommendation);

public static class WhisperModelCatalog
{
    public const string DefaultProfileId = "base";

    public static IReadOnlyList<WhisperModelProfile> Profiles { get; } =
    [
        new(
            "base",
            "Быстро — Base",
            "ggml-base.bin",
            147_951_465,
            "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe",
            "https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-base.bin",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
            true,
            "Базовое",
            "Самая высокая",
            "Короткие ролики, чистая речь, когда важнее скорость."),
        new(
            "small-q5_1",
            "Оптимально — Small",
            "ggml-small-q5_1.bin",
            190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb",
            "https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-small-q5_1.bin",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin",
            false,
            "Выше",
            "Средняя",
            "Рекомендуется для русских курсов, вебинаров и длинных записей."),
        new(
            "medium-q5_0",
            "Максимальное качество — Medium",
            "ggml-medium-q5_0.bin",
            539_212_467,
            "19fea4b380c3a618ec4723c3eef2eb785ffba0d0538cf43f8f235e7b3b34220f",
            "https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-medium-q5_0.bin",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium-q5_0.bin",
            false,
            "Максимальное из этих трёх",
            "Низкая",
            "Сложная речь, плохой звук и случаи, где качество важнее времени.")
    ];

    public static WhisperModelProfile Get(string? id)
        => Profiles.FirstOrDefault(profile =>
               profile.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
           ?? Profiles[0];

    public static string FormatSize(long bytes)
        => bytes >= 1024L * 1024 * 1024
            ? $"{bytes / (1024d * 1024 * 1024):0.00} ГБ"
            : $"{bytes / (1024d * 1024):0} МБ";
}
