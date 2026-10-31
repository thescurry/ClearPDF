namespace ClearPDF.Scanning;

/// <summary>Width/height from JPEG SOF or BMP BITMAPINFOHEADER — no decoder.</summary>
internal static class ScanImageHeader
{
    public static bool TryRead(byte[] data, out int width, out int height, out ScanImageFormat format)
    {
        width = 0;
        height = 0;
        format = ScanImageFormat.Jpeg;
        if (data == null || data.Length < 4)
            return false;

        if (data[0] == 0xFF && data[1] == 0xD8 && TryJpeg(data, out width, out height))
        {
            format = ScanImageFormat.Jpeg;
            return true;
        }

        if (data[0] == (byte)'B' && data[1] == (byte)'M' && data.Length >= 26)
        {
            width = BitConverter.ToInt32(data, 18);
            height = Math.Abs(BitConverter.ToInt32(data, 22));
            format = ScanImageFormat.Bmp;
            return width > 0 && height > 0;
        }

        if (data.Length >= 24 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G')
        {
            width = ReadJpegInt32(data, 16);
            height = ReadJpegInt32(data, 20);
            format = ScanImageFormat.Png;
            return width > 0 && height > 0;
        }

        return false;
    }

    private static int ReadJpegInt32(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static bool TryJpeg(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;
        var i = 2;
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = data[i + 1];
            if (marker == 0xD9 || marker == 0xDA)
                return false;
            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                i += 2;
                continue;
            }

            if (i + 3 >= data.Length)
                return false;
            var len = (data[i + 2] << 8) | data[i + 3];
            if (len < 2 || i + 2 + len > data.Length)
                return false;

            // SOF0–SOF3, SOF5–SOF7, SOF9–SOF11, SOF13–SOF15 (skip DHT/JPG/DAC)
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                height = (data[i + 5] << 8) | data[i + 6];
                width = (data[i + 7] << 8) | data[i + 8];
                return width > 0 && height > 0;
            }

            i += 2 + len;
        }

        return false;
    }
}
