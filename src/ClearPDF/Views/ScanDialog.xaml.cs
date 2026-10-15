using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClearPDF.Scanning;
using ClearPDF.Scanning.Wia;
using ClearPDF.Services;
using Microsoft.Win32;

namespace ClearPDF.Views;

public partial class ScanDialog : Window
{
    private readonly IScannerProvider _provider;
    private readonly string? _openPdfPath;
    private readonly ScanSession _session = new();
    private readonly ObservableCollection<ThumbItem> _thumbs = new();
    private CancellationTokenSource? _scanCts;
    private bool _scanning;
    private Point _dragStart;
    private bool _dragPending;

    public string? SavedPath { get; private set; }

    public ScanDialog(IScannerProvider provider, string? openPdfPath)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _openPdfPath = openPdfPath;
        InitializeComponent();
        ThumbList.ItemsSource = _thumbs;
        Loaded += OnLoaded;
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

        var envy = MockScanner.HpEnvy7640();
        envy.Queue.Add(MockScanner.CreateSolidPage(1275, 1650, 150, ScanColorMode.Color, 90, 90, 90));
        var mfp = MockScanner.HpColorLaserJetM277dw();
        mfp.Queue.Add(MockScanner.CreateSolidPage(1275, 1650, 150, ScanColorMode.Color, 70, 70, 90));
        return new MockScannerProvider(envy, mfp);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppendButton.IsEnabled = !string.IsNullOrWhiteSpace(_openPdfPath) && File.Exists(_openPdfPath);
        ColorBox.ItemsSource = new[] { "Color", "Grayscale", "Black and white" };
        ColorBox.SelectedIndex = 0;
        DpiBox.ItemsSource = ScanDefaults.DpiOptions;
        DpiBox.SelectedItem = 300;
        ReloadDevices();
        RefreshThumbs();
    }

    private void ReloadDevices()
    {
        IReadOnlyList<ScanDeviceInfo> devices;
        try
        {
            devices = _provider.ListDevices();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            ScannerBox.ItemsSource = Array.Empty<ScanDeviceInfo>();
            return;
        }

        ScannerBox.ItemsSource = devices;
        if (devices.Count == 0)
        {
            StatusText.Text = "No scanners found.";
            ScanButton.IsEnabled = false;
            return;
        }

        ScannerBox.SelectedIndex = 0;
        StatusText.Text = "Choose a scanner and click Scan.";
    }

    private ScanDeviceInfo? SelectedDevice => ScannerBox.SelectedItem as ScanDeviceInfo;

    private void Settings_Changed(object sender, SelectionChangedEventArgs e)
    {
        var device = SelectedDevice;
        if (device == null)
            return;

        var sources = new List<string>();
        if (device.SupportsFlatbed)
            sources.Add("Flatbed");
        if (device.SupportsFeeder)
            sources.Add("Feeder");
        if (sources.Count == 0)
            sources.Add("Flatbed");

        var keep = SourceBox.SelectedItem as string;
        SourceBox.ItemsSource = sources;
        if (keep != null && sources.Contains(keep))
            SourceBox.SelectedItem = keep;
        else
            SourceBox.SelectedIndex = 0;

        var feeder = string.Equals(SourceBox.SelectedItem as string, "Feeder", StringComparison.Ordinal);
        DuplexBox.Visibility = device.SupportsDuplex && feeder ? Visibility.Visible : Visibility.Collapsed;
        if (!device.SupportsDuplex || !feeder)
            DuplexBox.IsChecked = false;
    }

    private ScanSettings CurrentSettings()
    {
        var source = string.Equals(SourceBox.SelectedItem as string, "Feeder", StringComparison.Ordinal)
            ? ScanSource.Feeder
            : ScanSource.Flatbed;
        var color = ColorBox.SelectedIndex switch
        {
            1 => ScanColorMode.Grayscale,
            2 => ScanColorMode.BlackAndWhite,
            _ => ScanColorMode.Color
        };
        var dpi = DpiBox.SelectedItem is int d ? d : 300;
        return new ScanSettings
        {
            Source = source,
            ColorMode = color,
            Dpi = dpi,
            Duplex = DuplexBox.IsChecked == true
        };
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_scanning)
            return;
        var device = SelectedDevice;
        if (device == null)
        {
            StatusText.Text = "No scanners found.";
            return;
        }

        IScanner scanner;
        try
        {
            scanner = _provider.Open(device.Id);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            return;
        }

        _scanCts = new CancellationTokenSource();
        SetScanning(true);
        StatusText.Text = "Scanning…";
        try
        {
            var pages = await scanner.ScanAsync(
                CurrentSettings(),
                new Progress<ScanProgress>(p => StatusText.Text = p.Status ?? $"Scanned {p.PagesCompleted}"),
                _scanCts.Token);
            _session.AddRange(pages);
            RefreshThumbs();
            if (_thumbs.Count > 0)
                ThumbList.SelectedIndex = _thumbs.Count - 1;
            StatusText.Text = _session.Count == 1 ? "1 page scanned." : $"{_session.Count} pages scanned.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Scan cancelled.";
        }
        catch (ScannerException ex)
        {
            StatusText.Text = ex.Message;
            AppLog.Warn("Scan: " + ex.Message);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            AppLog.Error("Scan failed", ex);
        }
        finally
        {
            SetScanning(false);
            _scanCts.Dispose();
            _scanCts = null;
        }
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

    private void SetScanning(bool scanning)
    {
        _scanning = scanning;
        ScanButton.IsEnabled = !scanning && SelectedDevice != null;
        ScanMoreButton.IsEnabled = !scanning && _session.Count > 0;
        CancelScanButton.IsEnabled = scanning;
        SaveNewButton.IsEnabled = !scanning && _session.Count > 0;
        AppendButton.IsEnabled = !scanning && _session.Count > 0 &&
                                 !string.IsNullOrWhiteSpace(_openPdfPath) && File.Exists(_openPdfPath);
        ScannerBox.IsEnabled = !scanning;
        SourceBox.IsEnabled = !scanning;
        ColorBox.IsEnabled = !scanning;
        DpiBox.IsEnabled = !scanning;
        DuplexBox.IsEnabled = !scanning;
    }

    private void RefreshThumbs()
    {
        var keep = ThumbList.SelectedIndex;
        _thumbs.Clear();
        for (var i = 0; i < _session.Count; i++)
            _thumbs.Add(new ThumbItem(_session.Pages[i], i));
        if (_thumbs.Count == 0)
            ThumbList.SelectedIndex = -1;
        else
            ThumbList.SelectedIndex = Math.Clamp(keep, 0, _thumbs.Count - 1);
        ScanMoreButton.IsEnabled = !_scanning && _session.Count > 0;
        SaveNewButton.IsEnabled = !_scanning && _session.Count > 0;
        AppendButton.IsEnabled = !_scanning && _session.Count > 0 &&
                                 !string.IsNullOrWhiteSpace(_openPdfPath) && File.Exists(_openPdfPath);
    }

    private int SelectedIndex => ThumbList.SelectedIndex;

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var i = SelectedIndex;
        if (i <= 0)
            return;
        _session.MoveUp(i);
        RefreshThumbs();
        ThumbList.SelectedIndex = i - 1;
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var i = SelectedIndex;
        if (i < 0 || i >= _session.Count - 1)
            return;
        _session.MoveDown(i);
        RefreshThumbs();
        ThumbList.SelectedIndex = i + 1;
    }

    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        var i = SelectedIndex;
        if (i < 0)
            return;
        _session.Rotate(i, 90);
        RefreshThumbs();
        ThumbList.SelectedIndex = i;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var i = SelectedIndex;
        if (i < 0)
            return;
        _session.Delete(i);
        RefreshThumbs();
    }

    private void SaveNew_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Count == 0)
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
            ScannedPdfBuilder.SaveNew(_session.Pages, dlg.FileName);
            SavedPath = dlg.FileName;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Save failed: " + ex.Message;
            AppLog.Error("Scan save failed", ex);
        }
    }

    private void Append_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Count == 0 || string.IsNullOrWhiteSpace(_openPdfPath))
            return;
        try
        {
            ScannedPdfBuilder.Append(_session.Pages, _openPdfPath);
            SavedPath = _openPdfPath;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Append failed: " + ex.Message;
            AppLog.Error("Scan append failed", ex);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        DialogResult = false;
        Close();
    }

    private void ThumbList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Chrome only — session ops read SelectedIndex.
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

        var from = SelectedIndex;
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
        _session.Move(from, to);
        RefreshThumbs();
        ThumbList.SelectedIndex = to;
    }

    internal sealed class ThumbItem
    {
        public ThumbItem(ScannedPage page, int index)
        {
            Label = $"Page {index + 1}";
            Preview = LoadPreview(page);
        }

        public string Label { get; }
        public BitmapImage? Preview { get; }

        private static BitmapImage? LoadPreview(ScannedPage page)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(page.ImageBytes);
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
    }
}

internal static class ScanDialogVisual
{
    public static T? FindAncestor<T>(this DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
