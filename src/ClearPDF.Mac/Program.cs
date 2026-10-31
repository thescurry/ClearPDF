using Avalonia;
using ClearPDF.Services;
using System;
using System.Globalization;
using System.IO;

namespace ClearPDF.Mac;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (TrySmokeRender(args, out var code))
            return code;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Headless x64 load check: open a PDF with Docnet and write page 0 rasters.
    /// Usage: ClearPDF --smoke-render &lt;pdf&gt; &lt;output.bgra&gt;
    /// </summary>
    internal static bool TrySmokeRender(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length < 3 || !string.Equals(args[0], "--smoke-render", StringComparison.Ordinal))
            return false;

        try
        {
            var pdf = args[1];
            var dest = args[2];
            using var doc = new DocnetPdfDocumentFactory().Open(pdf);
            var buffer = doc.RenderPage(0, 1.0);
            if (buffer == null)
                throw new InvalidOperationException("RenderPage returned no raster.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dest)) ?? ".");
            File.WriteAllBytes(dest, buffer.Bgra32);
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{buffer.Width}x{buffer.Height} stride={buffer.Stride} bytes={buffer.Bgra32.Length}"));
            exitCode = 0;
        }
        catch (Exception ex)
        {
            AppLog.Error("Smoke render failed", ex);
            Console.Error.WriteLine(ex.Message);
            exitCode = 1;
        }

        return true;
    }
}
