namespace ClearPDF.Scanning;

/// <summary>
/// Pixel helpers for the scan pipeline. Operates on packed RGB (R,G,B).
/// </summary>
internal static class ScanRaster
{
    public static bool TryGetRgb(ScannedPage page, out int width, out int height, out byte[] rgb)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.RasterRgb != null && page.RasterWidth > 0 && page.RasterHeight > 0 &&
            page.RasterRgb.Length >= page.RasterWidth * page.RasterHeight * 3)
        {
            width = page.RasterWidth;
            height = page.RasterHeight;
            rgb = page.RasterRgb;
            return true;
        }

        if (ScanBmp.TryDecodeRgb(page.ImageBytes, out width, out height, out rgb))
            return true;

        width = 0;
        height = 0;
        rgb = Array.Empty<byte>();
        return false;
    }

    public static byte Luma(byte r, byte g, byte b) =>
        (byte)((r * 77 + g * 150 + b * 29) >> 8);

    public static (byte[] Rgb, int Width, int Height) Crop(
        byte[] rgb, int width, int height, ScanCropRect rect)
    {
        rect = rect.Clamp();
        var x0 = Math.Clamp((int)Math.Floor(rect.Left * width), 0, width - 1);
        var y0 = Math.Clamp((int)Math.Floor(rect.Top * height), 0, height - 1);
        var x1 = Math.Clamp((int)Math.Ceiling(rect.Right * width), x0 + 1, width);
        var y1 = Math.Clamp((int)Math.Ceiling(rect.Bottom * height), y0 + 1, height);
        var w = x1 - x0;
        var h = y1 - y0;
        var dest = new byte[w * h * 3];
        for (var y = 0; y < h; y++)
        {
            var src = ((y0 + y) * width + x0) * 3;
            Buffer.BlockCopy(rgb, src, dest, y * w * 3, w * 3);
        }

        return (dest, w, h);
    }

    public static (byte[] Rgb, int Width, int Height) TrimMargins(
        byte[] rgb, int width, int height, byte inkBelow = 240)
    {
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        var i = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++, i += 3)
            {
                if (Luma(rgb[i], rgb[i + 1], rgb[i + 2]) >= inkBelow)
                    continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX)
            return (rgb, width, height);

        var pad = Math.Max(4, Math.Min(width, height) / 50);
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(width - 1, maxX + pad);
        maxY = Math.Min(height - 1, maxY + pad);
        if (minX == 0 && minY == 0 && maxX == width - 1 && maxY == height - 1)
            return (rgb, width, height);

        return Crop(rgb, width, height, new ScanCropRect(
            minX / (double)width,
            minY / (double)height,
            (maxX + 1) / (double)width,
            (maxY + 1) / (double)height));
    }

    public static byte[] ToGray(byte[] rgb, int width, int height)
    {
        var gray = new byte[width * height];
        for (int i = 0, p = 0; i < gray.Length; i++, p += 3)
            gray[i] = Luma(rgb[p], rgb[p + 1], rgb[p + 2]);
        return gray;
    }

    public static (byte[] Gray, int Width, int Height) DownsampleGray(
        byte[] rgb, int width, int height, int targetWidth)
    {
        if (targetWidth <= 0 || width <= targetWidth)
            return (ToGray(rgb, width, height), width, height);

        var tw = targetWidth;
        var th = Math.Max(1, height * tw / width);
        var gray = new byte[tw * th];
        for (var y = 0; y < th; y++)
        {
            var sy = y * height / th;
            for (var x = 0; x < tw; x++)
            {
                var sx = x * width / tw;
                var p = (sy * width + sx) * 3;
                gray[y * tw + x] = Luma(rgb[p], rgb[p + 1], rgb[p + 2]);
            }
        }

        return (gray, tw, th);
    }

    /// <summary>Rotate around center. Canvas grows to fit. Fill is white.</summary>
    public static (byte[] Rgb, int Width, int Height) Rotate(
        byte[] rgb, int width, int height, double degrees)
    {
        if (Math.Abs(degrees) < 0.05)
            return (rgb, width, height);

        var rad = degrees * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var nw = Math.Max(1, (int)Math.Ceiling(Math.Abs(width * cos) + Math.Abs(height * sin)));
        var nh = Math.Max(1, (int)Math.Ceiling(Math.Abs(width * sin) + Math.Abs(height * cos)));
        var dest = new byte[nw * nh * 3];
        for (var i = 0; i < dest.Length; i++)
            dest[i] = 255;

        var cx = (width - 1) / 2.0;
        var cy = (height - 1) / 2.0;
        var ncx = (nw - 1) / 2.0;
        var ncy = (nh - 1) / 2.0;
        var invCos = Math.Cos(-rad);
        var invSin = Math.Sin(-rad);

        for (var y = 0; y < nh; y++)
        {
            for (var x = 0; x < nw; x++)
            {
                var dx = x - ncx;
                var dy = y - ncy;
                var sx = dx * invCos - dy * invSin + cx;
                var sy = dx * invSin + dy * invCos + cy;
                if (sx < 0 || sy < 0 || sx >= width - 1 || sy >= height - 1)
                    continue;

                var x0 = (int)sx;
                var y0 = (int)sy;
                var fx = sx - x0;
                var fy = sy - y0;
                var d = (y * nw + x) * 3;
                for (var c = 0; c < 3; c++)
                {
                    var p00 = rgb[(y0 * width + x0) * 3 + c];
                    var p10 = rgb[(y0 * width + x0 + 1) * 3 + c];
                    var p01 = rgb[((y0 + 1) * width + x0) * 3 + c];
                    var p11 = rgb[((y0 + 1) * width + x0 + 1) * 3 + c];
                    dest[d + c] = (byte)Math.Clamp(
                        (p00 * (1 - fx) + p10 * fx) * (1 - fy) +
                        (p01 * (1 - fx) + p11 * fx) * fy,
                        0, 255);
                }
            }
        }

        return (dest, nw, nh);
    }
}
