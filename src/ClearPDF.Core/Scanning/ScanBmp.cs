namespace ClearPDF.Scanning;

/// <summary>Decode uncompressed 24-bit / 32-bit / 8-bit Windows BMP to RGB.</summary>
internal static class ScanBmp
{
    public static bool TryDecodeRgb(byte[] data, out int width, out int height, out byte[] rgb)
    {
        width = 0;
        height = 0;
        rgb = Array.Empty<byte>();
        if (data == null || data.Length < 54 || data[0] != (byte)'B' || data[1] != (byte)'M')
            return false;

        var dataOffset = BitConverter.ToInt32(data, 10);
        var dib = BitConverter.ToInt32(data, 14);
        if (dib < 40)
            return false;

        width = BitConverter.ToInt32(data, 18);
        height = BitConverter.ToInt32(data, 22);
        var planes = BitConverter.ToInt16(data, 26);
        var bpp = BitConverter.ToInt16(data, 28);
        var compression = BitConverter.ToInt32(data, 30);
        if (planes != 1 || compression != 0 || width <= 0)
            return false;

        var topDown = height < 0;
        height = Math.Abs(height);
        if (height <= 0 || dataOffset < 0 || dataOffset >= data.Length)
            return false;

        try
        {
            rgb = bpp switch
            {
                24 => ReadRgb(data, dataOffset, width, height, topDown, 3),
                32 => ReadRgb(data, dataOffset, width, height, topDown, 4),
                8 => ReadPaletted(data, dataOffset, width, height, topDown, dib),
                _ => Array.Empty<byte>()
            };
        }
        catch (Exception)
        {
            rgb = Array.Empty<byte>();
            return false;
        }

        return rgb.Length == width * height * 3;
    }

    private static byte[] ReadRgb(byte[] data, int offset, int width, int height, bool topDown, int bytesPerPixel)
    {
        var stride = ((width * bytesPerPixel + 3) / 4) * 4;
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            var srcY = topDown ? y : height - 1 - y;
            var src = offset + srcY * stride;
            var dst = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var i = src + x * bytesPerPixel;
                rgb[dst] = data[i + 2];
                rgb[dst + 1] = data[i + 1];
                rgb[dst + 2] = data[i];
                dst += 3;
            }
        }

        return rgb;
    }

    private static byte[] ReadPaletted(byte[] data, int pixelOffset, int width, int height, bool topDown, int dib)
    {
        var paletteOffset = 14 + dib;
        var stride = ((width + 3) / 4) * 4;
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            var srcY = topDown ? y : height - 1 - y;
            var src = pixelOffset + srcY * stride;
            var dst = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var idx = data[src + x];
                var p = paletteOffset + idx * 4;
                rgb[dst] = data[p + 2];
                rgb[dst + 1] = data[p + 1];
                rgb[dst + 2] = data[p];
                dst += 3;
            }
        }

        return rgb;
    }
}
