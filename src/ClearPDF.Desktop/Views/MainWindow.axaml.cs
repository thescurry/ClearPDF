using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ClearPDF.Desktop.Services;
using ClearPDF.Helpers;
using ClearPDF.Pdfium;
using ClearPDF.Services;

namespace ClearPDF.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly IPdfRasterDocumentFactory _pdfFactory = new DocnetPdfRasterDocumentFactory();
    private readonly RecentFilesService _recents = new();
    private readonly PageRasterCache<Avalonia.Media.Imaging.Bitmap> _cache = new();

    private IPdfRasterDocument? _doc;
    private double _zoom = ZoomHelper.DefaultZoom;
    private int _currentPage;
    private Image[] _pageImages = Array.Empty<Image>();
    private double[] _pageTops = Array.Empty<double>();
    private bool _pageTopsDirty = true;
    private CancellationTokenSource? _renderCts;
    private DispatcherTimer? _zoomRenderTimer;
    private bool[] _pageShowingSharp = Array.Empty<bool>();

    public MainWindow()
    {
        InitializeComponent();
        AppMenu.IsVisible = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        WireNativeMenu();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, Window_DragOver);
        AddHandler(DragDrop.DropEvent, Window_Drop);

        Opened += (_, _) =>
        {
            RefreshRecentList();
            var cliPdf = CommandLinePdfArgs.TryGetPdfFromProcessCommandLine();
            if (cliPdf != null)
                OpenPath(cliPdf);
        };

        Closed += (_, _) =>
        {
            _zoomRenderTimer?.Stop();
            CancelRenders();
            _doc?.Dispose();
            _cache.Clear();
        };
    }

    private void WireNativeMenu()
    {
        var file = new NativeMenu();
        file.Items.Add(Item("Open…", TryGesture("Cmd+O"), () => _ = OpenWithDialogAsync()));
        file.Items.Add(Item("Save As…", TryGesture("Cmd+S"), () => _ = SaveAsAsync()));
        file.Items.Add(new NativeMenuItemSeparator());
        file.Items.Add(Item("Print…", TryGesture("Cmd+P"), () => _ = PrintAsync()));
        file.Items.Add(Item("Close", TryGesture("Cmd+W"), () => CloseDoc_Click(this, new RoutedEventArgs())));

        var view = new NativeMenu();
        view.Items.Add(Item("Zoom In", TryGesture("Cmd+Plus"), () => SetZoom(ZoomHelper.ZoomIn(_zoom))));
        view.Items.Add(Item("Zoom Out", TryGesture("Cmd+-"), () => SetZoom(ZoomHelper.ZoomOut(_zoom))));
        view.Items.Add(Item("Actual Size", TryGesture("Cmd+0"), () => SetZoom(ZoomHelper.DefaultZoom)));
        view.Items.Add(Item("Fit Width", null, () => FitWidth_Click(this, new RoutedEventArgs())));
        view.Items.Add(Item("Fit Page", null, () => FitPage_Click(this, new RoutedEventArgs())));

        var root = new NativeMenu();
        root.Items.Add(new NativeMenuItem("File") { Menu = file });
        root.Items.Add(new NativeMenuItem("View") { Menu = view });
        NativeMenu.SetMenu(this, root);
    }

    private static KeyGesture? TryGesture(string text)
    {
        try
        {
            return KeyGesture.Parse(text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static NativeMenuItem Item(string header, KeyGesture? gesture, Action action)
    {
        var item = new NativeMenuItem(header) { Gesture = gesture };
        item.Click += (_, _) => action();
        return item;
    }

    public void ShowAbout()
    {
        _ = new Window
        {
            Title = "About ClearPDF",
            Width = 360,
            Height = 160,
            Content = new TextBlock
            {
                Text = ProductInfo.About + "\nIntel macOS MVP — view / save / print.\nScanner is out of scope.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(20)
            },
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        }.ShowDialog(this);
    }

    private void About_Click(object? sender, RoutedEventArgs e) => ShowAbout();

    private void RefreshRecentList()
    {
        var items = _recents.Load()
            .Select(e => new RecentRow(e.DisplayName, e.Path, RecentFilesService.FormatRelativeTime(e.OpenedUtc)))
            .ToList();
        RecentList.ItemsSource = items;
        NoRecentText.IsVisible = items.Count == 0;
    }

    private void Open_Click(object? sender, RoutedEventArgs e) => _ = OpenWithDialogAsync();

    private void RecentItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            OpenPath(path);
    }

    private async Task OpenWithDialogAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PDF",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } }
            }
        });
        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (!string.IsNullOrWhiteSpace(path))
            OpenPath(path);
    }

    public void OpenPath(string path, string? password = null) =>
        _ = OpenPathAsync(path, password);

    private async Task OpenPathAsync(string path, string? password = null)
    {
        HideError();
        var pwd = password;
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
                DesktopLog.Warn($"Password error opening '{path}': {ex.Message}");
                if (!PdfUnlockPolicy.ShouldPromptAgainAfterFailure)
                    return;
                pwd = await PromptPasswordAsync();
                if (pwd == null)
                    return;
            }
            catch (Exception ex)
            {
                DesktopLog.Error($"Open failed: {path}", ex);
                ShowError(ex.Message);
                return;
            }
        }
    }

    private async Task<string?> PromptPasswordAsync()
    {
        var dlg = new PasswordWindow();
        var ok = await dlg.ShowDialog<bool>(this);
        return ok ? dlg.Password : null;
    }

    private void SetDocument(IPdfRasterDocument doc, string path)
    {
        CancelRenders();
        _cache.Clear();
        _doc?.Dispose();
        _doc = doc;
        _currentPage = 0;
        _zoom = ZoomHelper.DefaultZoom;
        _recents.Add(path);
        Title = Path.GetFileName(path) + " — ClearPDF";

        EmptyView.IsVisible = false;
        PageScroller.IsVisible = true;
        ToolbarBorder.IsVisible = true;
        StatusBar.IsVisible = true;

        BuildPagePlaceholders();
        UpdateStatus();
        _ = RenderVisibleAsync(includePreviews: true);
    }

    private void BuildPagePlaceholders()
    {
        PageHost.Children.Clear();
        if (_doc == null)
        {
            _pageImages = Array.Empty<Image>();
            _pageShowingSharp = Array.Empty<bool>();
            _pageTops = Array.Empty<double>();
            return;
        }

        _pageImages = new Image[_doc.PageCount];
        _pageShowingSharp = new bool[_doc.PageCount];
        _pageTops = new double[_doc.PageCount];
        _pageTopsDirty = true;

        for (var i = 0; i < _doc.PageCount; i++)
        {
            var (wPts, hPts) = _doc.GetPageSize(i);
            var img = new Image
            {
                Width = Math.Max(1, wPts * _zoom),
                Height = Math.Max(1, hPts * _zoom),
                Stretch = Avalonia.Media.Stretch.Fill
            };
            _pageImages[i] = img;
            var border = new Border
            {
                Child = img,
                BoxShadow = new Avalonia.Media.BoxShadows(
                    new Avalonia.Media.BoxShadow
                    {
                        Blur = 8,
                        OffsetY = 1,
                        Color = Avalonia.Media.Color.FromArgb(40, 0, 0, 0)
                    }),
                Background = Avalonia.Media.Brushes.White
            };
            PageHost.Children.Add(border);
        }
    }

    private void ApplyZoomToPlaceholders()
    {
        if (_doc == null)
            return;
        for (var i = 0; i < _pageImages.Length; i++)
        {
            var (wPts, hPts) = _doc.GetPageSize(i);
            _pageImages[i].Width = Math.Max(1, wPts * _zoom);
            _pageImages[i].Height = Math.Max(1, hPts * _zoom);
        }

        _pageTopsDirty = true;
        UpdateStatus();
    }

    private async Task RenderVisibleAsync(bool includePreviews)
    {
        if (_doc == null)
            return;

        CancelRenders();
        var cts = new CancellationTokenSource();
        _renderCts = cts;
        var token = cts.Token;
        var zoom = PdfRenderScale.QuantizeZoom(_zoom);
        var current = _currentPage;
        var pageCount = _doc.PageCount;
        _cache.FocusPage = current;

        var jobs = PageRenderPlanner.BuildJobs(current, pageCount, includePreviews);
        try
        {
            foreach (var (page, sharp) in jobs)
            {
                if (token.IsCancellationRequested)
                    return;

                var renderZoom = sharp ? zoom : PdfRenderScale.PreviewZoom;
                if (_cache.TryGet(page, PdfRenderScale.ZoomCacheKey(renderZoom), out var cached) && cached != null)
                {
                    ApplyBitmap(page, cached, sharp);
                    continue;
                }

                var doc = _doc;
                var raster = await Task.Run(() => doc.RenderPage(page, renderZoom), token);
                if (token.IsCancellationRequested)
                    return;

                var bmp = PdfBitmapFactory.FromRaster(raster);
                if (bmp == null)
                    continue;

                _cache.Set(page, PdfRenderScale.ZoomCacheKey(renderZoom), bmp);
                await Dispatcher.UIThread.InvokeAsync(() => ApplyBitmap(page, bmp, sharp));
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch (Exception ex)
        {
            DesktopLog.Error("Render failed", ex);
        }
    }

    private void ApplyBitmap(int page, Avalonia.Media.Imaging.Bitmap bmp, bool sharp)
    {
        if (page < 0 || page >= _pageImages.Length)
            return;
        if (PageRenderPlanner.ShouldKeepExistingBitmap(!sharp, _pageShowingSharp[page]))
            return;
        _pageImages[page].Source = bmp;
        if (sharp)
            _pageShowingSharp[page] = true;
    }

    private void CancelRenders()
    {
        try { _renderCts?.Cancel(); } catch { /* ignore */ }
        _renderCts?.Dispose();
        _renderCts = null;
    }

    private void ScheduleZoomRender()
    {
        _zoomRenderTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PageRenderPlanner.ZoomDebounceMs)
        };
        _zoomRenderTimer.Tick -= ZoomTimerOnTick;
        _zoomRenderTimer.Tick += ZoomTimerOnTick;
        _zoomRenderTimer.Stop();
        _zoomRenderTimer.Start();
    }

    private void ZoomTimerOnTick(object? sender, EventArgs e)
    {
        _zoomRenderTimer?.Stop();
        Array.Fill(_pageShowingSharp, false);
        _cache.ClearPages();
        _ = RenderVisibleAsync(includePreviews: true);
    }

    private void SetZoom(double zoom)
    {
        _zoom = ZoomHelper.Clamp(zoom);
        ApplyZoomToPlaceholders();
        ScheduleZoomRender();
    }

    private void ZoomIn_Click(object? sender, RoutedEventArgs e) => SetZoom(ZoomHelper.ZoomIn(_zoom));
    private void ZoomOut_Click(object? sender, RoutedEventArgs e) => SetZoom(ZoomHelper.ZoomOut(_zoom));
    private void ZoomReset_Click(object? sender, RoutedEventArgs e) => SetZoom(ZoomHelper.DefaultZoom);

    private void FitWidth_Click(object? sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;
        var (w, _) = _doc.GetPageSize(_currentPage);
        SetZoom(ZoomHelper.FitWidth(w, PageScroller.Viewport.Width, 56));
    }

    private void FitPage_Click(object? sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;
        var (w, h) = _doc.GetPageSize(_currentPage);
        SetZoom(ZoomHelper.FitPage(w, h, PageScroller.Viewport.Width, PageScroller.Viewport.Height, 56));
    }

    private void CloseDoc_Click(object? sender, RoutedEventArgs e)
    {
        if (_doc == null)
        {
            Close();
            return;
        }

        CancelRenders();
        _doc.Dispose();
        _doc = null;
        _cache.Clear();
        PageHost.Children.Clear();
        _pageImages = Array.Empty<Image>();
        Title = "ClearPDF";
        PageScroller.IsVisible = false;
        ToolbarBorder.IsVisible = false;
        StatusBar.IsVisible = false;
        EmptyView.IsVisible = true;
        RefreshRecentList();
    }

    private void SaveAs_Click(object? sender, RoutedEventArgs e) => _ = SaveAsAsync();

    private async Task SaveAsAsync()
    {
        if (_doc == null)
            return;

        var dest = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = Path.GetFileName(_doc.FilePath),
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } }
            }
        });
        if (dest == null)
            return;

        try
        {
            var target = dest.Path.LocalPath;
            File.Copy(_doc.FilePath, target, overwrite: true);
        }
        catch (Exception ex)
        {
            ShowError($"Save As failed: {ex.Message}");
        }
    }

    private void Print_Click(object? sender, RoutedEventArgs e) => _ = PrintAsync();

    private async Task PrintAsync()
    {
        if (_doc == null)
            return;

        var dlg = new PrintWindow(_doc.PageCount, _currentPage);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok || dlg.ChosenMode is not { } mode)
            return;

        var pages = PrintPageRange.ResolvePages(
            mode, _currentPage, _doc.PageCount, selectedPages0: null,
            dlg.From1Based, dlg.To1Based);
        if (pages.Count == 0)
            return;

        string? temp = null;
        try
        {
            var source = _doc.FilePath;
            var wholeDoc = pages.Count == _doc.PageCount
                           && pages[0] == 0
                           && pages[pages.Count - 1] == _doc.PageCount - 1;
            if (!wholeDoc)
            {
                temp = Path.Combine(Path.GetTempPath(), "ClearPDF-print-" + Guid.NewGuid().ToString("N") + ".pdf");
                _doc.ExtractPages(pages, temp);
                source = temp;
            }

            if (!CupsPrinter.IsAvailable())
            {
                ShowError("No system printer command (lp/lpr) is available on this Mac.");
                return;
            }

            await Task.Run(() => CupsPrinter.PrintPdf(source));
        }
        catch (Exception ex)
        {
            DesktopLog.Error("Print failed", ex);
            ShowError($"Print failed: {ex.Message}");
        }
        finally
        {
            if (temp != null)
            {
                try { File.Delete(temp); } catch { /* ignore */ }
            }
        }
    }

    private void PageScroller_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_doc == null || _pageImages.Length == 0)
            return;

        EnsurePageTops();
        var center = PageScroller.Offset.Y + PageScroller.Viewport.Height / 3;
        var best = ScrollPagePicker.PickCurrentPageNear(_pageTops, center, _currentPage);
        if (best == _currentPage)
            return;

        _currentPage = best;
        _cache.FocusPage = best;
        UpdateStatus();
        _ = RenderVisibleAsync(includePreviews: false);
    }

    private void EnsurePageTops()
    {
        if (!_pageTopsDirty || _pageImages.Length == 0)
            return;

        var y = 0.0;
        for (var i = 0; i < _pageImages.Length; i++)
        {
            _pageTops[i] = y;
            y += _pageImages[i].Height + 16;
        }

        _pageTopsDirty = false;
    }

    private void GoToPage(int page)
    {
        if (_doc == null)
            return;
        var target = Math.Clamp(page, 0, _doc.PageCount - 1);
        _currentPage = target;
        EnsurePageTops();
        PageScroller.Offset = new Avalonia.Vector(PageScroller.Offset.X, _pageTops[target]);
        UpdateStatus();
        _ = RenderVisibleAsync(includePreviews: false);
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        var meta = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
                   || e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var cmd = MacShortcuts.TryMatch(meta, shift, alt, e.Key.ToString());
        if (cmd is { } c)
        {
            Dispatch(c);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.PageDown or Key.Right)
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

    private void Dispatch(MacShortcuts.Command command)
    {
        switch (command)
        {
            case MacShortcuts.Command.Open:
                _ = OpenWithDialogAsync();
                break;
            case MacShortcuts.Command.SaveAs:
                _ = SaveAsAsync();
                break;
            case MacShortcuts.Command.Print:
                _ = PrintAsync();
                break;
            case MacShortcuts.Command.Close:
                CloseDoc_Click(this, new RoutedEventArgs());
                break;
            case MacShortcuts.Command.ZoomIn:
                SetZoom(ZoomHelper.ZoomIn(_zoom));
                break;
            case MacShortcuts.Command.ZoomOut:
                SetZoom(ZoomHelper.ZoomOut(_zoom));
                break;
            case MacShortcuts.Command.ActualSize:
                SetZoom(ZoomHelper.DefaultZoom);
                break;
            case MacShortcuts.Command.FitWidth:
                FitWidth_Click(this, new RoutedEventArgs());
                break;
            case MacShortcuts.Command.FitPage:
                FitPage_Click(this, new RoutedEventArgs());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }

    private void Window_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasPdfDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        var path = TryGetDroppedPdf(e);
        if (path != null)
            OpenPath(path);
    }

    private static bool HasPdfDrop(DragEventArgs e) => TryGetDroppedPdf(e) != null;

    private static string? TryGetDroppedPdf(DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files))
            return null;
        if (e.Data.GetFiles() is not { } files)
            return null;
        var paths = files.Select(f => f.Path.LocalPath);
        return CommandLinePdfArgs.TryGetFirstExistingPdf(paths);
    }

    private void UpdateStatus()
    {
        if (_doc == null)
            return;
        PageStatusText.Text = $"{_currentPage + 1} / {_doc.PageCount}";
        var pct = (int)Math.Round(_zoom * 100);
        ZoomStatusText.Text = pct + "%";
        ZoomLabel.Content = pct + "%";
    }

    private void ShowError(string message)
    {
        ErrorBannerText.Text = message;
        ErrorBanner.IsVisible = true;
    }

    private void HideError() => ErrorBanner.IsVisible = false;

    private void DismissError_Click(object? sender, RoutedEventArgs e) => HideError();

    private sealed record RecentRow(string DisplayName, string Path, string RelativeTime);
}
