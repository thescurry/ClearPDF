using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ClearPDF.Pdfium;

namespace ClearPDF.Desktop.Services;

public static class PdfBitmapFactory
{
    public static Bitmap? FromRaster(PdfRaster? raster)
    {
        if (raster is not { } r || r.IsEmpty)
            return null;

        var bmp = new WriteableBitmap(
            new PixelSize(r.Width, r.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using (var fb = bmp.Lock())
        {
            var srcStride = r.Stride;
            var destStride = fb.RowBytes;
            if (srcStride == destStride)
            {
                Marshal.Copy(r.Bgra32, 0, fb.Address, r.Bgra32.Length);
            }
            else
            {
                for (var y = 0; y < r.Height; y++)
                {
                    Marshal.Copy(
                        r.Bgra32,
                        y * srcStride,
                        fb.Address + (y * destStride),
                        srcStride);
                }
            }
        }

        return bmp;
    }
}
