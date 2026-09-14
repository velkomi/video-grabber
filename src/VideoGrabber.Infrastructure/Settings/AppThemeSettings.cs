using System.Text.Json;

namespace VideoGrabber.Infrastructure.Settings;

public enum AppThemeMode { System, Light, Dark }

public static class AppThemeSettings
{
    private sealed record Payload(string Theme);

    public static AppThemeMode Parse(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "light" => AppThemeMode.Light,
            "dark" => AppThemeMode.Dark,
            _ => AppThemeMode.System
        };

    public static AppThemeMode Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return AppThemeMode.System;
            var payload = JsonSerializer.Deserialize<Payload>(File.ReadAllText(path));
            return Parse(payload?.Theme);
        }
        catch { return AppThemeMode.System; }
    }

    public static void Save(string path, AppThemeMode mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new Payload(mode.ToString().ToLowerInvariant()));
        File.WriteAllText(path, json);
    }
}
