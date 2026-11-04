using Avalonia.Headless.XUnit;
using ClearPDF.Mac.Views;
using Xunit;

namespace ClearPDF.Mac.Tests;

public class MacSoakTests
{
    [AvaloniaFact]
    [Trait("Category", "Soak")]
    public void Osx_x64_soak_scroll_zoom_multiselect_save_as()
    {
        var seconds = ReadInt("CLEARPDF_SOAK_SECONDS", 2);
        var maxMb = ReadInt("CLEARPDF_SOAK_MAX_MB", SoakSession.DefaultMaxWorkingSetMb);
        var outDir = Environment.GetEnvironmentVariable("CLEARPDF_SOAK_OUT")
            ?? Path.Combine(Path.GetTempPath(), "clearpdf-soak-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        var pdf = Path.Combine(outDir, "multi-page.pdf");
        MultiPagePdfWriter.Write(pdf, 8);
        var second = Path.Combine(outDir, "multi-page-b.pdf");
        File.Copy(pdf, second, overwrite: true);
        var texts = Enumerable.Range(1, 8).Select(i => $"Page {i} hello unique").ToArray();

        var previous = MainWindow.RecentsOverride;
        MainWindow.RecentsOverride = new ClearPDF.Services.RecentFilesService(
            Path.Combine(outDir, "recent.json"));
        MainWindow? window = null;
        try
        {
            window = new MainWindow { Width = 1100, Height = 720 };
            var report = SoakSession.Run(window, new SoakOptions
            {
                OutDir = outDir,
                PdfPath = pdf,
                SecondPdfPath = second,
                PageTexts = texts,
                Seconds = seconds,
                MaxWorkingSetMb = maxMb
            });

            Assert.True(report.Ok, report.Error);
            Assert.False(report.Crashed);
            Assert.True(report.PeakWorkingSetMb <= maxMb, $"memory {report.PeakWorkingSetMb:0} MB");
            Assert.Contains("empty.png", report.Screenshots);
            Assert.Contains("intro.png", report.Screenshots);
            Assert.Contains("reader.png", report.Screenshots);
            Assert.Contains("tabs.png", report.Screenshots);
            Assert.Contains("scroll", report.Actions);
            Assert.Contains("zoom", report.Actions);
            Assert.Contains("multi-select", report.Actions);
            Assert.Contains("save-as", report.Actions);
            foreach (var name in new[] { "empty.png", "intro.png", "reader.png", "tabs.png" })
                Assert.True(new FileInfo(Path.Combine(outDir, name)).Length > 0, name);
            Assert.True(File.Exists(Path.Combine(outDir, "smoke-report.json")));
        }
        finally
        {
            window?.Close();
            MainWindow.RecentsOverride = previous;
        }
    }

    private static int ReadInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0
            ? value
            : fallback;
}
