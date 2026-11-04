using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClearPDF.Helpers;
using ClearPDF.Services;

namespace ClearPDF.Mac.Views;

public partial class MainWindow
{
    internal static RecentFilesService? RecentsOverride { get; set; }

    internal int OpenDialogCount { get; private set; }
    internal Func<Task>? OpenDialogOverride { get; set; }
    internal string? SaveAsPathOverride { get; set; }

    internal void OpenDocumentForTests(IPdfDocument doc, string path) =>
        AttachNewDocument(doc, path);

    internal void DriveWindowKeyDown(KeyEventArgs e) =>
        Window_KeyDown(this, e);

    internal void DriveFindBoxKey(KeyEventArgs e, bool fromKeyUp) =>
        HandleFindBoxKey(e, fromKeyUp);

    internal bool FindIsOpen => Find?.IsOpen == true;
    internal int FindIndex => Find?.CurrentIndex ?? -1;
    internal string FindStatusText => Find?.StatusText ?? string.Empty;
    internal string FindQuery => Find?.Query ?? string.Empty;
    internal int TabCount => _tabs.Count;
    internal string? CurrentTabTitle => _tabs.Current?.Title;
    internal IReadOnlyList<string> TabTitles => _tabs.Tabs.Select(t => t.Title).ToArray();
    internal bool EmptyViewIsVisible => EmptyView.IsVisible;
    internal bool ReaderViewIsVisible => ReaderView.IsVisible;
    internal bool SaveAsChoiceIsOpen => SaveAsChoiceOverlay.IsVisible;
    internal int SelectionCount => _selection.Count;
    internal double CurrentZoom => _zoom;
    internal int CurrentPageIndex => _currentPage;
    internal int RecentRowCount =>
        (RecentList.ItemsSource as System.Collections.ICollection)?.Count ?? 0;
    internal ItemsControl RecentListControl => RecentList;
    internal ScrollViewer PageScrollerForTests => PageScroller;

    internal void RefreshRecentListForTests() => RefreshRecentList();

    internal void ApplyZoomForTests(double zoom) => ApplyZoom(zoom);

    internal void GoToPageForTests(int page) => GoToPage(page);

    internal void ScrollToOffsetForTests(double y) =>
        PageScroller.Offset = new Vector(PageScroller.Offset.X, Math.Max(0, y));

    internal void SelectThumbForTests(int pageIndex, KeyModifiers mods)
    {
        if (_doc == null)
            return;

        if ((mods & KeyModifiers.Shift) == KeyModifiers.Shift)
            _selection = PageSelection.Range(_selection.Anchor, pageIndex, _doc.PageCount);
        else if ((mods & KeyModifiers.Control) == KeyModifiers.Control
                 || (mods & KeyModifiers.Meta) == KeyModifiers.Meta)
            _selection = PageSelection.Toggle(_selection, pageIndex, _doc.PageCount);
        else
            _selection = PageSelection.Click(pageIndex, _doc.PageCount);

        RefreshThumbSelection();
        HideSaveAsChoice();
        GoToPage(pageIndex, syncSingleSelection: false);
    }

    internal void ClickSaveAsForTests() =>
        SaveAs_Click(this, new RoutedEventArgs());

    internal void ConfirmSaveAsChoiceForTests() =>
        SaveAsChoiceSave_Click(this, new RoutedEventArgs());

    internal static KeyEventArgs NewKey(
        Key key,
        KeyModifiers modifiers = KeyModifiers.None,
        PhysicalKey? physical = null,
        bool keyUp = false)
    {
        return new KeyEventArgs
        {
            RoutedEvent = keyUp ? InputElement.KeyUpEvent : InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            PhysicalKey = physical ?? PhysicalKey.None
        };
    }
}
