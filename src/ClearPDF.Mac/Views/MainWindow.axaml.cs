using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ClearPDF.Helpers;
using ClearPDF.Imaging;
using ClearPDF.Mac.Services;
using ClearPDF.Services;

namespace ClearPDF.Mac.Views;

public partial class MainWindow : Window
{
    private readonly IPdfDocumentFactory _pdfFactory = new DocnetPdfDocumentFactory();
    private readonly RecentFilesService _recents = new();
    private readonly PageBitmapCache _bitmapCache = new();

    private IPdfDocument? _doc;
    private double _zoom = ZoomHelper.DefaultZoom;
    private int _currentPage;
    private IReadOnlyList<FindHelper.Hit> _findHits = Array.Empty<FindHelper.Hit>();
    private int _findIndex = -1;
    private string[] _pageTexts = Array.Empty<string>();
    private double _renderedZoom = ZoomHelper.DefaultZoom;
    private DispatcherTimer? _zoomRenderTimer;
    private DispatcherTimer? _scrollRenderTimer;

    private Image[] _pageImages = Array.Empty<Image>();
    private Border[] _thumbFrames = Array.Empty<Border>();
    private Image[] _thumbImages = Array.Empty<Image>();
    private TextBlock[] _thumbChecks = Array.Empty<TextBlock>();

    private CancellationTokenSource? _renderCts;
    private int _renderGeneration;
    private bool _thumbsBuilt;
    private readonly HashSet<(int Page, int ZoomKey, int Gen)> _inFlight = new();
    private readonly object _inFlightGate = new();
    private bool[] _pageShowingSharp = Array.Empty<bool>();
    private double[] _pageTops = Array.Empty<double>();
    private bool _pageTopsDirty = true;
    private int _highlightedThumb = -1;
    private int _findHitPage = -1;
    private PageSet _selection = PageSet.Empty;
    private readonly HashSet<int> _selectedThumbs = new();

    private CancellationTokenSource? _thumbCts;
    private int _thumbGeneration;

    private const double ScrollerPadding = 28;
    private const double ThumbWidthPx = 120;
    private const double ThumbRowGapPx = 4;

    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.Parse("#5A7FA6"));
    private static readonly IBrush ThumbIdleBrush = new SolidColorBrush(Color.Parse("#CCCCCC"));
    private static readonly FilePickerFileType PdfFileType = new("PDF files")
    {
        Patterns = new[] { "*.pdf" }
    };

    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, Window_DragOver);
        AddHandler(DragDrop.DropEvent, Window_Drop);
        Opened += OnOpened;
        Closed += (_, _) =>
        {
            _zoomRenderTimer?.Stop();
            _scrollRenderTimer?.Stop();
            CancelRenders();
            CancelThumbRenders();
            _doc?.Dispose();
            _bitmapCache.Clear();
        };
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        RefreshRecentList();
        var cliPdf = CommandLinePdfArgs.TryGetPdfFromProcessCommandLine();
        if (cliPdf != null)
            _ = OpenPathAsync(cliPdf);
    }

    private void Window_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetDroppedPdf(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object? sender, DragEventArgs e)
    {
        var path = TryGetDroppedPdf(e);
        e.Handled = true;
        if (path != null)
            _ = OpenPathAsync(path);
    }

    private static string? TryGetDroppedPdf(DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files))
            return null;
        var files = e.Data.GetFiles();
        if (files == null)
            return null;
        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Cast<string>();
        return CommandLinePdfArgs.TryGetFirstExistingPdf(paths);
    }

    private void RefreshRecentList()
    {
        var items = _recents.Load()
            .Select(e => new RecentRow(e.DisplayName, e.Path, RecentFilesService.FormatRelativeTime(e.OpenedUtc)))
            .ToList();
        RecentList.ItemsSource = items;
        NoRecentText.IsVisible = items.Count == 0;
    }

    private async void Open_Click(object? sender, RoutedEventArgs e) => await OpenWithDialog();

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new AboutWindow();
        await dlg.ShowDialog(this);
    }

    private async void DuckWaffles_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new DuckWafflesWindow();
        await dlg.ShowDialog(this);
    }

    private void RecentItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            _ = OpenPathAsync(path);
    }

    private async Task OpenWithDialog()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PDF",
            AllowMultiple = false,
            FileTypeFilter = new[] { PdfFileType }
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            await OpenPathAsync(path);
    }

    private async Task OpenPathAsync(string path, string? password = null)
    {
        HideError();
        string? pwd = password;
        while (true)
        {
            try
            {
                var doc = _pdfFactory.Open(path, pwd);
                SetDocument(doc, path);
                return;
            }
            catch (PdfPasswordException ex)
            {
                AppLog.Warn($"Password error opening '{path}': {ex.Message}");
                if (!PdfUnlockPolicy.ShouldPromptAgainAfterFailure)
                    return;
                pwd = await PromptPasswordAsync();
                if (pwd == null)
                    return;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Open failed: {path}", ex);
                ShowError(ex.Message);
                return;
            }
        }
    }

    private async Task<string?> PromptPasswordAsync()
    {
        var dlg = new PasswordDialog();
        var ok = await dlg.ShowDialog<bool>(this);
        return ok ? dlg.Password : null;
    }

    private void SetDocument(IPdfDocument doc, string path)
    {
        _zoomRenderTimer?.Stop();
        _scrollRenderTimer?.Stop();
        CancelRenders();
        CancelThumbRenders();
        _bitmapCache.Clear();
        _findHitPage = -1;
        _highlightedThumb = -1;
        _selectedThumbs.Clear();
        HideSaveAsChoice();
        InvalidatePageTops();
        _doc?.Dispose();
        _doc = doc;
        _zoom = ZoomHelper.DefaultZoom;
        _currentPage = 0;
        _findHits = Array.Empty<FindHelper.Hit>();
        _findIndex = -1;
        _thumbsBuilt = false;
        _selection = PageSelection.Click(0, doc.PageCount);
        FindPanel.IsVisible = false;

        _pageTexts = new string[doc.PageCount];
        for (var i = 0; i < doc.PageCount; i++)
            _pageTexts[i] = doc.GetPageText(i);

        _recents.Add(path);
        RefreshRecentList();

        EmptyView.IsVisible = false;
        ReaderView.IsVisible = true;
        AppMenu.IsVisible = false;
        ToolbarBorder.IsVisible = true;
        StatusBar.IsVisible = true;
        Title = $"ClearPDF — {Path.GetFileName(path)}";

        if (!DocnetPdfDocumentFactory.AreNativesAvailable)
        {
            ShowError("PDFium natives not loaded — showing stub pages. On macOS, publish with -r osx-x64 so Docnet.Core can ship pdfium.dylib.");
        }

        PageHost.RenderTransform = null;
        _renderedZoom = _zoom;
        BuildPageShells();
        BuildThumbnailShells();
        UpdateStatus();
        SchedulePageRenders(includePreviews: true);
        ScheduleThumbFills();
    }

    private void BuildPageShells()
    {
        if (_doc == null)
            return;

        PageHost.Children.Clear();
        _pageImages = new Image[_doc.PageCount];

        for (var i = 0; i < _doc.PageCount; i++)
        {
            var (pw, ph) = _doc.GetPageSize(i);
            var displayW = Math.Max(1, pw * _renderedZoom);
            var displayH = Math.Max(1, ph * _renderedZoom);

            var image = new Image
            {
                Width = displayW,
                Height = displayH,
                Stretch = Stretch.Fill
            };
            _pageImages[i] = image;

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 20),
                Width = displayW,
                Height = displayH,
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 8,
                    OffsetY = 1,
                    Color = Color.FromArgb(26, 0, 0, 0)
                }),
                Child = image,
                Tag = i
            };
            PageHost.Children.Add(border);
        }

        _pageShowingSharp = new bool[_doc.PageCount];
        _findHitPage = -1;
        InvalidatePageTops();
    }

    private void ResizePageShellsToRenderedZoom()
    {
        if (_doc == null)
            return;

        for (var i = 0; i < _pageImages.Length; i++)
        {
            var (pw, ph) = _doc.GetPageSize(i);
            var displayW = Math.Max(1, pw * _renderedZoom);
            var displayH = Math.Max(1, ph * _renderedZoom);
            _pageImages[i].Width = displayW;
            _pageImages[i].Height = displayH;
            if (PageHost.Children[i] is Border border)
            {
                border.Width = displayW;
                border.Height = displayH;
            }
        }

        InvalidatePageTops();
    }

    private void BuildThumbnailShells()
    {
        if (_doc == null)
            return;

        ThumbnailList.Children.Clear();
        _selectedThumbs.Clear();
        _thumbFrames = new Border[_doc.PageCount];
        _thumbImages = new Image[_doc.PageCount];
        _thumbChecks = new TextBlock[_doc.PageCount];

        for (var i = 0; i < _doc.PageCount; i++)
        {
            var pageIndex = i;
            var (pw, ph) = _doc.GetPageSize(i);
            if (pw <= 0)
                pw = PdfPageSize.LetterWidthPoints;
            if (ph <= 0)
                ph = PdfPageSize.LetterHeightPoints;
            var thumbHeight = ThumbWidthPx * (ph / pw);

            var image = new Image
            {
                Stretch = Stretch.Fill,
                Width = ThumbWidthPx,
                Height = thumbHeight
            };
            _thumbImages[i] = image;

            var selected = _selection.Contains(i);
            var frame = new Border
            {
                BorderThickness = new Thickness(2),
                BorderBrush = ThumbBorderBrush(selected),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.White,
                Child = image,
                Tag = pageIndex
            };

            var check = CreateThumbCheck(selected);
            _thumbChecks[i] = check;

            var caption = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 4, 0, 0)
            };
            caption.Children.Add(CreateThumbBadge(i));
            caption.Children.Add(check);

            var row = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, ThumbRowGapPx),
                Tag = pageIndex,
                ContextMenu = CreateThumbContextMenu()
            };
            row.Children.Add(frame);
            row.Children.Add(caption);
            row.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left)
                    OnThumbLeftUp(pageIndex, e);
            };
            row.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(row).Properties.IsRightButtonPressed)
                    OnThumbRightDown(pageIndex);
            };

            _thumbFrames[i] = frame;
            ThumbnailList.Children.Add(row);
            if (selected)
                _selectedThumbs.Add(i);
        }

        _thumbsBuilt = true;
        _highlightedThumb = _currentPage;
    }

    private void ScheduleThumbFills()
    {
        if (_doc == null || _thumbImages.Length == 0)
            return;

        CancelThumbRenders();
        var token = _thumbCts!.Token;
        var gen = _thumbGeneration;
        var doc = _doc;
        var count = doc.PageCount;

        _ = Task.Run(() =>
        {
            for (var pageIndex = 0; pageIndex < count; pageIndex++)
            {
                if (token.IsCancellationRequested || gen != _thumbGeneration)
                    return;

                if (_bitmapCache.TryGetThumb(pageIndex, out var cached) && cached != null)
                {
                    var hit = cached;
                    var idx = pageIndex;
                    Dispatcher.UIThread.Post(() => ApplyThumbBitmap(idx, hit, gen, doc));
                    continue;
                }

                try
                {
                    var bmp = doc.RenderPage(pageIndex, PdfRenderScale.ThumbZoom);
                    if (bmp == null || token.IsCancellationRequested || gen != _thumbGeneration)
                        return;
                    _bitmapCache.SetThumb(pageIndex, bmp);
                    Dispatcher.UIThread.Post(() => ApplyThumbBitmap(pageIndex, bmp, gen, doc));
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"Thumb render failed page {pageIndex}: {ex.Message}");
                }
            }
        }, token);
    }

    private void ApplyThumbBitmap(int pageIndex, PdfPixelBuffer bmp, int gen, IPdfDocument doc)
    {
        if (gen != _thumbGeneration || _doc != doc)
            return;
        if (pageIndex < 0 || pageIndex >= _thumbImages.Length)
            return;
        _thumbImages[pageIndex].Source = PdfPixelBufferAvalonia.ToBitmap(bmp);
    }

    private void CancelThumbRenders()
    {
        _thumbGeneration++;
        try
        {
            _thumbCts?.Cancel();
            _thumbCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _thumbCts = new CancellationTokenSource();
    }

    private static Border CreateThumbBadge(int pageIndex)
    {
        return new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            Child = new TextBlock
            {
                Text = (pageIndex + 1).ToString(),
                Foreground = Brushes.White,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static TextBlock CreateThumbCheck(bool selected)
    {
        return new TextBlock
        {
            Text = "✓",
            FontSize = 11,
            Foreground = AccentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            IsVisible = selected
        };
    }

    private ContextMenu CreateThumbContextMenu()
    {
        var save = new MenuItem { Header = "Save pages as…" };
        save.Click += SavePagesAs_Click;
        var menu = new ContextMenu();
        menu.Items.Add(save);
        return menu;
    }

    private void OnThumbLeftUp(int pageIndex, PointerReleasedEventArgs e)
    {
        if (_doc == null)
            return;

        var mods = e.KeyModifiers;
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
        e.Handled = true;
    }

    private void OnThumbRightDown(int pageIndex)
    {
        if (_doc == null)
            return;
        if (!_selection.Contains(pageIndex))
        {
            _selection = PageSelection.Click(pageIndex, _doc.PageCount);
            RefreshThumbSelection();
            UpdateStatus();
        }
    }

    private static IBrush ThumbBorderBrush(bool selected) =>
        selected ? AccentBrush : ThumbIdleBrush;

    private void RefreshThumbSelection()
    {
        if (!_thumbsBuilt || _doc == null)
            return;

        for (var i = 0; i < _thumbFrames.Length; i++)
        {
            var selected = _selection.Contains(i);
            if (selected)
                _selectedThumbs.Add(i);
            else
                _selectedThumbs.Remove(i);
            ApplyThumbChrome(i, selected);
        }

        _highlightedThumb = _currentPage;
        UpdateStatus();
    }

    private void ApplyThumbChrome(int index, bool selected)
    {
        if (index < 0 || index >= _thumbFrames.Length)
            return;
        _thumbFrames[index].BorderBrush = ThumbBorderBrush(selected);
        if (index < _thumbChecks.Length)
            _thumbChecks[index].IsVisible = selected;
    }

    private void GoToPage(int pageIndex, bool syncSingleSelection = true)
    {
        if (_doc == null || pageIndex < 0 || pageIndex >= _doc.PageCount)
            return;

        _currentPage = pageIndex;
        if (syncSingleSelection && !PageSelection.IsMulti(_selection))
            _selection = PageSelection.FollowCurrent(pageIndex, _doc.PageCount);

        RefreshThumbSelection();
        if (pageIndex < PageHost.Children.Count && PageHost.Children[pageIndex] is Control el)
            el.BringIntoView();

        UpdateStatus();
        SchedulePageRenders(includePreviews: false);
    }

    private void UpdateStatus()
    {
        if (_doc == null)
            return;
        var sel = PageSelection.IsMulti(_selection)
            ? $"  ·  {_selection.Count} selected"
            : string.Empty;
        PageStatusText.Text = $"{_currentPage + 1} / {_doc.PageCount}{sel}";
        ZoomStatusText.Text = $"{Math.Round(_zoom * 100)}%";
        ZoomLabel.Content = $"{Math.Round(_zoom * 100)}%";
    }

    private void ApplyZoom(double zoom)
    {
        _zoom = ZoomHelper.Clamp(zoom);
        ApplyInstantZoomTransform();
        UpdateStatus();
        ScheduleDebouncedPageRender();
    }

    private void ApplyInstantZoomTransform()
    {
        if (_renderedZoom <= 0 || PageHost.Children.Count == 0)
        {
            PageHost.RenderTransform = null;
            return;
        }

        var factor = _zoom / _renderedZoom;
        PageHost.RenderTransform = Math.Abs(factor - 1.0) < 0.001
            ? null
            : new ScaleTransform(factor, factor);
    }

    private void ScheduleDebouncedPageRender()
    {
        if (_zoomRenderTimer == null)
        {
            _zoomRenderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PageRenderPlanner.ZoomDebounceMs)
            };
            _zoomRenderTimer.Tick += (_, _) =>
            {
                _zoomRenderTimer.Stop();
                CommitZoomRender();
            };
        }

        _zoomRenderTimer.Stop();
        _zoomRenderTimer.Start();
    }

    private void CommitZoomRender()
    {
        if (_doc == null)
            return;

        if (Math.Abs(_renderedZoom - _zoom) < 0.001)
        {
            PageHost.RenderTransform = null;
            return;
        }

        _renderedZoom = _zoom;
        ResizePageShellsToRenderedZoom();
        PageHost.RenderTransform = null;

        if (_currentPage < PageHost.Children.Count && PageHost.Children[_currentPage] is Control el)
            el.BringIntoView();

        Array.Clear(_pageShowingSharp);
        _bitmapCache.FocusPage = _currentPage;
        _bitmapCache.Trim();
        SchedulePageRenders(includePreviews: true);
    }

    private void CancelRenders()
    {
        _renderGeneration++;
        try
        {
            _renderCts?.Cancel();
            _renderCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _renderCts = new CancellationTokenSource();
        lock (_inFlightGate)
            _inFlight.Clear();
    }

    private void SchedulePageRenders(bool includePreviews)
    {
        if (_doc == null || _pageImages.Length == 0)
            return;

        if (includePreviews)
            CancelRenders();
        else if (_renderCts == null)
            _renderCts = new CancellationTokenSource();

        var token = _renderCts!.Token;
        var gen = _renderGeneration;
        var doc = _doc;
        var sharpZoom = PdfRenderScale.QuantizeZoom(_renderedZoom);
        var current = _currentPage;
        var count = doc.PageCount;
        _bitmapCache.FocusPage = current;

        foreach (var job in PageRenderPlanner.BuildJobs(current, count, includePreviews))
        {
            var pageIndex = job.Page;
            var zoom = job.Sharp ? sharpZoom : PdfRenderScale.PreviewZoom;
            var zoomKey = PdfRenderScale.ZoomCacheKey(zoom);

            if (_bitmapCache.TryGet(pageIndex, zoom, out var cached) && cached != null)
            {
                ApplyPageBitmap(pageIndex, cached, gen, doc, job.Sharp);
                continue;
            }

            var flight = (pageIndex, zoomKey, gen);
            lock (_inFlightGate)
            {
                if (!_inFlight.Add(flight))
                    continue;
            }

            var isSharp = job.Sharp;
            _ = Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested || gen != _renderGeneration)
                        return;
                    var bmp = doc.RenderPage(pageIndex, zoom);
                    if (bmp == null || token.IsCancellationRequested || gen != _renderGeneration)
                        return;
                    _bitmapCache.Set(pageIndex, zoom, bmp);
                    Dispatcher.UIThread.Post(() => ApplyPageBitmap(pageIndex, bmp, gen, doc, isSharp));
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"Page render failed {pageIndex}@{zoom:0.##}: {ex.Message}");
                }
                finally
                {
                    lock (_inFlightGate)
                        _inFlight.Remove(flight);
                }
            }, token);
        }
    }

    private void ApplyPageBitmap(int pageIndex, PdfPixelBuffer bmp, int gen, IPdfDocument doc, bool isSharp)
    {
        if (gen != _renderGeneration || _doc != doc)
            return;
        if (pageIndex < 0 || pageIndex >= _pageImages.Length)
            return;
        var alreadySharp = pageIndex < _pageShowingSharp.Length && _pageShowingSharp[pageIndex];
        if (PageRenderPlanner.ShouldKeepExistingBitmap(!isSharp, alreadySharp))
            return;
        _pageImages[pageIndex].Source = PdfPixelBufferAvalonia.ToBitmap(bmp);
        if (pageIndex < _pageShowingSharp.Length)
            _pageShowingSharp[pageIndex] = isSharp;
    }

    private void ScheduleDebouncedScrollRender()
    {
        if (_scrollRenderTimer == null)
        {
            _scrollRenderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PageRenderPlanner.ScrollRenderDebounceMs)
            };
            _scrollRenderTimer.Tick += (_, _) =>
            {
                _scrollRenderTimer.Stop();
                SchedulePageRenders(includePreviews: false);
            };
        }

        _scrollRenderTimer.Stop();
        _scrollRenderTimer.Start();
    }

    private void InvalidatePageTops() => _pageTopsDirty = true;

    private void EnsurePageTops()
    {
        var n = PageHost.Children.Count;
        if (!_pageTopsDirty && _pageTops.Length == n)
            return;
        if (_pageTops.Length != n)
            _pageTops = new double[n];
        for (var i = 0; i < n; i++)
        {
            if (PageHost.Children[i] is Control el)
            {
                var p = el.TranslatePoint(new Point(0, 0), PageHost);
                _pageTops[i] = p?.Y ?? 0;
            }
        }

        _pageTopsDirty = false;
    }

    private void ZoomIn_Click(object? sender, RoutedEventArgs e) => ApplyZoom(ZoomHelper.ZoomIn(_zoom));
    private void ZoomOut_Click(object? sender, RoutedEventArgs e) => ApplyZoom(ZoomHelper.ZoomOut(_zoom));
    private void ZoomReset_Click(object? sender, RoutedEventArgs e) => ApplyZoom(ZoomHelper.DefaultZoom);

    private void FitWidth_Click(object? sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;
        var (w, _) = _doc.GetPageSize(_currentPage);
        ApplyZoom(ZoomHelper.FitWidth(w, PageScroller.Viewport.Width, padding: ScrollerPadding));
    }

    private void FitPage_Click(object? sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;
        var (w, h) = _doc.GetPageSize(_currentPage);
        ApplyZoom(ZoomHelper.FitPage(w, h, PageScroller.Viewport.Width, PageScroller.Viewport.Height,
            padding: ScrollerPadding));
    }

    private void FindToggle_Click(object? sender, RoutedEventArgs e)
    {
        FindPanel.IsVisible = !FindPanel.IsVisible;
        if (FindPanel.IsVisible)
            FindBox.Focus();
    }

    private void FindBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RunFind(reset: true);
            FindNext_Click(sender, e);
        }
    }

    private void RunFind(bool reset)
    {
        _findHits = FindHelper.FindAll(_pageTexts, FindBox.Text ?? string.Empty);
        if (reset)
            _findIndex = -1;
        FindStatus.Text = _findHits.Count == 0 ? "No matches" : $"{_findHits.Count} matches";
        if (_findHits.Count == 0)
            ApplyFindHitChrome();
    }

    private void FindNext_Click(object? sender, RoutedEventArgs e)
    {
        RunFind(reset: false);
        _findIndex = FindHelper.NextIndex(_findHits.Count, _findIndex);
        JumpToHit();
    }

    private void FindPrev_Click(object? sender, RoutedEventArgs e)
    {
        RunFind(reset: false);
        _findIndex = FindHelper.PrevIndex(_findHits.Count, _findIndex);
        JumpToHit();
    }

    private void JumpToHit()
    {
        if (_findIndex < 0 || _findIndex >= _findHits.Count)
            return;
        var hit = _findHits[_findIndex];
        FindStatus.Text = $"{_findIndex + 1} / {_findHits.Count}";
        ApplyFindHitChrome();
        GoToPage(hit.PageIndex);
    }

    private void ApplyFindHitChrome()
    {
        var page = FindHelper.CurrentHitPage(_findHits, _findIndex);
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

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;

        if (PdfPageExtract.ShouldOfferSaveAsChoice(_selection.Count))
        {
            SaveAsSelectedRadio.Content = $"Selected pages ({_selection.Count})";
            SaveAsSelectedRadio.IsChecked = true;
            SaveAsChoiceOverlay.IsVisible = true;
            return;
        }

        HideSaveAsChoice();
        await SaveWholeDocument();
    }

    private async void SaveAsChoiceSave_Click(object? sender, RoutedEventArgs e)
    {
        var selected = SaveAsSelectedRadio.IsChecked == true;
        HideSaveAsChoice();
        if (selected)
            await SaveSelectedPages();
        else
            await SaveWholeDocument();
    }

    private void SaveAsChoiceCancel_Click(object? sender, RoutedEventArgs e) => HideSaveAsChoice();

    private void SaveAsChoiceDim_PointerPressed(object? sender, PointerPressedEventArgs e) => HideSaveAsChoice();

    private void SaveAsChoiceCard_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        e.Handled = true;

    private async void SavePagesAs_Click(object? sender, RoutedEventArgs e)
    {
        HideSaveAsChoice();
        await SaveSelectedPages();
    }

    private void HideSaveAsChoice()
    {
        if (SaveAsChoiceOverlay != null)
            SaveAsChoiceOverlay.IsVisible = false;
    }

    private async Task SaveWholeDocument()
    {
        if (_doc == null)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = Path.GetFileName(_doc.FilePath),
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { PdfFileType }
        });
        var dest = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(dest))
            return;

        try
        {
            File.Copy(_doc.FilePath, dest, overwrite: true);
        }
        catch (Exception ex)
        {
            ShowError($"Save As failed: {ex.Message}");
        }
    }

    private async Task SaveSelectedPages()
    {
        if (_doc == null)
            return;

        var pages = PdfPageExtract.PagesForExtract(_selection.Pages, _currentPage, _doc.PageCount);
        if (pages.Count == 0)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save pages as…",
            SuggestedFileName = PdfPageExtract.SuggestFileName(_doc.FilePath, pages),
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { PdfFileType }
        });
        var dest = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(dest))
            return;

        try
        {
            _doc.ExtractPages(pages, dest);
        }
        catch (Exception ex)
        {
            ShowError($"Save pages as… failed: {ex.Message}");
        }
    }

    private void PageScroller_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_doc == null || PageHost.Children.Count == 0)
            return;

        if (e.ExtentDelta.Y != 0 || e.ExtentDelta.X != 0)
            InvalidatePageTops();

        EnsurePageTops();
        var center = PageScroller.Offset.Y + PageScroller.Viewport.Height / 3;
        var best = ScrollPagePicker.PickCurrentPageNear(_pageTops, center, _currentPage);

        if (best != _currentPage)
        {
            _currentPage = best;
            if (!PageSelection.IsMulti(_selection))
            {
                _selection = PageSelection.FollowCurrent(best, _doc.PageCount);
                RefreshThumbSelection();
            }

            UpdateStatus();
            ScheduleDebouncedScrollRender();
        }
    }

    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SaveAsChoiceOverlay.IsVisible)
        {
            HideSaveAsChoice();
            e.Handled = true;
            return;
        }

        if (FocusManager?.GetFocusedElement() is TextBox && e.Key is not (Key.Escape))
        {
            if (IsCommand(e) && e.Key == Key.O)
            {
                await OpenWithDialog();
                e.Handled = true;
            }
            else if (IsCommand(e) && e.Key == Key.F)
            {
                FindToggle_Click(sender, e);
                e.Handled = true;
            }

            return;
        }

        if (IsCommand(e) && e.Key == Key.O)
        {
            await OpenWithDialog();
            e.Handled = true;
        }
        else if (IsCommand(e) && e.Key == Key.F)
        {
            FindToggle_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key is Key.PageDown or Key.Right)
        {
            GoToPage(_currentPage + 1);
            e.Handled = true;
        }
        else if (e.Key is Key.PageUp or Key.Left)
        {
            GoToPage(_currentPage - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Home)
        {
            GoToPage(0);
            e.Handled = true;
        }
        else if (e.Key == Key.End && _doc != null)
        {
            GoToPage(_doc.PageCount - 1);
            e.Handled = true;
        }
    }

    private static bool IsCommand(KeyEventArgs e) =>
        e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);

    private void ShowError(string message)
    {
        ErrorBannerText.Text = message;
        ErrorBanner.IsVisible = true;
    }

    private void HideError() => ErrorBanner.IsVisible = false;

    private void DismissError_Click(object? sender, RoutedEventArgs e) => HideError();

    private sealed record RecentRow(string DisplayName, string Path, string RelativeTime);
}
