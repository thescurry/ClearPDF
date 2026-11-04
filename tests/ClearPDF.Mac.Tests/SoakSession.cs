using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClearPDF.Mac.Views;
using ClearPDF.Services;

namespace ClearPDF.Mac.Tests;

internal static class SoakSession
{
    public const int DefaultMaxWorkingSetMb = 768;
    public const int CiSeconds = 180;

    public static SoakReport Run(MainWindow window, SoakOptions options)
    {
        Directory.CreateDirectory(options.OutDir);
        var process = Process.GetCurrentProcess();
        var peak = process.WorkingSet64;
        var started = Stopwatch.StartNew();
        var actions = new List<string>();
        var screenshots = new List<string>();

        try
        {
            window.Width = 1100;
            window.Height = 720;
            window.Show();
            Pump();

            Capture(window, options.OutDir, "empty.png", screenshots);
            SeedIntroRecents(window);
            Capture(window, options.OutDir, "intro.png", screenshots);

            var pages = Math.Max(6, options.PageTexts.Length);
            Open(window, options.PdfPath, options.PageTexts);
            actions.Add("open-reader");
            Pump();
            Capture(window, options.OutDir, "reader.png", screenshots);

            var secondPath = options.SecondPdfPath ?? options.PdfPath + ".tab.pdf";
            if (!File.Exists(secondPath))
                File.Copy(options.PdfPath, secondPath, overwrite: true);
            window.OpenDocumentForTests(
                OpenDoc(secondPath, options.PageTexts.Select(t => t + " b").ToArray()),
                secondPath);
            actions.Add("open-second-tab");
            Pump();
            Capture(window, options.OutDir, "tabs.png", screenshots);

            window.ExecuteShortcut(AppShortcut.PreviousTab);
            Pump();

            var dest = Path.Combine(options.OutDir, "soak-save-as.pdf");
            window.SaveAsPathOverride = dest;
            var deadline = started.Elapsed + TimeSpan.FromSeconds(options.Seconds);
            var step = 0;
            while (started.Elapsed < deadline)
            {
                switch (step % 4)
                {
                    case 0:
                        var page = step % pages;
                        window.GoToPageForTests(page);
                        window.ScrollToOffsetForTests(page * 120);
                        actions.Add("scroll");
                        break;
                    case 1:
                        window.ApplyZoomForTests(step % 2 == 0 ? 1.5 : 0.75);
                        actions.Add("zoom");
                        break;
                    case 2:
                        window.SelectThumbForTests(0, KeyModifiers.None);
                        window.SelectThumbForTests(Math.Min(2, pages - 1), KeyModifiers.Meta);
                        window.SelectThumbForTests(Math.Min(3, pages - 1), KeyModifiers.Shift);
                        actions.Add("multi-select");
                        break;
                    case 3:
                        window.ClickSaveAsForTests();
                        Pump();
                        if (window.SaveAsChoiceIsOpen)
                            window.ConfirmSaveAsChoiceForTests();
                        Pump();
                        actions.Add("save-as");
                        break;
                    default:
                        throw new InvalidOperationException($"unhandled soak step {step}");
                }

                peak = Math.Max(peak, process.WorkingSet64);
                if (BytesToMb(peak) > options.MaxWorkingSetMb)
                    throw new InvalidOperationException(
                        $"Working set {BytesToMb(peak):0} MB exceeded limit {options.MaxWorkingSetMb} MB");

                Pump();
                SleepSlice(options.Seconds);
                step++;
            }

            peak = Math.Max(peak, process.WorkingSet64);
            var report = new SoakReport
            {
                Ok = true,
                Seconds = options.Seconds,
                ElapsedSec = started.Elapsed.TotalSeconds,
                PeakWorkingSetMb = BytesToMb(peak),
                MaxWorkingSetMb = options.MaxWorkingSetMb,
                Crashed = false,
                Actions = actions,
                Screenshots = screenshots,
                Pdf = options.PdfPath,
                Pages = pages,
                SaveAsExists = File.Exists(dest)
            };
            WriteReport(options.OutDir, report);
            return report;
        }
        catch (Exception ex)
        {
            peak = Math.Max(peak, process.WorkingSet64);
            var report = new SoakReport
            {
                Ok = false,
                Seconds = options.Seconds,
                ElapsedSec = started.Elapsed.TotalSeconds,
                PeakWorkingSetMb = BytesToMb(peak),
                MaxWorkingSetMb = options.MaxWorkingSetMb,
                Crashed = true,
                Error = ex.Message,
                Actions = actions,
                Screenshots = screenshots,
                Pdf = options.PdfPath,
                Pages = options.PageTexts.Length
            };
            WriteReport(options.OutDir, report);
            throw;
        }
    }

    private static IPdfDocument Open(MainWindow window, string path, string[] texts)
    {
        var doc = OpenDoc(path, texts);
        window.OpenDocumentForTests(doc, path);
        return doc;
    }

    private static IPdfDocument OpenDoc(string path, string[] texts)
    {
        if (DocnetPdfDocumentFactory.AreNativesAvailable)
            return new DocnetPdfDocumentFactory().Open(path);
        return new FakePdfDocument(path, texts);
    }

    private static void SeedIntroRecents(MainWindow window)
    {
        if (MainWindow.RecentsOverride == null)
            return;
        MainWindow.RecentsOverride.Add("/docs/intro-sample.pdf");
        window.RefreshRecentListForTests();
        Pump();
    }

    private static void Capture(Window window, string outDir, string name, List<string> shots)
    {
        window.Show();
        window.InvalidateVisual();
        window.InvalidateMeasure();
        window.InvalidateArrange();
        window.UpdateLayout();
        Pump();

        var path = Path.Combine(outDir, name);
        using (var frame = window.CaptureRenderedFrame())
        {
            if (frame == null)
                throw new InvalidOperationException("headless frame capture returned null for " + name);
            using var stream = File.Create(path);
            frame.Save(stream);
        }

        var info = new FileInfo(path);
        if (!info.Exists || info.Length == 0)
            throw new InvalidOperationException("screenshot not written: " + path);
        shots.Add(name);
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private static void SleepSlice(int totalSeconds)
    {
        var ms = totalSeconds >= 30 ? 400 : 20;
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
            Pump();
    }

    private static double BytesToMb(long bytes) => bytes / (1024.0 * 1024.0);

    private static void WriteReport(string outDir, SoakReport report)
    {
        File.WriteAllText(
            Path.Combine(outDir, "smoke-report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}

internal sealed class SoakOptions
{
    public required string OutDir { get; init; }
    public required string PdfPath { get; init; }
    public string? SecondPdfPath { get; init; }
    public required string[] PageTexts { get; init; }
    public int Seconds { get; init; } = 2;
    public int MaxWorkingSetMb { get; init; } = SoakSession.DefaultMaxWorkingSetMb;
}

internal sealed class SoakReport
{
    public bool Ok { get; set; }
    public int Seconds { get; set; }
    public double ElapsedSec { get; set; }
    public double PeakWorkingSetMb { get; set; }
    public int MaxWorkingSetMb { get; set; }
    public bool Crashed { get; set; }
    public string? Error { get; set; }
    public List<string> Actions { get; set; } = new();
    public List<string> Screenshots { get; set; } = new();
    public string Pdf { get; set; } = string.Empty;
    public int Pages { get; set; }
    public bool SaveAsExists { get; set; }
}
