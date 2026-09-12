using Microsoft.UI.Xaml;
using VideoGrabber.Core.Security;

namespace VideoGrabber.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        AppDiagnostics.Write("App constructor");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppDiagnostics.Write($"AppDomain exception: {args.ExceptionObject}");
        UnhandledException += (_, args) =>
        {
            AppDiagnostics.Write($"XAML exception: {args.Exception}");
            args.Handled = true;
        };
        InitializeComponent();
        AppDiagnostics.Write("App XAML initialized");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppDiagnostics.Write("OnLaunched started");
        if (_window is null)
        {
            _window = new MainWindow();
            AppDiagnostics.Write("MainWindow constructed");
        }
        else
        {
            AppDiagnostics.Write("Existing MainWindow reused");
        }
        _window.Activate();
        AppDiagnostics.Write("MainWindow activated");
    }
}

internal static class AppDiagnostics
{
    public static void Write(string message)
    {
        var failure = message.Contains("failed", StringComparison.OrdinalIgnoreCase) || message.Contains("exception", StringComparison.OrdinalIgnoreCase);
        VideoGrabber.Infrastructure.Diagnostics.DiagnosticHub.Log.Write("application", failure ? "failed" : "event", message);
    }
}
