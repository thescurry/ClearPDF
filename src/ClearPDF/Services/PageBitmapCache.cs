using System.Windows.Media.Imaging;
using ClearPDF.Helpers;

namespace ClearPDF.Services;

/// <summary>
/// WPF wrapper around <see cref="PageRasterCache{T}"/>. Writes expect frozen
/// <see cref="BitmapSource"/> instances. Thumbnails live in a separate map so
/// zoom/scroll never drop sidebar bitmaps.
/// </summary>
internal sealed class PageBitmapCache
{
    private readonly PageRasterCache<BitmapSource> _inner = new(PageRenderPlanner.MaxCachedPageRasters);

    public int FocusPage
    {
        get => _inner.FocusPage;
        set => _inner.FocusPage = value;
    }

    public bool TryGet(int pageIndex, double zoom, out BitmapSource? bitmap) =>
        _inner.TryGet(pageIndex, PdfRenderScale.ZoomCacheKey(zoom), out bitmap);

    public void Set(int pageIndex, double zoom, BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!bitmap.IsFrozen)
            throw new ArgumentException("Bitmap must be frozen before cache insert.", nameof(bitmap));

        _inner.Set(pageIndex, PdfRenderScale.ZoomCacheKey(zoom), bitmap);
    }

    public bool TryGetThumb(int pageIndex, out BitmapSource? bitmap) =>
        _inner.TryGetThumb(pageIndex, out bitmap);

    public void SetThumb(int pageIndex, BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!bitmap.IsFrozen)
            throw new ArgumentException("Bitmap must be frozen before cache insert.", nameof(bitmap));

        _inner.SetThumb(pageIndex, bitmap);
    }

    public void Clear() => _inner.Clear();

    /// <summary>Clear main-page rasters only — leave sidebar thumbs intact.</summary>
    public void ClearPages() => _inner.ClearPages();

    public void Trim() => _inner.Trim();
}
