using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClearPDF.Imaging;

namespace ClearPDF.Services;

/// <summary>WPF display adapter for <see cref="PdfPixelBuffer"/>.</summary>
internal static class PdfPixelBufferWpf
{
    public static BitmapSource ToFrozenBitmap(PdfPixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var bmp = BitmapSource.Create(
            buffer.Width,
            buffer.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            buffer.Bgra32,
            buffer.Stride);
        bmp.Freeze();
        return bmp;
    }
}
