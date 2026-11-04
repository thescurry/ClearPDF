using ClearPDF.Helpers;
using ClearPDF.Imaging;

namespace ClearPDF.Services;

/// <summary>
/// Page-raster cache of <see cref="PdfPixelBuffer"/> instances. Thumbnails live
/// in a separate map so zoom/scroll never drop sidebar bitmaps.
/// </summary>
public sealed class PageBitmapCache
{
    private readonly PageRasterCache<PdfPixelBuffer> _inner = new(PageRenderPlanner.MaxCachedPageRasters);

    public int FocusPage
    {
        get => _inner.FocusPage;
        set => _inner.FocusPage = value;
    }

    public bool TryGet(int pageIndex, double zoom, out PdfPixelBuffer? bitmap) =>
        _inner.TryGet(pageIndex, PdfRenderScale.ZoomCacheKey(zoom), out bitmap);

    public void Set(int pageIndex, double zoom, PdfPixelBuffer bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        _inner.Set(pageIndex, PdfRenderScale.ZoomCacheKey(zoom), bitmap);
    }

    public bool TryGetThumb(int pageIndex, out PdfPixelBuffer? bitmap) =>
        _inner.TryGetThumb(pageIndex, out bitmap);

    public void SetThumb(int pageIndex, PdfPixelBuffer bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        _inner.SetThumb(pageIndex, bitmap);
    }

    public void Clear() => _inner.Clear();

    /// <summary>Clear main-page rasters only — leave sidebar thumbs intact.</summary>
    public void ClearPages() => _inner.ClearPages();

    public void Trim() => _inner.Trim();
}
