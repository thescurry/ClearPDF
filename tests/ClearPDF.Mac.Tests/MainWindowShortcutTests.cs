using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ClearPDF.Mac.Views;
using Xunit;

namespace ClearPDF.Mac.Tests;

public class MainWindowShortcutTests
{
    [AvaloniaFact]
    public void Avalonia_maps_Return_and_Enter_to_the_same_key()
    {
        Assert.Equal(Key.Enter, Key.Return);
        Assert.Equal(6, (int)Key.Enter);
    }

    [AvaloniaFact]
    public void CmdF_opens_once_window_and_findbox_do_not_toggle_it_closed()
    {
        WithReader(window =>
        {
            window.ExecuteShortcut(AppShortcut.FindOpen);
            Assert.True(window.FindIsOpen);

            window.DriveWindowKeyDown(MainWindow.NewKey(Key.F, KeyModifiers.Meta));
            window.DriveFindBoxKey(MainWindow.NewKey(Key.F, KeyModifiers.Meta), fromKeyUp: false);
            Assert.True(window.FindIsOpen);
        });
    }

    [AvaloniaFact]
    public void CmdG_advances_one_hit_window_and_findbox_do_not_skip()
    {
        WithReader(window =>
        {
            window.ExecuteShortcut(AppShortcut.FindOpen);
            SetFindText(window, "hello");
            window.DriveFindBoxKey(MainWindow.NewKey(Key.Enter), fromKeyUp: false);
            Assert.Equal(0, window.FindIndex);

            window.ExecuteShortcut(AppShortcut.FindNext);
            Assert.Equal(1, window.FindIndex);

            window.DriveWindowKeyDown(MainWindow.NewKey(Key.G, KeyModifiers.Meta));
            window.DriveFindBoxKey(MainWindow.NewKey(Key.G, KeyModifiers.Meta), fromKeyUp: false);
            Assert.Equal(1, window.FindIndex);
            Assert.Equal("2 / 2", window.FindStatusText);
        });
    }

    [AvaloniaFact]
    public void CmdW_closes_one_tab_and_does_not_close_the_window()
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
            window.ExecuteShortcut(AppShortcut.CloseTab);
            window.DriveWindowKeyDown(MainWindow.NewKey(Key.W, KeyModifiers.Meta));
            Assert.Equal(0, window.TabCount);
            Assert.True(window.IsVisible);
        });
    }

    [AvaloniaFact]
    public void Ime_candidate_confirm_Enter_on_KeyUp_does_not_search()
    {
        WithReader(window =>
        {
            window.ExecuteShortcut(AppShortcut.FindOpen);
            SetFindText(window, "hello");

            window.DriveFindBoxKey(MainWindow.NewKey(Key.Enter, keyUp: true), fromKeyUp: true);

            Assert.True(window.FindIsOpen);
            Assert.Equal(-1, window.FindIndex);
            Assert.Equal(string.Empty, window.FindStatusText);
        });
    }

    [AvaloniaFact]
    public void Suppress_flag_resets_on_focus_change_and_later_Enter_still_searches()
    {
        WithReader(window =>
        {
            window.ExecuteShortcut(AppShortcut.FindOpen);
            SetFindText(window, "hello");
            window.DriveFindBoxKey(MainWindow.NewKey(Key.Enter), fromKeyUp: false);
            Assert.True(window.SuppressFindKeyUp);
            window.ExecuteShortcut(AppShortcut.FindNext);
            Assert.Equal(1, window.FindIndex);

            window.BlurFindBoxForTests();
            Assert.False(window.SuppressFindKeyUp);

            window.DriveFindBoxKey(MainWindow.NewKey(Key.Enter), fromKeyUp: false);
            Assert.Equal(0, window.FindIndex);
            Assert.Equal("1 / 2", window.FindStatusText);
        });
    }

    [AvaloniaFact]
    public void Keypad_Enter_commits_find()
    {
        WithReader(window =>
        {
            window.ExecuteShortcut(AppShortcut.FindOpen);
            SetFindText(window, "hello");

            window.DriveFindBoxKey(
                MainWindow.NewKey(Key.Enter, physical: PhysicalKey.NumPadEnter),
                fromKeyUp: false);

            Assert.Equal(0, window.FindIndex);
            Assert.Equal("1 / 2", window.FindStatusText);
        });
    }

    [AvaloniaFact]
    public void Shift_Cmd_brackets_switch_one_tab()
    {
        WithReader(window =>
        {
            window.OpenDocumentForTests(new FakePdfDocument("/docs/two.pdf", "other"), "/docs/two.pdf");
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

    private static void WithReader(Action<MainWindow> body)
    {
        var window = OpenReader();
        try
        {
            body(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static MainWindow OpenReader()
    {
        var window = new MainWindow();
        window.Show();
        var doc = new FakePdfDocument("/docs/one.pdf");
        window.OpenDocumentForTests(doc, doc.FilePath);
        return window;
    }

    private static void SetFindText(MainWindow window, string text)
    {
        window.FindBox.Text = text;
    }
}
