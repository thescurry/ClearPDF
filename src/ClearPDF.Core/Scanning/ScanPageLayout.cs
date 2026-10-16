namespace ClearPDF.Scanning;

public static class ScanPageLayout
{
    public static (double Width, double Height) PageSizePoints(ScannedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var dpi = Math.Max(page.Dpi, 1);
        var (w, h) = page.OrientedPixels;
        w = Math.Max(w, 1);
        h = Math.Max(h, 1);
        return (w * 72.0 / dpi, h * 72.0 / dpi);
    }

    public static int NormalizeDegrees(int degrees)
    {
        var d = degrees % 360;
        if (d < 0)
            d += 360;
        // Snap to 90° so a 45° slip from UI still lands on a PDF-sane rotate.
        return (int)(Math.Round(d / 90.0) * 90) % 360;
    }

    public static bool IsQuarterTurn(int degrees)
    {
        var d = NormalizeDegrees(degrees);
        return d == 90 || d == 270;
    }
}
