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
#if VIDEOGRABBER_PRESENTATION_PROBE
        DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
        DebugSettings.XamlResourceReferenceFailed += (_, resource) =>
            System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetEnvironmentVariable("VIDEOGRABBER_PRESENTATION_ROOT")!, "resources.txt"), resource.Message + "\n");
#endif
        InitializeComponent();
        AppDiagnostics.Write("App XAML initialized");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppDiagnostics.Write("OnLaunched started");
#if VIDEOGRABBER_PRESENTATION_PROBE
        if (Environment.GetEnvironmentVariable("VIDEOGRABBER_PROBE_MINIMAL") == "1")
        {
            StartNativeBaseline();
            return;
        }
#endif
        if (_window is null)
        {
            _window = new MainWindow();
            AppDiagnostics.Write("MainWindow constructed");
        }
        else
        {
            AppDiagnostics.Write("Existing MainWindow reused");
        }
#if VIDEOGRABBER_PRESENTATION_PROBE
        ((MainWindow)_window).PrepareStudioProbePage();
#endif
        _window.Activate();
        AppDiagnostics.Write("MainWindow activated");
#if VIDEOGRABBER_PRESENTATION_PROBE
        ((MainWindow)_window).StartStudioPresentationProbe();
#endif
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
