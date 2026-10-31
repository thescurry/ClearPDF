namespace ClearPDF.Scanning;

/// <summary>
/// Drops nearly-empty sheets (white glass, leftover ADF card). Uses a
/// downsampled ink ratio so JPEG swatches and 300 DPI letter pages share
/// the same threshold.
/// </summary>
public static class ScanBlankDetector
{
    public const double DefaultInkThreshold = 0.008;

    public static bool IsBlank(ScannedPage page, double inkThreshold = DefaultInkThreshold)
    {
        if (!ScanRaster.TryGetRgb(page, out var w, out var h, out var rgb))
            return false;
        return IsBlank(rgb, w, h, inkThreshold);
    }

    public static bool IsBlank(byte[] rgb, int width, int height, double inkThreshold = DefaultInkThreshold)
    {
        if (rgb == null || width <= 0 || height <= 0)
            return true;
        if (rgb.Length < width * height * 3)
            return true;

        var (gray, w, h) = ScanRaster.DownsampleGray(rgb, width, height, 160);
        var ink = 0;
        for (var i = 0; i < gray.Length; i++)
        {
            if (gray[i] < 240)
                ink++;
        }

        return ink / (double)gray.Length < inkThreshold;
    }
}
