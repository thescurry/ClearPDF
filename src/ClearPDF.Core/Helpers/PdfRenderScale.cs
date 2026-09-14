namespace ClearPDF.Helpers;

/// <summary>
/// Quantize zoom and cap raster DPI so image-heavy pages do not explode memory.
/// Pure math — UI-free for unit tests.
/// </summary>
public static class PdfRenderScale
{
    /// <summary>Pixels-per-point multiplier at zoom 1.0 (matches open baseline ≈ 144 DPI).</summary>
    public const double BaselinePixelsPerPoint = PdfPageSize.DocnetRenderScale;

    /// <summary>Longest edge cap for a single page raster (pixels).</summary>
    public const int MaxEdgePixels = 4096;

    /// <summary>Total pixel budget per page (width × height).</summary>
    public const long MaxPixels = 8_000_000;

    /// <summary>Zoom quantization step for cache keys (avoids thrash on tiny deltas).</summary>
    public const double QuantizeStep = 0.05;

    /// <summary>Low-res preview scale used for off-screen / scrolling placeholders.</summary>
    public const double PreviewZoom = 0.35;

    /// <summary>
    /// Sidebar thumbnail zoom (~120px-wide letter at baseline 2.0 ppp).
    /// Intentionally below <see cref="ZoomHelper.MinZoom"/> so thumbs stay tiny
    /// and never share cache keys with main-view rasters.
    /// </summary>
    public const double ThumbZoom = 0.2;

    /// <summary>Floor for render zoom (thumbs may go below UI MinZoom).</summary>
    public const double MinRenderZoom = 0.05;

    /// <summary>Round zoom to <see cref="QuantizeStep"/> within ZoomHelper min/max.</summary>
    public static double QuantizeZoom(double zoom)
    {
        var clamped = ZoomHelper.Clamp(zoom);
        var q = Math.Round(clamped / QuantizeStep) * QuantizeStep;
        // Avoid -0 / floating noise at boundaries.
        if (q < ZoomHelper.MinZoom) q = ZoomHelper.MinZoom;
        if (q > ZoomHelper.MaxZoom) q = ZoomHelper.MaxZoom;
        return Math.Round(q, 4);
    }

    /// <summary>
    /// Docnet <c>PageDimensions(scalingFactor)</c> pixels-per-point for a page at the
    /// given zoom, capped by max edge and max total pixels.
    /// Allows values below UI <see cref="ZoomHelper.MinZoom"/> so thumbnail rasters
    /// stay low-res (~72–96 DPI equivalent at <see cref="ThumbZoom"/>).
    /// </summary>
    public static double CapPixelsPerPoint(
        double zoom,
        double pageWidthPoints,
        double pageHeightPoints,
        double baseline = BaselinePixelsPerPoint,
        int maxEdgePixels = MaxEdgePixels,
        long maxPixels = MaxPixels)
    {
        if (pageWidthPoints <= 0 || pageHeightPoints <= 0)
            return baseline;

        var desired = Math.Clamp(zoom, MinRenderZoom, ZoomHelper.MaxZoom) * baseline;
        if (desired <= 0)
            return baseline;

        var w = pageWidthPoints * desired;
        var h = pageHeightPoints * desired;
        var factor = 1.0;

        var longest = Math.Max(w, h);
        if (longest > maxEdgePixels)
            factor = Math.Min(factor, maxEdgePixels / longest);

        var area = w * h;
        if (area > maxPixels)
            factor = Math.Min(factor, Math.Sqrt(maxPixels / area));

        var capped = desired * factor;
        // Docnet rejects non-positive / absurdly tiny scales.
        return Math.Max(0.05, capped);
    }

    /// <summary>Integer cache key for a quantized zoom (avoids double dict keys).</summary>
    public static int ZoomCacheKey(double quantizedZoom) =>
        (int)Math.Round(QuantizeZoom(quantizedZoom) / QuantizeStep);

    /// <summary>
    /// Cache key for thumbnail zoom — does not go through UI MinZoom clamp,
    /// so thumbs never collide with main-page keys at zoom 0.25.
    /// </summary>
    public static int ThumbCacheKey() =>
        (int)Math.Round(Math.Clamp(ThumbZoom, MinRenderZoom, ZoomHelper.MaxZoom) / QuantizeStep);
}
