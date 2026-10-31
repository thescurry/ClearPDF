using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ClearPDF.Imaging;

namespace ClearPDF.Mac.Services;

/// <summary>Avalonia display adapter for <see cref="PdfPixelBuffer"/>.</summary>
internal static class PdfPixelBufferAvalonia
{
    private static readonly ConditionalWeakTable<PdfPixelBuffer, WriteableBitmap> Converted = new();

    public static WriteableBitmap ToBitmap(PdfPixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (Converted.TryGetValue(buffer, out var cached))
            return cached;
        var bmp = new WriteableBitmap(
            new PixelSize(buffer.Width, buffer.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using var fb = bmp.Lock();
        var destStride = fb.RowBytes;
        if (destStride == buffer.Stride)
        {
            Marshal.Copy(buffer.Bgra32, 0, fb.Address, buffer.Stride * buffer.Height);
        }
        else
        {
            for (var y = 0; y < buffer.Height; y++)
            {
                Marshal.Copy(
                    buffer.Bgra32,
                    y * buffer.Stride,
                    IntPtr.Add(fb.Address, y * destStride),
                    buffer.Width * 4);
            }
        }

        Converted.Add(buffer, bmp);
        return bmp;
    }
}
