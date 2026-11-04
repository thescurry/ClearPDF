using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClearPDF.Imaging;

namespace ClearPDF.Services;

/// <summary>WPF display adapter for <see cref="PdfPixelBuffer"/>.</summary>
internal static class PdfPixelBufferWpf
{
    private static readonly ConditionalWeakTable<PdfPixelBuffer, BitmapSource> Converted = new();

    public static BitmapSource ToFrozenBitmap(PdfPixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (Converted.TryGetValue(buffer, out var cached))
            return cached;

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
        Converted.Add(buffer, bmp);
        return bmp;
    }
}
