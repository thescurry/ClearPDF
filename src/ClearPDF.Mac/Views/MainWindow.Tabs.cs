using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ClearPDF.Helpers;
using ClearPDF.Services;

namespace ClearPDF.Mac.Views;

public partial class MainWindow
{
    private readonly DocumentTabSet _tabs = new();
    private readonly Dictionary<string, LiveDoc> _live = new();

    private sealed class LiveDoc
    {
        public required IPdfDocument Doc { get; init; }
        public required string Path { get; init; }
        public double Zoom { get; set; } = ZoomHelper.DefaultZoom;
        public double RenderedZoom { get; set; } = ZoomHelper.DefaultZoom;
        public int CurrentPage { get; set; }
        public string[] PageTexts { get; set; } = Array.Empty<string>();
        public PageSet Selection { get; set; } = PageSet.Empty;
        public PageBitmapCache Cache { get; } = new();
        public int FindHitPage { get; set; } = -1;
    }

    private void InstallNativeMenu()
    {
        NativeMenuItem Item(string header, KeyGesture gesture, EventHandler handler)
        {
            var item = new NativeMenuItem(header) { Gesture = gesture };
            item.Click += handler;
            return item;
        }

        var file = new NativeMenuItem("File")
        {
            Menu = new NativeMenu
            {
                Item("Open…", new KeyGesture(Key.O, KeyModifiers.Meta), (_, _) => _ = OpenWithDialog()),
                Item("Close", new KeyGesture(Key.W, KeyModifiers.Meta), (_, _) => ExecuteShortcut(AppShortcut.CloseTab))
            }
        };
        var edit = new NativeMenuItem("Edit")
        {
            Menu = new NativeMenu
            {
                Item("Find…", new KeyGesture(Key.F, KeyModifiers.Meta), (_, _) => ExecuteShortcut(AppShortcut.FindOpen)),
                Item("Find Next", new KeyGesture(Key.G, KeyModifiers.Meta), (_, _) => ExecuteShortcut(AppShortcut.FindNext)),
                Item("Find Previous", new KeyGesture(Key.G, KeyModifiers.Meta | KeyModifiers.Shift),
                    (_, _) => ExecuteShortcut(AppShortcut.FindPrevious))
            }
        };
        var window = new NativeMenuItem("Window")
        {
            Menu = new NativeMenu
            {
                Item("Show Previous Tab", new KeyGesture(Key.OemOpenBrackets, KeyModifiers.Meta | KeyModifiers.Shift),
                    (_, _) => ExecuteShortcut(AppShortcut.PreviousTab)),
                Item("Show Next Tab", new KeyGesture(Key.OemCloseBrackets, KeyModifiers.Meta | KeyModifiers.Shift),
                    (_, _) => ExecuteShortcut(AppShortcut.NextTab))
            }
        };

        var menu = new NativeMenu();
        menu.Items.Add(file);
        menu.Items.Add(edit);
        menu.Items.Add(window);
        NativeMenu.SetMenu(this, menu);
    }

    /// <summary>
    /// Single owner for menu-bar shortcuts. Window_KeyDown and the Find box
    /// must not apply these again.
    /// </summary>
    internal void ExecuteShortcut(AppShortcut shortcut)
    {
        switch (shortcut)
        {
            case AppShortcut.FindOpen:
                ApplyFindAction(FindAction.Open);
                return;
            case AppShortcut.FindNext:
                ApplyFindAction(FindAction.Next);
                return;
            case AppShortcut.FindPrevious:
                ApplyFindAction(FindAction.Previous);
                return;
            case AppShortcut.CloseTab:
                if (_tabs.Count > 0)
                    CloseCurrentTab();
                return;
            case AppShortcut.PreviousTab:
                SwitchTab(() => _tabs.SelectPrevious());
                return;
            case AppShortcut.NextTab:
                SwitchTab(() => _tabs.SelectNext());
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(shortcut), shortcut, null);
        }
    }

    private bool TryActivateExistingTab(string path)
    {
        if (!_tabs.Tabs.Any(t => DocumentPath.Same(t.Path, path)))
            return false;

        CaptureCurrentTabState();
        _tabs.Open(path);
        ActivateCurrentTab();
        _recents.Add(path);
        RefreshRecentList();
        return true;
    }

    private void AttachNewDocument(IPdfDocument doc, string path)
    {
        CaptureCurrentTabState();
        var opened = _tabs.Open(path);
        if (opened.AlreadyOpen)
        {
            doc.Dispose();
            ActivateCurrentTab();
            _recents.Add(path);
            RefreshRecentList();
            return;
        }

        var texts = new string[doc.PageCount];
        for (var i = 0; i < doc.PageCount; i++)
            texts[i] = doc.GetPageText(i);

        _live[opened.Tab.Id] = new LiveDoc
        {
            Doc = doc,
            Path = path,
            PageTexts = texts,
            Selection = PageSelection.Click(0, doc.PageCount)
        };

        _recents.Add(path);
        RefreshRecentList();
        ActivateCurrentTab();
    }

    private void CaptureCurrentTabState()
    {
        var tab = _tabs.Current;
        if (tab == null || !_live.TryGetValue(tab.Id, out var live))
            return;

        live.Zoom = _zoom;
        live.RenderedZoom = _renderedZoom;
        live.CurrentPage = _currentPage;
        live.Selection = _selection;
        live.FindHitPage = _findHitPage;
        live.PageTexts = _pageTexts;
    }

    private void ActivateCurrentTab()
    {
        var tab = _tabs.Current;
        if (tab == null || !_live.TryGetValue(tab.Id, out var live))
        {
            ShowEmptyReader();
            return;
        }

        _zoomRenderTimer?.Stop();
        _scrollRenderTimer?.Stop();
        CancelRenders();
        CancelThumbRenders();

        _doc = live.Doc;
        _bitmapCache = live.Cache;
        _zoom = live.Zoom;
        _renderedZoom = live.RenderedZoom;
        _currentPage = live.CurrentPage;
        _selection = live.Selection;
        _pageTexts = live.PageTexts;
        _findHitPage = -1;
        _thumbsBuilt = false;
        _highlightedThumb = -1;
        _selectedThumbs.Clear();
        HideSaveAsChoice();
        InvalidatePageTops();

        EmptyView.IsVisible = false;
        ReaderView.IsVisible = true;
        AppMenu.IsVisible = false;
        ToolbarBorder.IsVisible = true;
        StatusBar.IsVisible = true;
        Title = $"ClearPDF — {tab.Title}";

        if (!DocnetPdfDocumentFactory.AreNativesAvailable)
        {
            ShowError("PDFium natives not loaded — showing stub pages. On macOS, publish with -r osx-x64 so Docnet.Core can ship pdfium.dylib.");
        }

        PageHost.RenderTransform = null;
        BuildPageShells();
        BuildThumbnailShells();
        SyncFindUi();
        ApplyFindHitChrome();
        UpdateStatus();
        RebuildTabStrip();
        SchedulePageRenders(includePreviews: true);
        ScheduleThumbFills();

        if (tab.Find.IsOpen)
            FindBox.Focus();
    }

    private void ShowEmptyReader()
    {
        _zoomRenderTimer?.Stop();
        _scrollRenderTimer?.Stop();
        CancelRenders();
        CancelThumbRenders();
        _doc = null;
        _bitmapCache = new PageBitmapCache();
        _pageTexts = Array.Empty<string>();
        _pageImages = Array.Empty<Image>();
        _thumbFrames = Array.Empty<Border>();
        _thumbImages = Array.Empty<Image>();
        _thumbChecks = Array.Empty<TextBlock>();
        _findHitPage = -1;
        _highlightedThumb = -1;
        _thumbsBuilt = false;
        PageHost.Children.Clear();
        ThumbnailList.Children.Clear();
        HideSaveAsChoice();
        SyncFindUi();
        RebuildTabStrip();

        EmptyView.IsVisible = true;
        ReaderView.IsVisible = false;
        AppMenu.IsVisible = true;
        ToolbarBorder.IsVisible = false;
        StatusBar.IsVisible = false;
        Title = "ClearPDF";
    }

    private void CloseCurrentTab()
    {
        var tab = _tabs.Current;
        if (tab != null)
            CloseTab(tab.Id);
    }

    private void CloseTab(string id)
    {
        if (_live.Remove(id, out var live))
        {
            if (ReferenceEquals(_doc, live.Doc))
                _doc = null;
            live.Cache.Clear();
            live.Doc.Dispose();
        }

        _tabs.Close(id);
        if (_tabs.Current == null)
            ShowEmptyReader();
        else
            ActivateCurrentTab();
    }

    private void SwitchTab(Func<bool> move)
    {
        if (_tabs.Count <= 1)
            return;
        CaptureCurrentTabState();
        if (move())
            ActivateCurrentTab();
    }

    private void DisposeAllTabs()
    {
        foreach (var live in _live.Values)
        {
            live.Cache.Clear();
            live.Doc.Dispose();
        }

        _live.Clear();
        _doc = null;
    }

    private void RebuildTabStrip()
    {
        TabStrip.Children.Clear();
        TabStripBorder.IsVisible = _tabs.Count > 0;
        foreach (var tab in _tabs.Tabs)
        {
            var selected = tab.Id == _tabs.Current?.Id;
            var title = new TextBlock
            {
                Text = tab.Title,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse(selected ? "#222222" : "#6A6A72")),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 180,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var close = new Button
            {
                Content = "✕",
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                Tag = tab.Id,
                VerticalAlignment = VerticalAlignment.Center
            };
            close.Classes.Add("tabclose");
            close.Click += TabClose_Click;

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(title);
            row.Children.Add(close);

            var chrome = new Border
            {
                Background = selected ? Brushes.White : new SolidColorBrush(Color.Parse("#ECECF0")),
                BorderBrush = selected ? AccentBrush : new SolidColorBrush(Color.Parse("#D8D9DE")),
                BorderThickness = selected ? new Thickness(1, 1, 1, 0) : new Thickness(1),
                Padding = new Thickness(10, 4, 6, 4),
                Margin = new Thickness(0, 4, 4, 0),
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Child = row,
                Tag = tab.Id,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            chrome.PointerPressed += Tab_PointerPressed;
            TabStrip.Children.Add(chrome);
        }
    }

    private void Tab_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: string id })
            return;
        if (e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed)
        {
            if (_tabs.Current?.Id == id)
                return;
            CaptureCurrentTabState();
            if (_tabs.Select(id))
                ActivateCurrentTab();
            e.Handled = true;
        }
    }

    private void TabClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            CaptureCurrentTabState();
            CloseTab(id);
            e.Handled = true;
        }
    }
}
