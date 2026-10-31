using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClearPDF.Scanning;

namespace ClearPDF.Views;

public partial class ScanCropDialog : Window
{
    private readonly ScannedPage _page;
    private Point _start;
    private bool _dragging;

    public ScanCropRect Crop { get; private set; } = ScanCropRect.Full;

    public ScanCropDialog(ScannedPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 720;
            bmp.StreamSource = new MemoryStream(_page.ImageBytes);
            bmp.Rotation = _page.RotationDegrees switch
            {
                90 => Rotation.Rotate90,
                180 => Rotation.Rotate180,
                270 => Rotation.Rotate270,
                _ => Rotation.Rotate0
            };
            bmp.EndInit();
            bmp.Freeze();
            PageImage.Source = bmp;
        }
        catch (Exception)
        {
            PageImage.Source = null;
        }
    }

    private void Stage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _start = e.GetPosition(PageImage);
        Stage.CaptureMouse();
        CropRect.Visibility = Visibility.Visible;
        UpdateRect(_start, _start);
    }

    private void Stage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;
        UpdateRect(_start, e.GetPosition(PageImage));
    }

    private void Stage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;
        _dragging = false;
        Stage.ReleaseMouseCapture();
        UpdateRect(_start, e.GetPosition(PageImage));
        Crop = NormalizedFromRect();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Crop = ScanCropRect.Full;
        CropRect.Visibility = Visibility.Collapsed;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (CropRect.Visibility == Visibility.Visible)
            Crop = NormalizedFromRect();
        DialogResult = true;
        Close();
    }

    private void UpdateRect(Point a, Point b)
    {
        var image = PageImage.RenderSize;
        if (image.Width < 1 || image.Height < 1)
            return;

        var origin = PageImage.TransformToAncestor(Stage).Transform(new Point(0, 0));
        var x1 = Math.Clamp(Math.Min(a.X, b.X), 0, image.Width);
        var y1 = Math.Clamp(Math.Min(a.Y, b.Y), 0, image.Height);
        var x2 = Math.Clamp(Math.Max(a.X, b.X), 0, image.Width);
        var y2 = Math.Clamp(Math.Max(a.Y, b.Y), 0, image.Height);
        Canvas.SetLeft(CropRect, origin.X + x1);
        Canvas.SetTop(CropRect, origin.Y + y1);
        CropRect.Width = Math.Max(2, x2 - x1);
        CropRect.Height = Math.Max(2, y2 - y1);
    }

    private ScanCropRect NormalizedFromRect()
    {
        var image = PageImage.RenderSize;
        if (image.Width < 1 || image.Height < 1 || CropRect.Visibility != Visibility.Visible)
            return ScanCropRect.Full;

        var origin = PageImage.TransformToAncestor(Stage).Transform(new Point(0, 0));
        var left = (Canvas.GetLeft(CropRect) - origin.X) / image.Width;
        var top = (Canvas.GetTop(CropRect) - origin.Y) / image.Height;
        var right = left + CropRect.Width / image.Width;
        var bottom = top + CropRect.Height / image.Height;
        return new ScanCropRect(left, top, right, bottom).Clamp();
    }
}
