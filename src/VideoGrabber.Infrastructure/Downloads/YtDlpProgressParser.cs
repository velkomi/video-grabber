using System.Globalization;
using System.Text.RegularExpressions;
using VideoGrabber.Core.Downloads;

namespace VideoGrabber.Infrastructure.Downloads;

public static partial class YtDlpProgressParser
{
    public static bool TryParse(string line, out DownloadProgress progress)
    {
        if (TryParseMachineProgress(line, out progress))
        {
            return true;
        }

        var match = ProgressLine().Match(line);
        if (!match.Success ||
            !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
        {
            progress = new DownloadProgress(null, string.Empty);
            return false;
        }

        progress = new DownloadProgress(
            percent,
            "Загрузка",
            match.Groups[2].Value.Trim(),
            match.Groups[3].Value.Trim());
        return true;
    }

    private static bool TryParseMachineProgress(string line, out DownloadProgress progress)
    {
        const string prefix = "videograbber:";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            progress = new DownloadProgress(null, string.Empty);
            return false;
        }

        var fields = line[prefix.Length..].Split('|');
        if (fields.Length != 7)
        {
            progress = new DownloadProgress(null, string.Empty);
            return false;
        }

        var downloadedBytes = ParseNumber(fields[0]);
        var totalBytes = ParseNumber(fields[1]) ?? ParseNumber(fields[2]);
        var fragmentIndex = ParseNumber(fields[3]);
        var fragmentCount = ParseNumber(fields[4]);

        double? percent = null;
        if (downloadedBytes is >= 0 && totalBytes is > 0)
        {
            percent = downloadedBytes.Value / totalBytes.Value * 100d;
        }
        else if (fragmentIndex is >= 0 && fragmentCount is > 0)
        {
            percent = fragmentIndex.Value / fragmentCount.Value * 100d;
        }

        if (percent is null)
        {
            progress = new DownloadProgress(null, string.Empty);
            return false;
        }

        progress = new DownloadProgress(
            Math.Clamp(percent.Value, 0, 100),
            "Загрузка",
            Normalize(fields[5]),
            Normalize(fields[6]));
        return true;
    }

    private static double? ParseNumber(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    private static string? Normalize(string value)
    {
        var normalized = value.Trim();
        return normalized is "" or "NA" or "None" ? null : normalized;
    }

    [GeneratedRegex(@"^(?:download:)?\s*([0-9]+(?:\.[0-9]+)?)%\|([^|]*)\|([^|]*)\s*$")]
    private static partial Regex ProgressLine();
}
