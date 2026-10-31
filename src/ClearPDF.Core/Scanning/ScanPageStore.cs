namespace ClearPDF.Scanning;

/// <summary>
/// Keeps the full raster on disk so a long batch holds JPEG + thumbnail
/// in RAM only. Decode on demand via <see cref="TryRead"/>.
/// </summary>
public sealed class ScanPageStore : IDisposable
{
    private readonly string _dir;
    private bool _disposed;

    public ScanPageStore(string? directory = null)
    {
        _dir = directory ?? Path.Combine(
            Path.GetTempPath(), "clearpdf-scan", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public string DirectoryPath => _dir;

    public ScannedPage Persist(ScannedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!ScanRaster.TryGetRgb(page, out var width, out var height, out var rgb))
        {
            return page with
            {
                ThumbnailBytes = page.ThumbnailBytes ?? page.ImageBytes,
                RasterRgb = null
            };
        }

        var current = Write(page.Id.ToString("N"), rgb, width, height);
        var source = page.SourceRasterPath ?? Write(page.Id.ToString("N") + "-src", rgb, width, height);
        var thumb = MakeThumbnail(rgb, width, height, page.ColorMode);
        return page with
        {
            RasterPath = current,
            SourceRasterPath = source,
            SourceImageBytes = page.SourceImageBytes ?? page.ImageBytes,
            SourcePixelWidth = page.SourcePixelWidth > 0 ? page.SourcePixelWidth : page.PixelWidth,
            SourcePixelHeight = page.SourcePixelHeight > 0 ? page.SourcePixelHeight : page.PixelHeight,
            ThumbnailBytes = thumb,
            RasterRgb = null,
            RasterWidth = width,
            RasterHeight = height
        };
    }

    public string Write(string name, byte[] rgb, int width, int height)
    {
        var path = Path.Combine(_dir, name + ".srgb");
        using var fs = File.Create(path);
        fs.Write("SRGB"u8);
        fs.Write(BitConverter.GetBytes(width));
        fs.Write(BitConverter.GetBytes(height));
        fs.Write(rgb, 0, width * height * 3);
        return path;
    }

    public static bool TryRead(string? path, out int width, out int height, out byte[] rgb)
    {
        width = 0;
        height = 0;
        rgb = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        var data = File.ReadAllBytes(path);
        if (data.Length < 12 || data[0] != (byte)'S' || data[1] != (byte)'R' ||
            data[2] != (byte)'G' || data[3] != (byte)'B')
            return false;

        width = BitConverter.ToInt32(data, 4);
        height = BitConverter.ToInt32(data, 8);
        var need = width * height * 3;
        if (width <= 0 || height <= 0 || data.Length < 12 + need)
            return false;

        rgb = new byte[need];
        Buffer.BlockCopy(data, 12, rgb, 0, need);
        return true;
    }

    public static byte[] MakeThumbnail(byte[] rgb, int width, int height, ScanColorMode colorMode)
    {
        var scaled = ScanRaster.DownsampleRgb(rgb, width, height, 160);
        return ScanJpegEncoder.EncodeRgb(scaled.Rgb, scaled.Width, scaled.Height, colorMode);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // leftover temp is harmless
        }
    }
}
