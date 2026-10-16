using Avalonia;
using ClearPDF.Pdfium;

namespace ClearPDF.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        DocnetPdfRasterDocumentFactory.HintNativeSearchPath(
            baseDir,
            Path.Combine(baseDir, "runtimes", "osx-x64", "native"),
            Path.Combine(baseDir, "runtimes", "osx", "native"));

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
