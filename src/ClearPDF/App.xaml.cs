using System.Windows;
using System.Windows.Threading;
using ClearPDF.Services;

namespace ClearPDF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppLog.LogAppStart();
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            AppLog.Error("Unhandled AppDomain exception", ex);
        else
            AppLog.Error($"Unhandled AppDomain exception: {e.ExceptionObject}");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Unhandled Dispatcher exception", e.Exception);
        // Leave e.Handled = false so WPF default crash behavior remains; log only.
    }
}
