using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClearPDF.Scanning;
using ClearPDF.Scanning.Wia;
using ClearPDF.Services;
using Microsoft.Win32;

namespace ClearPDF.Views;

public partial class ScanDialog : Window
{
    private readonly ScanViewModel _vm;
    private readonly ObservableCollection<ThumbItem> _thumbs = new();
    private Point _dragStart;
    private bool _dragPending;
    private bool _syncing;

    public string? SavedPath => _vm.SavedPath;

    public ScanDialog(IScannerProvider provider, string? openPdfPath)
        : this(new ScanViewModel(provider, openPdfPath))
    {
    }

    internal ScanDialog(ScanViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        ThumbList.ItemsSource = _thumbs;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>WIA 2.0 on Windows; mock devices when WIA is unavailable (tests / no hardware).</summary>
    public static IScannerProvider CreateProvider()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return new WiaScannerProvider();
            }
            catch (Exception ex)
            {
                AppLog.Warn("WIA provider failed; using mock scanners: " + ex.Message);
            }
        }

        return CreateMockProvider();
    }

    internal static IScannerProvider CreateMockProvider()
    {
        var envy = MockScanner.HpEnvy7640();
        envy.Queue.Add(MockScanner.CreateSolidPage(1275, 1650, 150, ScanColorMode.Color, 90, 90, 90));
        var mfp = MockScanner.HpColorLaserJetM277dw();
        mfp.Queue.Add(MockScanner.CreateSolidPage(1275, 1650, 150, ScanColorMode.Color, 70, 70, 90));
        mfp.Queue.Add(MockScanner.CreateSolidPage(1275, 1650, 150, ScanColorMode.Color, 80, 70, 70));
        return new MockScannerProvider(envy, mfp);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ColorBox.ItemsSource = new[] { "Color", "Grayscale", "Black and white" };
        DpiBox.ItemsSource = ScanDefaults.DpiOptions;
        _vm.Pages.CollectionChanged += Pages_CollectionChanged;
        _vm.PropertyChanged += ViewModel_PropertyChanged;
        BindDevices();
        BindSettings();
        BindInsertChoices();
        RefreshThumbs();
        SyncChrome();
        ScanButton.Focus();
        _ = ReloadDevicesSafe();
    }

    private async Task ReloadDevicesSafe()
    {
        try
        {
            await _vm.ReloadDevicesAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Scanner list failed", ex);
            StatusText.Text = ex.Message;
        }

        BindDevices();
        BindSettings();
        SyncChrome();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _vm.Pages.CollectionChanged -= Pages_CollectionChanged;
        _vm.PropertyChanged -= ViewModel_PropertyChanged;
        _vm.CancelScan();
        _vm.PersistPreferences();
        _vm.Dispose();
    }

    private void Pages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.Invoke(RefreshThumbs);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ScanViewModel.Status)
            or nameof(ScanViewModel.IsScanning)
            or nameof(ScanViewModel.CanScan)
            or nameof(ScanViewModel.CanSave)
            or nameof(ScanViewModel.CanInsert)
            or nameof(ScanViewModel.CanScanMore)
            or nameof(ScanViewModel.CanDuplex)
            or nameof(ScanViewModel.PageCount)
            or nameof(ScanViewModel.SelectedIndex))
        {
            Dispatcher.Invoke(SyncChrome);
        }
    }

    private void BindDevices()
    {
        _syncing = true;
        ScannerBox.ItemsSource = _vm.Devices;
        ScannerBox.SelectedItem = _vm.SelectedDevice;
        _syncing = false;
    }

    private void BindSettings()
    {
        _syncing = true;
        SourceBox.ItemsSource = _vm.AvailableSources.Select(SourceLabel).ToList();
        SourceBox.SelectedItem = SourceLabel(_vm.Source);
        ColorBox.SelectedIndex = _vm.ColorMode switch
        {
            ScanColorMode.Grayscale => 1,
            ScanColorMode.BlackAndWhite => 2,
            _ => 0
        };
        DpiBox.SelectedItem = ScanDefaults.DpiOptions.Contains(_vm.Dpi) ? _vm.Dpi : 300;
        DuplexBox.IsChecked = _vm.Duplex;
        BlankBox.IsChecked = _vm.DropBlankPages;
        DeskewBox.IsChecked = _vm.AutoDeskew;
        TrimBox.IsChecked = _vm.AutoTrim;
        OcrBox.ToolTip = _vm.OcrUnavailableReason;
        _syncing = false;
    }

    private void BindInsertChoices()
    {
        if (!_vm.HasOpenPdf)
        {
            InsertRow.Visibility = Visibility.Collapsed;
            InsertButton.Visibility = Visibility.Collapsed;
            return;
        }

        var items = new List<InsertChoice>
        {
            new(ScanInsertMode.Start, 0, "At the start")
        };
        for (var i = 1; i <= _vm.OpenPdfPageCount; i++)
            items.Add(new InsertChoice(ScanInsertMode.AfterPage, i, $"After page {i}"));
        items.Add(new InsertChoice(ScanInsertMode.End, _vm.OpenPdfPageCount, "At the end"));

        _syncing = true;
        InsertBox.ItemsSource = items;
        InsertBox.DisplayMemberPath = nameof(InsertChoice.Label);
        InsertBox.SelectedItem = items[^1];
        _vm.InsertMode = ScanInsertMode.End;
        _syncing = false;
    }

    private void SyncChrome()
    {
        StatusText.Text = _vm.Status;
        ScanButton.IsEnabled = _vm.CanScan;
        ScanMoreButton.IsEnabled = _vm.CanScanMore;
        ScanMoreButton.Visibility = _vm.PageCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        CancelScanButton.IsEnabled = _vm.IsScanning;
        SaveNewButton.IsEnabled = _vm.CanSave;
        InsertButton.IsEnabled = _vm.CanInsert;
        ScanProgressBar.Visibility = _vm.IsScanning ? Visibility.Visible : Visibility.Collapsed;
        ScannerBox.IsEnabled = !_vm.IsScanning;
        SourceBox.IsEnabled = !_vm.IsScanning;
        ColorBox.IsEnabled = !_vm.IsScanning;
        DpiBox.IsEnabled = !_vm.IsScanning;
        DuplexBox.Visibility = _vm.CanDuplex || _vm.Duplex ? Visibility.Visible : Visibility.Collapsed;
        DuplexBox.IsEnabled = !_vm.IsScanning && _vm.CanDuplex;
        PageCountText.Text = FormatPageCount();

        if (ThumbList.SelectedIndex != _vm.SelectedIndex &&
            _vm.SelectedIndex >= -1 && _vm.SelectedIndex < _thumbs.Count)
            ThumbList.SelectedIndex = _vm.SelectedIndex;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.ScanAsync(addMore: _vm.VisibleCount > 0 || _vm.PageCount > 0);
            if (!IsLoaded)
                return;
            RefreshThumbs();
            SyncChrome();
        }
        catch (Exception ex)
        {
            AppLog.Error("Scan failed", ex);
            if (IsLoaded)
                StatusText.Text = ex.Message;
        }
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _vm.CancelScan();

    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.RescanSelectedAsync();
            if (IsLoaded)
            {
                RefreshThumbs();
                SyncChrome();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Rescan failed", ex);
            if (IsLoaded)
                StatusText.Text = ex.Message;
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        _vm.MoveSelected(-1);
        RefreshThumbs();
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        _vm.MoveSelected(1);
        RefreshThumbs();
    }

    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        _vm.RotateSelected(90);
        RefreshThumbs();
    }

    private void Trim_Click(object sender, RoutedEventArgs e)
    {
        _vm.TrimSelected();
        RefreshThumbs();
    }

    private void Crop_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.CanEditSelected)
            return;
        var page = _vm.Pages[_vm.SelectedIndex];
        var dlg = new ScanCropDialog(page) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _vm.CropSelected(dlg.Crop);
            RefreshThumbs();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        _vm.DeleteSelected();
        RefreshThumbs();
        SyncChrome();
    }

    private async void SaveNew_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.CanSave)
            return;
        var dlg = new SaveFileDialog
        {
            Title = "Save as new PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = "scanned.pdf"
        };
        if (dlg.ShowDialog(this) != true)
            return;
        try
        {
            await _vm.SaveNewAsync(dlg.FileName);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Save failed: " + ex.Message;
            AppLog.Error("Scan save failed", ex);
        }
    }

    private async void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.CanInsert)
            return;
        try
        {
            await _vm.InsertIntoOpenPdfAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not add pages: " + ex.Message;
            AppLog.Error("Scan insert failed", ex);
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        _vm.RestoreSelected();
        RefreshThumbs();
        SyncChrome();
    }

    private void Revert_Click(object sender, RoutedEventArgs e)
    {
        _vm.RevertSelected();
        RefreshThumbs();
        SyncChrome();
    }

    private void ScannerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        _vm.SelectedDevice = ScannerBox.SelectedItem as ScanDeviceInfo;
        BindSettings();
        SyncChrome();
    }

    private void SourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        _vm.Source = string.Equals(SourceBox.SelectedItem as string, "Feeder", StringComparison.Ordinal)
            ? ScanSource.Feeder
            : ScanSource.Flatbed;
        BindSettings();
        SyncChrome();
    }

    private void ColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        _vm.ColorMode = ColorBox.SelectedIndex switch
        {
            1 => ScanColorMode.Grayscale,
            2 => ScanColorMode.BlackAndWhite,
            _ => ScanColorMode.Color
        };
    }

    private void DpiBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        if (DpiBox.SelectedItem is int dpi)
            _vm.Dpi = dpi;
    }

    private void DuplexBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        _vm.Duplex = DuplexBox.IsChecked == true;
    }

    private void BlankBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        _vm.DropBlankPages = BlankBox.IsChecked == true;
    }

    private void DeskewBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        _vm.AutoDeskew = DeskewBox.IsChecked == true;
    }

    private void TrimBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        _vm.AutoTrim = TrimBox.IsChecked == true;
    }

    private void InsertBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || InsertBox.SelectedItem is not InsertChoice choice)
            return;
        _vm.InsertMode = choice.Mode;
        _vm.InsertAfterPage = choice.AfterPage;
    }

    private void ThumbList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        _vm.SelectedIndex = ThumbList.SelectedIndex;
    }

    private void RefreshThumbs()
    {
        var keep = _vm.SelectedIndex;
        _syncing = true;
        _thumbs.Clear();
        for (var i = 0; i < _vm.Pages.Count; i++)
            _thumbs.Add(new ThumbItem(_vm.Pages[i], i));
        ThumbList.SelectedIndex = _thumbs.Count == 0
            ? -1
            : Math.Clamp(keep, 0, _thumbs.Count - 1);
        _syncing = false;
        PageCountText.Text = FormatPageCount();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm.IsScanning && e.Key == Key.Escape)
        {
            _vm.CancelScan();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            SaveNew_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I)
        {
            Insert_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Up)
        {
            _vm.MoveSelected(-1);
            RefreshThumbs();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Down)
        {
            _vm.MoveSelected(1);
            RefreshThumbs();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Space or Key.S)
        {
            if (Keyboard.FocusedElement is ComboBox or ComboBoxItem)
                return;
            if (_vm.CanScan)
            {
                Scan_Click(sender, e);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.R)
        {
            _vm.RotateSelected(90);
            RefreshThumbs();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.T)
        {
            _vm.TrimSelected();
            RefreshThumbs();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            _vm.DeleteSelected();
            RefreshThumbs();
            SyncChrome();
            e.Handled = true;
        }
    }

    private void ThumbList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragPending = true;
    }

    private void ThumbList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragPending || e.LeftButton != MouseButtonState.Pressed)
            return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var from = ThumbList.SelectedIndex;
        if (from < 0)
            return;
        _dragPending = false;
        DragDrop.DoDragDrop(ThumbList, from, DragDropEffects.Move);
    }

    private void ThumbList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void ThumbList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(int)))
            return;
        var from = (int)e.Data.GetData(typeof(int));
        var target = (e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>();
        if (target == null)
            return;
        var to = ThumbList.ItemContainerGenerator.IndexFromContainer(target);
        if (from < 0 || to < 0 || from == to)
            return;
        _vm.Move(from, to);
        RefreshThumbs();
    }

    private string FormatPageCount()
    {
        if (_vm.PageCount == 0)
            return "No pages yet";
        var visible = _vm.VisibleCount == 1 ? "1 page" : $"{_vm.VisibleCount} pages";
        return _vm.BlankDropped == 0
            ? visible
            : $"{visible} ({_vm.BlankDropped} hidden as blank)";
    }

    private static string SourceLabel(ScanSource source) =>
        source == ScanSource.Feeder ? "Feeder" : "Flatbed";

    internal sealed class ThumbItem
    {
        public ThumbItem(ScannedPage page, int index)
        {
            Label = page.HiddenAsBlank ? "Hidden as blank" : $"Page {index + 1}";
            Preview = LoadPreview(page);
        }

        public string Label { get; }
        public ImageSource? Preview { get; }

        private static ImageSource? LoadPreview(ScannedPage page)
        {
            try
            {
                var bytes = page.ThumbnailBytes ?? page.ImageBytes;
                if (page.RasterRgb != null && page.RasterWidth > 0 && page.RasterHeight > 0)
                    return FromRgb(page.RasterRgb, page.RasterWidth, page.RasterHeight, page.RotationDegrees);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 180;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.Rotation = page.RotationDegrees switch
                {
                    90 => Rotation.Rotate90,
                    180 => Rotation.Rotate180,
                    270 => Rotation.Rotate270,
                    _ => Rotation.Rotate0
                };
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ImageSource? FromRgb(byte[] rgb, int width, int height, int rotation)
        {
            var stride = width * 3;
            var pixels = new byte[stride * height];
            for (var i = 0; i < width * height; i++)
            {
                pixels[i * 3] = rgb[i * 3 + 2];
                pixels[i * 3 + 1] = rgb[i * 3 + 1];
                pixels[i * 3 + 2] = rgb[i * 3];
            }

            var bmp = BitmapSource.Create(
                width, height, 96, 96, PixelFormats.Bgr24, null, pixels, stride);
            var degrees = ScanPageLayout.NormalizeDegrees(rotation);
            if (degrees == 0)
            {
                bmp.Freeze();
                return bmp;
            }

            var rotated = new TransformedBitmap(bmp, new RotateTransform(degrees));
            rotated.Freeze();
            return rotated;
        }
    }

    private sealed record InsertChoice(ScanInsertMode Mode, int AfterPage, string Label);
}

internal static class ScanDialogVisual
{
    public static T? FindAncestor<T>(this DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
