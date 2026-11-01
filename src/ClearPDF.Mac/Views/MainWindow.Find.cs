using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClearPDF.Helpers;

namespace ClearPDF.Mac.Views;

public partial class MainWindow
{
    private bool _suppressFindKeyUp;

    private FindSession? Find => _tabs.Current?.Find;

    private void FindToggle_Click(object? sender, RoutedEventArgs e) =>
        ApplyFindAction(FindAction.Toggle);

    private void FindNext_Click(object? sender, RoutedEventArgs e) =>
        ApplyFindAction(FindAction.Next);

    private void FindPrev_Click(object? sender, RoutedEventArgs e) =>
        ApplyFindAction(FindAction.Previous);

    private void FindBox_KeyDown(object? sender, KeyEventArgs e)
    {
        HandleFindBoxKey(e, fromKeyUp: false);
    }

    private void FindBox_KeyUp(object? sender, KeyEventArgs e)
    {
        HandleFindBoxKey(e, fromKeyUp: true);
    }

    private void HandleFindBoxKey(KeyEventArgs e, bool fromKeyUp)
    {
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var command = IsCommand(e);
        var action = FindKeys.Resolve(e.Key.ToString(), shift, command);
        if (action == FindAction.None)
            return;

        if (fromKeyUp)
        {
            if (_suppressFindKeyUp)
            {
                _suppressFindKeyUp = false;
                return;
            }

            if (action is not (FindAction.CommitNext or FindAction.Previous or FindAction.Close))
                return;
        }
        else if (FindKeys.IsEnter(e.Key.ToString()))
        {
            _suppressFindKeyUp = true;
        }

        ApplyFindAction(action);
        e.Handled = true;
    }

    private void ApplyFindAction(FindAction action)
    {
        switch (action)
        {
            case FindAction.None:
                return;
            case FindAction.Toggle:
                if (_tabs.Current == null)
                    return;
                Find!.Toggle();
                SyncFindUi();
                if (Find.IsOpen)
                    FindBox.Focus();
                else
                    ApplyFindHitChrome();
                return;
            case FindAction.CommitNext:
                if (Find is not { IsOpen: true })
                    return;
                Find.Commit(_pageTexts, FindBox.Text ?? string.Empty);
                AfterFindMove();
                return;
            case FindAction.Next:
                if (Find is not { IsOpen: true })
                    return;
                Find.Next(_pageTexts, FindBox.Text ?? string.Empty);
                AfterFindMove();
                return;
            case FindAction.Previous:
                if (Find is not { IsOpen: true })
                    return;
                Find.Previous(_pageTexts, FindBox.Text ?? string.Empty);
                AfterFindMove();
                return;
            case FindAction.Close:
                if (Find is not { IsOpen: true })
                    return;
                Find.Close();
                SyncFindUi();
                ApplyFindHitChrome();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private void AfterFindMove()
    {
        SyncFindUi();
        ApplyFindHitChrome();
        if (Find != null && Find.CurrentPage >= 0)
            GoToPage(Find.CurrentPage);
    }

    private void SyncFindUi()
    {
        var find = Find;
        FindPanel.IsVisible = find?.IsOpen == true;
        if (find == null)
        {
            FindBox.Text = string.Empty;
            FindStatus.Text = string.Empty;
            return;
        }

        if (FindBox.Text != find.Query)
            FindBox.Text = find.Query;
        FindStatus.Text = find.StatusText;
    }

    private void ApplyFindHitChrome()
    {
        var page = Find is { IsOpen: true } ? Find.CurrentPage : -1;
        if (_findHitPage == page)
            return;

        if (_findHitPage >= 0 &&
            _findHitPage < PageHost.Children.Count &&
            PageHost.Children[_findHitPage] is Border oldBorder)
        {
            oldBorder.BorderBrush = Brushes.Transparent;
        }

        _findHitPage = page;
        if (page >= 0 &&
            page < PageHost.Children.Count &&
            PageHost.Children[page] is Border border)
        {
            border.BorderBrush = AccentBrush;
        }
    }
}
