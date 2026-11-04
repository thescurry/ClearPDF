using ClearPDF.Mac.Views;
using ClearPDF.Services;

namespace ClearPDF.Mac.Tests;

internal static class MacWindowHarness
{
    public static void WithWindow(Action<MainWindow> body, RecentFilesService? recents = null)
    {
        var previous = MainWindow.RecentsOverride;
        var store = recents
            ?? new RecentFilesService(Path.Combine(
                Path.GetTempPath(),
                "clearpdf-recent-" + Guid.NewGuid().ToString("N") + ".json"));
        MainWindow.RecentsOverride = store;
        MainWindow? window = null;
        try
        {
            window = new MainWindow
            {
                Width = 1100,
                Height = 720
            };
            window.Show();
            body(window);
        }
        finally
        {
            window?.Close();
            MainWindow.RecentsOverride = previous;
        }
    }

    public static MainWindow OpenReader(params string[] pageTexts)
    {
        var window = new MainWindow
        {
            Width = 1100,
            Height = 720
        };
        window.Show();
        var doc = new FakePdfDocument("/docs/one.pdf", pageTexts);
        window.OpenDocumentForTests(doc, doc.FilePath);
        return window;
    }
}
