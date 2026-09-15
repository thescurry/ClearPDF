using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClearPDF.Helpers;
using ClearPDF.Services;
using ClearPDF.Views;
using Microsoft.Win32;

namespace ClearPDF;

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

    /// <summary>Page Image controls — Source swapped async; last frame stays visible.</summary>
    private Image[] _pageImages = Array.Empty<Image>();

    /// <summary>Thumbnail chrome borders (selection only — bitmaps never rebuilt on scroll).</summary>
    private Border[] _thumbFrames = Array.Empty<Border>();

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

    /// <summary>Thumbnail Image controls — filled by a pipeline separate from main pages.</summary>
    private Image[] _thumbImages = Array.Empty<Image>();

    /// <summary>Thumb-only cancel/gen — page CancelRenders must not abort thumb fills.</summary>
    private CancellationTokenSource? _thumbCts;
    private int _thumbGeneration;

    private const double ScrollerPadding = 20;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshRecentList();
        // Explorer "Open with" / double-click / `ClearPDF.exe "path.pdf"` — argv after exe.
        var cliPdf = CommandLinePdfArgs.TryGetPdfFromProcessCommandLine();
        if (cliPdf != null)
            OpenPath(cliPdf);
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedPdf(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var path = TryGetDroppedPdf(e);
        e.Handled = true;
        if (path != null)
            OpenPath(path);
    }

    /// <summary>First existing .pdf from a file-drop (Explorer drag onto the window).</summary>
    private static string? TryGetDroppedPdf(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return null;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return null;
        return CommandLinePdfArgs.TryGetFirstExistingPdf(files);
    }

    private void RefreshRecentList()
    {
        var items = _recents.Load()
            .Select(e => new RecentRow(e.DisplayName, e.Path, RecentFilesService.FormatRelativeTime(e.OpenedUtc)))
            .ToList();
        RecentList.ItemsSource = items;
        NoRecentText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenWithDialog();

    private void DuckWaffles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DuckWafflesWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void RecentItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            OpenPath(path);
    }

    private void OpenWithDialog()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open PDF",
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true)
            OpenPath(dlg.FileName);
    }

    private void OpenPath(string path, string? password = null)
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
                // Wrong/missing password — prompt again (view unlock only). Never store.
                AppLog.Warn($"Password error opening '{path}': {ex.Message}");
                if (!PdfUnlockPolicy.ShouldPromptAgainAfterFailure)
                    return;
                pwd = PromptPassword();
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

    private string? PromptPassword()
    {
        // PdfUnlockPolicy: prompt exists, view unlock only, never persist.
        var dlg = new PasswordDialog { Owner = this };
        return dlg.ShowDialog() == true ? dlg.Password : null;
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
        InvalidatePageTops();
        _doc?.Dispose();
        _doc = doc;
        _zoom = ZoomHelper.DefaultZoom;
        _currentPage = 0;
        _findHits = Array.Empty<FindHelper.Hit>();
        _findIndex = -1;
        _thumbsBuilt = false;
        FindPanel.Visibility = Visibility.Collapsed;

        _pageTexts = new string[doc.PageCount];
        for (var i = 0; i < doc.PageCount; i++)
            _pageTexts[i] = doc.GetPageText(i);

        _recents.Add(path);
        RefreshRecentList();

        EmptyView.Visibility = Visibility.Collapsed;
        ReaderView.Visibility = Visibility.Visible;
        ToolbarBorder.Visibility = Visibility.Visible;
        StatusBar.Visibility = Visibility.Visible;
        Title = $"ClearPDF — {Path.GetFileName(path)}";

        if (!DocnetPdfDocumentFactory.AreNativesAvailable)
        {
            ShowError("PDFium natives not loaded — showing stub pages. On Windows 10+, run `dotnet restore` / `dotnet run` so Docnet.Core can ship pdfium.dll.");
        }

        PageHost.LayoutTransform = Transform.Identity;
        _renderedZoom = _zoom;
        BuildPageShells();
        BuildThumbnailShells();
        UpdateStatus();
        // Sharp near page 0; low-res previews for the rest — all off UI thread.
        // Thumbs use a separate generation so this cancel does not wipe them.
        SchedulePageRenders(includePreviews: true);
        // Background low-res thumbs for the whole doc — never reuse main-page rasters.
        ScheduleThumbFills();
    }

    /// <summary>
    /// Build page Borders/Images once. Sizes follow PDF points × rendered zoom.
    /// Bitmaps are filled asynchronously; prior Source stays until swap.
    /// </summary>
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
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true,
                // Light placeholder while first decode runs.
                Source = null
            };
            _pageImages[i] = image;

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 0, 0, 16),
                Width = displayW,
                Height = displayH,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.22,
                    Color = Colors.Black
                },
                Child = image,
                Tag = i
            };
            PageHost.Children.Add(border);
        }

        _pageShowingSharp = new bool[_doc.PageCount];
        _findHitPage = -1;
        InvalidatePageTops();
    }

    /// <summary>
    /// Resize page shells to match committed rendered zoom (LayoutTransform cleared after).
    /// Keeps existing Image.Source so the last frame stays visible during async re-render.
    /// </summary>
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

    /// <summary>Fixed thumb rail width in DIPs; height follows each page's PDF-point aspect.</summary>
    private const double ThumbWidthPx = 120;

    /// <summary>Tight spacing between thumb rows (no oversized letterbox gaps).</summary>
    private const double ThumbRowGapPx = 4;

    /// <summary>
    /// Build thumbnail chrome shells once at open. Bitmaps are filled later by
    /// <see cref="ScheduleThumbFills"/> — scroll/zoom only touch selection chrome.
    /// Row height = <see cref="ThumbWidthPx"/> × (pageHeight/pageWidth) from
    /// <see cref="IPdfDocument.GetPageSize"/> so Uniform/Fill never letterboxes
    /// inside a forced square or fixed 120×156 cell.
    /// </summary>
    private void BuildThumbnailShells()
    {
        if (_doc == null)
            return;

        ThumbnailList.Items.Clear();
        _thumbFrames = new Border[_doc.PageCount];
        _thumbImages = new Image[_doc.PageCount];

        for (var i = 0; i < _doc.PageCount; i++)
        {
            var pageIndex = i;
            var (pw, ph) = _doc.GetPageSize(i);
            if (pw <= 0)
                pw = PdfPageSize.LetterWidthPoints;
            if (ph <= 0)
                ph = PdfPageSize.LetterHeightPoints;
            var thumbHeight = ThumbWidthPx * (ph / pw);

            // Size the Image to page aspect — Stretch.Fill then paints edge-to-edge
            // (no white letterbox). Do not force a square or fixed 120×156 cell.
            var image = new Image
            {
                Stretch = Stretch.Fill,
                Width = ThumbWidthPx,
                Height = thumbHeight,
                Source = null
            };
            _thumbImages[i] = image;

            var frame = new Border
            {
                BorderThickness = new Thickness(2),
                BorderBrush = ThumbBorderBrush(i == _currentPage),
                Margin = new Thickness(0, 0, 0, ThumbRowGapPx),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.White,
                Cursor = Cursors.Hand,
                Child = new Grid
                {
                    Width = ThumbWidthPx,
                    Height = thumbHeight,
                    Children =
                    {
                        image,
                        CreateThumbBadge(i, i == _currentPage)
                    }
                },
                Tag = pageIndex
            };

            frame.MouseLeftButtonUp += (_, _) => GoToPage(pageIndex);
            _thumbFrames[i] = frame;
            ThumbnailList.Items.Add(frame);
        }

        _thumbsBuilt = true;
        _highlightedThumb = _currentPage;
    }

    /// <summary>
    /// Background fill for every thumb at <see cref="PdfRenderScale.ThumbZoom"/>.
    /// Uses a dedicated cancel/gen and thumb cache — never main-page high-DPI rasters.
    /// </summary>
    private void ScheduleThumbFills()
    {
        if (_doc == null || _thumbImages.Length == 0)
            return;

        CancelThumbRenders();
        var token = _thumbCts!.Token;
        var gen = _thumbGeneration;
        var doc = _doc;
        var count = doc.PageCount;

        // One sequential worker — Docnet is process-gated; flooding Task.Run
        // contended with sharp page rasters on image-heavy docs.
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
                    Dispatcher.BeginInvoke(() => ApplyThumbBitmap(idx, hit, gen, doc));
                    continue;
                }

                try
                {
                    var bmp = doc.RenderPage(pageIndex, PdfRenderScale.ThumbZoom);
                    if (bmp == null || token.IsCancellationRequested || gen != _thumbGeneration)
                        return;
                    _bitmapCache.SetThumb(pageIndex, bmp);
                    Dispatcher.BeginInvoke(() => ApplyThumbBitmap(pageIndex, bmp, gen, doc));
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"Thumb render failed page {pageIndex}: {ex.Message}");
                }
            }
        }, token);
    }

    private void ApplyThumbBitmap(int pageIndex, BitmapSource bmp, int gen, IPdfDocument doc)
    {
        if (gen != _thumbGeneration || _doc != doc)
            return;
        if (pageIndex < 0 || pageIndex >= _thumbImages.Length)
            return;
        // Swap Source only — never rebuild the shell (selection chrome must not clear thumbs).
        _thumbImages[pageIndex].Source = bmp;
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

    private Border CreateThumbBadge(int pageIndex, bool selected)
    {
        return new Border
        {
            Width = 18,
            Height = 18,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(4),
            Background = selected
                ? (Brush)FindResource("AccentBrush")
                : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            Child = new TextBlock
            {
                Text = (pageIndex + 1).ToString(),
                Foreground = Brushes.White,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Tag = "badge"
        };
    }

    private Brush ThumbBorderBrush(bool selected) =>
        selected
            ? (Brush)FindResource("AccentBrush")
            : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

    /// <summary>Update selection chrome only — no Docnet calls, no full-rail walk.</summary>
    private void HighlightCurrentThumbnail()
    {
        if (!_thumbsBuilt || _thumbFrames.Length == 0)
            return;

        if (_highlightedThumb >= 0 &&
            _highlightedThumb < _thumbFrames.Length &&
            _highlightedThumb != _currentPage)
        {
            ApplyThumbChrome(_highlightedThumb, selected: false);
        }

        if (_currentPage >= 0 && _currentPage < _thumbFrames.Length)
            ApplyThumbChrome(_currentPage, selected: true);

        _highlightedThumb = _currentPage;
    }

    private void ApplyThumbChrome(int index, bool selected)
    {
        _thumbFrames[index].BorderBrush = ThumbBorderBrush(selected);
        if (_thumbFrames[index].Child is not Grid grid)
            return;
        foreach (var child in grid.Children)
        {
            if (child is Border { Tag: "badge" } badge)
            {
                badge.Background = selected
                    ? (Brush)FindResource("AccentBrush")
                    : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
            }
        }
    }

    private void GoToPage(int pageIndex)
    {
        if (_doc == null || pageIndex < 0 || pageIndex >= _doc.PageCount)
            return;

        _currentPage = pageIndex;
        HighlightCurrentThumbnail();
        UpdateStatus();

        if (pageIndex < PageHost.Children.Count && PageHost.Children[pageIndex] is FrameworkElement el)
            el.BringIntoView();

        // Ensure sharp bitmaps around the target — do not rebuild all pages.
        SchedulePageRenders(includePreviews: false);
    }

    private void UpdateStatus()
    {
        var count = _doc?.PageCount ?? 0;
        PageStatusText.Text = count == 0 ? "—" : $"{_currentPage + 1} / {count}";
        var pct = $"{(int)Math.Round(_zoom * 100)}%";
        ZoomLabel.Content = pct;
        ZoomStatusText.Text = pct;
    }

    private void ApplyZoom(double zoom)
    {
        _zoom = ZoomHelper.Clamp(zoom);
        ApplyInstantZoomTransform();
        UpdateStatus();
        ScheduleDebouncedPageRender();
    }

    /// <summary>
    /// Immediate visual zoom via LayoutTransform so rapid clicks feel instant.
    /// Full PDFium re-render is deferred (see ScheduleDebouncedPageRender).
    /// </summary>
    private void ApplyInstantZoomTransform()
    {
        if (_renderedZoom <= 0 || PageHost.Children.Count == 0)
        {
            PageHost.LayoutTransform = Transform.Identity;
            return;
        }

        var factor = _zoom / _renderedZoom;
        if (Math.Abs(factor - 1.0) < 0.001)
            PageHost.LayoutTransform = Transform.Identity;
        else
            PageHost.LayoutTransform = new ScaleTransform(factor, factor);
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

    /// <summary>
    /// Commit zoom: resize shells, clear LayoutTransform, async re-raster nearby pages.
    /// Thumbnails stay at fixed thumb zoom (never rebuilt here).
    /// </summary>
    private void CommitZoomRender()
    {
        if (_doc == null)
            return;

        if (Math.Abs(_renderedZoom - _zoom) < 0.001)
        {
            PageHost.LayoutTransform = Transform.Identity;
            return;
        }

        _renderedZoom = _zoom;
        ResizePageShellsToRenderedZoom();
        PageHost.LayoutTransform = Transform.Identity;

        if (_currentPage < PageHost.Children.Count &&
            PageHost.Children[_currentPage] is FrameworkElement el)
        {
            el.BringIntoView();
        }

        // PageRenderPlanner.ShouldRebuildThumbsOnZoom is false — do not touch thumb shells.
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

    /// <summary>
    /// Queue background rasters. Nearby pages get sharp (quantized) zoom; far pages
    /// optionally get a low-res preview. Scroll never cancels in-window work and
    /// never rebuilds thumbnails.
    /// </summary>
    private void SchedulePageRenders(bool includePreviews)
    {
        if (_doc == null || _pageImages.Length == 0)
            return;

        // Zoom/open may cancel everything. Scroll keeps in-flight window rasters.
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
                    Dispatcher.BeginInvoke(() => ApplyPageBitmap(pageIndex, bmp, gen, doc, isSharp));
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

    private void ApplyPageBitmap(int pageIndex, BitmapSource bmp, int gen, IPdfDocument doc, bool isSharp)
    {
        if (gen != _renderGeneration || _doc != doc)
            return;
        if (pageIndex < 0 || pageIndex >= _pageImages.Length)
            return;
        var alreadySharp = pageIndex < _pageShowingSharp.Length && _pageShowingSharp[pageIndex];
        if (PageRenderPlanner.ShouldKeepExistingBitmap(!isSharp, alreadySharp))
            return;
        // Keep last frame until we have something to show — then swap.
        _pageImages[pageIndex].Source = bmp;
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
            if (PageHost.Children[i] is FrameworkElement el)
                _pageTops[i] = el.TransformToAncestor(PageHost).Transform(new Point(0, 0)).Y;
        }

        _pageTopsDirty = false;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ApplyZoom(ZoomHelper.ZoomIn(_zoom));
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ApplyZoom(ZoomHelper.ZoomOut(_zoom));

    private void ZoomReset_Click(object sender, RoutedEventArgs e) => ApplyZoom(ZoomHelper.DefaultZoom);

    private void FitWidth_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;
        var (w, _) = _doc.GetPageSize(_currentPage);
        var viewport = PageScroller.ViewportWidth;
        ApplyZoom(ZoomHelper.FitWidth(w, viewport, padding: ScrollerPadding));
    }

    private void FitPage_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;
        var (w, h) = _doc.GetPageSize(_currentPage);
        ApplyZoom(ZoomHelper.FitPage(w, h, PageScroller.ViewportWidth, PageScroller.ViewportHeight,
            padding: ScrollerPadding));
    }

    private void FindToggle_Click(object sender, RoutedEventArgs e)
    {
        FindPanel.Visibility = FindPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (FindPanel.Visibility == Visibility.Visible)
            FindBox.Focus();
    }

    private void FindBox_KeyDown(object sender, KeyEventArgs e)
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

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        RunFind(reset: false);
        _findIndex = FindHelper.NextIndex(_findHits.Count, _findIndex);
        JumpToHit();
    }

    private void FindPrev_Click(object sender, RoutedEventArgs e)
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

    /// <summary>One muted accent on the current find-hit page (same token as selected thumb).</summary>
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
            border.BorderBrush = (Brush)FindResource("AccentBrush");
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;

        var dlg = new SaveFileDialog
        {
            Title = "Save As",
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = Path.GetFileName(_doc.FilePath)
        };
        if (dlg.ShowDialog(this) != true)
            return;

        try
        {
            File.Copy(_doc.FilePath, dlg.FileName, overwrite: true);
        }
        catch (Exception ex)
        {
            ShowError($"Save As failed: {ex.Message}");
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;

        try
        {
            // Enable Pages (from–to) and Selection (= current highlighted thumb page).
            var dlg = new PrintDialog
            {
                UserPageRangeEnabled = true,
                SelectedPagesEnabled = true,
                MinPage = 1,
                MaxPage = (uint)Math.Max(1, _doc.PageCount),
            };
            if (dlg.ShowDialog() != true)
                return;

            // PrintDialog.PrintDocument does not apply PageRange itself — map into paginator.
            // Selection = current left-rail / viewer page (PrintPageRange).
            var mode = dlg.PageRangeSelection switch
            {
                PageRangeSelection.SelectedPages => PrintRangeMode.Selection,
                PageRangeSelection.UserPages => PrintRangeMode.UserPages,
                _ => PrintRangeMode.AllPages
            };
            var (startPage, endPage) = PrintPageRange.Resolve(
                mode, _currentPage, _doc.PageCount,
                dlg.PageRange.PageFrom, dlg.PageRange.PageTo);

            var paginator = new PdfPrintPaginator(
                _doc, dlg.PrintableAreaWidth, dlg.PrintableAreaHeight, startPage, endPage);
            dlg.PrintDocument(paginator, Path.GetFileName(_doc.FilePath));
        }
        catch (Exception ex)
        {
            ShowError($"Print failed: {ex.Message}");
        }
    }

    private void PageScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_doc == null || PageHost.Children.Count == 0)
            return;

        if (e.ExtentHeightChange != 0 || e.ExtentWidthChange != 0)
            InvalidatePageTops();

        EnsurePageTops();
        var center = PageScroller.VerticalOffset + PageScroller.ViewportHeight / 3;
        var best = ScrollPagePicker.PickCurrentPageNear(_pageTops, center, _currentPage);

        if (best != _currentPage)
        {
            _currentPage = best;
            // Selection chrome only — do NOT rebuild thumbnails or cancel in-window rasters.
            HighlightCurrentThumbnail();
            UpdateStatus();
            ScheduleDebouncedScrollRender();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            OpenWithDialog();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            FindToggle_Click(sender, e);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.P)
        {
            Print_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown || e.Key == Key.Right)
        {
            GoToPage(_currentPage + 1);
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp || e.Key == Key.Left)
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

    private void ShowError(string message)
    {
        ErrorBannerText.Text = message;
        ErrorBanner.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorBanner.Visibility = Visibility.Collapsed;

    private void DismissError_Click(object sender, RoutedEventArgs e) => HideError();

    private sealed record RecentRow(string DisplayName, string Path, string RelativeTime);
}

/// <summary>Simple one-page-per-sheet print path using rendered bitmaps.
/// Optional [_startPage, _endPage] (0-based inclusive) limits which PDF pages are printed.</summary>
internal sealed class PdfPrintPaginator : System.Windows.Documents.DocumentPaginator
{
    private readonly IPdfDocument _doc;
    private readonly Size _pageSize;
    private readonly int _startPage;
    private readonly int _endPage;

    public PdfPrintPaginator(IPdfDocument doc, double width, double height, int startPage = 0, int endPage = -1)
    {
        _doc = doc;
        _pageSize = new Size(width, height);
        _startPage = Math.Clamp(startPage, 0, Math.Max(0, doc.PageCount - 1));
        _endPage = endPage < 0
            ? doc.PageCount - 1
            : Math.Clamp(endPage, _startPage, Math.Max(0, doc.PageCount - 1));
    }

    public override bool IsPageCountValid => true;
    public override int PageCount => _doc.PageCount == 0 ? 0 : _endPage - _startPage + 1;
    public override Size PageSize
    {
        get => _pageSize;
        set { }
    }

    public override IDocumentPaginatorSource Source => null!;

    public override System.Windows.Documents.DocumentPage GetPage(int pageNumber)
    {
        // pageNumber is the print-job index (0..PageCount-1); map into the PDF page index.
        var docPage = _startPage + pageNumber;
        var (pw, ph) = _doc.GetPageSize(docPage);
        var scale = Math.Min(_pageSize.Width / pw, _pageSize.Height / ph);
        var bmp = _doc.RenderPage(docPage, scale);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (bmp != null)
            {
                // Fit into printable area using points-based layout size.
                var drawW = pw * scale;
                var drawH = ph * scale;
                dc.DrawImage(bmp, new Rect(0, 0, drawW, drawH));
            }
        }
        return new System.Windows.Documents.DocumentPage(visual);
    }
}
