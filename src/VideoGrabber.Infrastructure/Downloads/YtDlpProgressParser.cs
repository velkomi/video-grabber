using System.Globalization;
using System.Text.RegularExpressions;
using VideoGrabber.Core.Downloads;

namespace VideoGrabber.Infrastructure.Downloads;

public static partial class YtDlpProgressParser
{
    public static bool TryParse(string line, out DownloadProgress progress)
    {
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

    [GeneratedRegex(@"^(?:download:)?\s*([0-9]+(?:\.[0-9]+)?)%\|([^|]*)\|([^|]*)\s*$")]
    private static partial Regex ProgressLine();
}
