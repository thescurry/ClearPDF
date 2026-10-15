using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClearPDF.Desktop.Services;
using ClearPDF.Desktop.Views;
using ClearPDF.Helpers;

namespace ClearPDF.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            var window = new MainWindow();
            desktop.MainWindow = window;
            desktop.Startup += (_, e) =>
            {
                var pdf = CommandLinePdfArgs.TryGetFirstExistingPdf(e.Args);
                if (pdf != null)
                    Dispatcher.UIThread.Post(() => window.OpenPath(pdf));
            };
        }

        if (ApplicationLifetime is IActivatableLifetime activatable)
        {
            activatable.Activated += OnActivated;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                DesktopLog.Error("Unhandled AppDomain exception", ex);
        };

        DesktopLog.LogAppStart();
        base.OnFrameworkInitializationCompleted();
    }

    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e is FileActivatedEventArgs files)
        {
            var paths = files.Files.Select(f => f.Path.LocalPath);
            var pdf = CommandLinePdfArgs.TryGetFirstExistingPdf(paths);
            if (pdf != null && MainWindow is { } window)
                Dispatcher.UIThread.Post(() => window.OpenPath(pdf));
        }
    }

    private MainWindow? MainWindow =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

    private void AboutClicked(object? sender, EventArgs e)
    {
        MainWindow?.ShowAbout();
    }
}
