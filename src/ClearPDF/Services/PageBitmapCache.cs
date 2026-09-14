using System.Windows.Media.Imaging;
using ClearPDF.Helpers;

namespace ClearPDF.Services;

/// <summary>
/// Bitmap cache keyed by (pageIndex, quantizedZoom). Thread-safe reads;
/// writes expect frozen <see cref="BitmapSource"/> instances.
/// Thumbnail bitmaps live in a separate map so they never share keys with
/// main-page / preview rasters and are not cleared by page-zoom churn.
/// </summary>
internal sealed class PageBitmapCache
{
    private readonly object _gate = new();
    private readonly Dictionary<CacheKey, BitmapSource> _map = new();
    private readonly Dictionary<int, BitmapSource> _thumbs = new();

    private readonly record struct CacheKey(int PageIndex, int ZoomKey);

    public bool TryGet(int pageIndex, double zoom, out BitmapSource? bitmap)
    {
        var key = MakeKey(pageIndex, zoom);
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var bmp))
            {
                bitmap = bmp;
                return true;
            }
        }

        bitmap = null;
        return false;
    }

    public void Set(int pageIndex, double zoom, BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!bitmap.IsFrozen)
            throw new ArgumentException("Bitmap must be frozen before cache insert.", nameof(bitmap));

        var key = MakeKey(pageIndex, zoom);
        lock (_gate)
        {
            _map[key] = bitmap;
        }
    }

    public bool TryGetThumb(int pageIndex, out BitmapSource? bitmap)
    {
        lock (_gate)
        {
            if (_thumbs.TryGetValue(pageIndex, out var bmp))
            {
                bitmap = bmp;
                return true;
            }
        }

        bitmap = null;
        return false;
    }

    public void SetThumb(int pageIndex, BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!bitmap.IsFrozen)
            throw new ArgumentException("Bitmap must be frozen before cache insert.", nameof(bitmap));

        lock (_gate)
        {
            _thumbs[pageIndex] = bitmap;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _thumbs.Clear();
        }
    }

    /// <summary>Clear main-page rasters only — leave sidebar thumbs intact.</summary>
    public void ClearPages()
    {
        lock (_gate)
        {
            _map.Clear();
        }
    }

    private static CacheKey MakeKey(int pageIndex, double zoom) =>
        new(pageIndex, PdfRenderScale.ZoomCacheKey(zoom));
}
