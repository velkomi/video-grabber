using VideoGrabber.Infrastructure.Settings;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class AppThemeSettingsTests
{
    [Theory]
    [InlineData("system", AppThemeMode.System)]
    [InlineData("light", AppThemeMode.Light)]
    [InlineData("dark", AppThemeMode.Dark)]
    public void Parse_accepts_supported_modes(string raw, AppThemeMode expected)
        => Assert.Equal(expected, AppThemeSettings.Parse(raw));

    [Fact]
    public void Invalid_mode_falls_back_to_system()
        => Assert.Equal(AppThemeMode.System, AppThemeSettings.Parse("neon"));

    [Fact]
    public void Save_and_load_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), "VG-theme-" + Guid.NewGuid().ToString("N"), "ui-settings.json");
        try
        {
            AppThemeSettings.Save(path, AppThemeMode.Dark);
            Assert.Equal(AppThemeMode.Dark, AppThemeSettings.Load(path));
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
