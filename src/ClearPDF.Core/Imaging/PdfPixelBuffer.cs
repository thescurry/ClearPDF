namespace ClearPDF.Imaging;

/// <summary>
/// Platform-neutral BGRA32 page raster. UI layers turn this into
/// WPF <c>BitmapSource</c> or Avalonia <c>WriteableBitmap</c>.
/// </summary>
public sealed class PdfPixelBuffer
{
    public byte[] Bgra32 { get; }
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0 || Bgra32.Length == 0;

    public PdfPixelBuffer(byte[] bgra32, int width, int height, int? stride = null)
    {
        ArgumentNullException.ThrowIfNull(bgra32);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        var actualStride = stride ?? checked(width * 4);
        if (actualStride < width * 4)
            throw new ArgumentOutOfRangeException(nameof(stride));
        if (bgra32.Length < checked(actualStride * height))
            throw new ArgumentException("Pixel buffer is shorter than stride × height.", nameof(bgra32));

        Bgra32 = bgra32;
        Width = width;
        Height = height;
        Stride = actualStride;
    }

    /// <summary>
    /// Gray-edged white placeholder used when PDFium natives are missing.
    /// </summary>
    public static PdfPixelBuffer CreatePlaceholder(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var i = row + x * 4;
                var edge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                if (edge)
                {
                    pixels[i] = 0xCC;
                    pixels[i + 1] = 0xCC;
                    pixels[i + 2] = 0xCC;
                    pixels[i + 3] = 0xFF;
                }
                else
                {
                    pixels[i] = 0xFF;
                    pixels[i + 1] = 0xFF;
                    pixels[i + 2] = 0xFF;
                    pixels[i + 3] = 0xFF;
                }
            }
        }

        return new PdfPixelBuffer(pixels, width, height, stride);
    }
}
