using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ClearPDF.Mac.Views;
using Xunit;

namespace ClearPDF.Mac.Tests;

public class MainWindowTabTests
{
    [AvaloniaFact]
    public void Opening_two_files_creates_one_tab_each()
    {
        WithReader(window =>
        {
            window.OpenDocumentForTests(
                new FakePdfDocument("/docs/two.pdf", "other"),
                "/docs/two.pdf");

            Assert.Equal(2, window.TabCount);
            Assert.Equal(new[] { "one.pdf", "two.pdf" }, window.TabTitles);
            Assert.Equal("two.pdf", window.CurrentTabTitle);
            Assert.True(window.ReaderViewIsVisible);
        });
    }

    [AvaloniaFact]
    public void Reopening_an_open_file_focuses_its_tab()
    {
        WithReader(window =>
        {
            window.OpenDocumentForTests(
                new FakePdfDocument("/docs/two.pdf", "other"),
                "/docs/two.pdf");
            Assert.Equal("two.pdf", window.CurrentTabTitle);

            var again = new FakePdfDocument("/docs/one.pdf", "should not attach");
            window.OpenDocumentForTests(again, again.FilePath);

            Assert.Equal(2, window.TabCount);
            Assert.Equal("one.pdf", window.CurrentTabTitle);
            Assert.Equal(1, again.DisposeCount);
            Assert.Equal(new[] { "one.pdf", "two.pdf" }, window.TabTitles);
        });
    }

    [AvaloniaFact]
    public void CmdW_closes_the_current_tab()
    {
        WithReader(window =>
        {
            var second = new FakePdfDocument("/docs/two.pdf", "other");
            window.OpenDocumentForTests(second, second.FilePath);
            Assert.Equal(2, window.TabCount);

            window.ExecuteShortcut(AppShortcut.CloseTab);
            window.DriveWindowKeyDown(MainWindow.NewKey(Key.W, KeyModifiers.Meta));

            Assert.Equal(1, window.TabCount);
            Assert.Equal("one.pdf", window.CurrentTabTitle);
            Assert.Equal(1, second.DisposeCount);
            Assert.True(window.IsVisible);

            window.ExecuteShortcut(AppShortcut.CloseTab);
            Assert.Equal(0, window.TabCount);
            Assert.True(window.EmptyViewIsVisible);
            Assert.True(window.IsVisible);
        });
    }

    [AvaloniaFact]
    public void Cmd_Shift_brackets_switch_tabs()
    {
        WithReader(window =>
        {
            window.OpenDocumentForTests(
                new FakePdfDocument("/docs/two.pdf", "other"),
                "/docs/two.pdf");
            Assert.Equal("two.pdf", window.CurrentTabTitle);

            window.ExecuteShortcut(AppShortcut.PreviousTab);
            window.DriveWindowKeyDown(
                MainWindow.NewKey(Key.OemOpenBrackets, KeyModifiers.Meta | KeyModifiers.Shift));
            Assert.Equal("one.pdf", window.CurrentTabTitle);

            window.ExecuteShortcut(AppShortcut.NextTab);
            window.DriveWindowKeyDown(
                MainWindow.NewKey(Key.OemCloseBrackets, KeyModifiers.Meta | KeyModifiers.Shift));
            Assert.Equal("two.pdf", window.CurrentTabTitle);
        });
    }

    [AvaloniaFact]
    public void Find_state_is_preserved_per_tab()
    {
        WithReader(window =>
        {
            window.ExecuteShortcut(AppShortcut.FindOpen);
            window.FindBox.Text = "hello";
            window.DriveFindBoxKey(MainWindow.NewKey(Key.Enter), fromKeyUp: false);
            Assert.Equal("hello", window.FindQuery);
            Assert.Equal("1 / 2", window.FindStatusText);

            window.OpenDocumentForTests(
                new FakePdfDocument("/docs/two.pdf", "unique term"),
                "/docs/two.pdf");
            Assert.False(window.FindIsOpen);

            window.ExecuteShortcut(AppShortcut.FindOpen);
            window.FindBox.Text = "unique";
            window.DriveFindBoxKey(MainWindow.NewKey(Key.Enter), fromKeyUp: false);
            Assert.Equal("unique", window.FindQuery);
            Assert.Equal("1 / 1", window.FindStatusText);

            window.ExecuteShortcut(AppShortcut.PreviousTab);
            Assert.True(window.FindIsOpen);
            Assert.Equal("hello", window.FindQuery);
            Assert.Equal("hello", window.FindBox.Text);
            Assert.Equal("1 / 2", window.FindStatusText);
            Assert.Equal("one.pdf", window.CurrentTabTitle);

            window.ExecuteShortcut(AppShortcut.NextTab);
            Assert.True(window.FindIsOpen);
            Assert.Equal("unique", window.FindQuery);
            Assert.Equal("unique", window.FindBox.Text);
            Assert.Equal("1 / 1", window.FindStatusText);
            Assert.Equal("two.pdf", window.CurrentTabTitle);
        });
    }

    private static void WithReader(Action<MainWindow> body)
    {
        var previous = MainWindow.RecentsOverride;
        MainWindow.RecentsOverride = new ClearPDF.Services.RecentFilesService(
            Path.Combine(Path.GetTempPath(), "clearpdf-tab-" + Guid.NewGuid().ToString("N") + ".json"));
        MainWindow? window = null;
        try
        {
            window = MacWindowHarness.OpenReader();
            body(window);
        }
        finally
        {
            window?.Close();
            MainWindow.RecentsOverride = previous;
        }
    }
}
