using System.Globalization;

namespace VideoGrabber.Core.Downloads;

public readonly record struct DownloadQuality(int? MaximumHeight)
{
    public static bool TryParse(string? value, out DownloadQuality quality)
    {
        quality = default;
        if (value == "best") return true;
        if (value == "4K") { quality = new(2160); return true; }
        if (value is null || value.Length < 2 || value[^1] != 'p') return false;
        var digits = value.AsSpan(0, value.Length - 1);
        foreach (var digit in digits)
            if (digit is < '0' or > '9') return false;
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var height) || height <= 0)
            return false;
        quality = new(height);
        return true;
    }

    public string ToTag() => MaximumHeight is null ? "best"
        : MaximumHeight > 0 ? MaximumHeight.Value.ToString(CultureInfo.InvariantCulture) + "p"
        : throw new InvalidOperationException("Quality height must be positive.");
}
