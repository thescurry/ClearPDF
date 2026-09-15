namespace ClearPDF.Helpers;

/// <summary>
/// Thread-safe page-raster cache keyed by (pageIndex, zoomKey), with a
/// separate thumb map so zoom/scroll never drop sidebar bitmaps.
/// Bounded by <see cref="PageRenderPlanner.MaxCachedPageRasters"/>; thumbs
/// are not counted against that budget.
/// </summary>
public sealed class PageRasterCache<T> where T : class
{
    private readonly object _gate = new();
    private readonly Dictionary<PageKey, T> _pages = new();
    private readonly Dictionary<int, T> _thumbs = new();
    private readonly int _maxPageEntries;

    private readonly record struct PageKey(int PageIndex, int ZoomKey);

    public PageRasterCache(int maxPageEntries = PageRenderPlanner.MaxCachedPageRasters)
    {
        if (maxPageEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPageEntries));
        _maxPageEntries = maxPageEntries;
    }

    /// <summary>Focus page used when trimming — keep rasters nearest this index.</summary>
    public int FocusPage { get; set; }

    public int PageEntryCount
    {
        get { lock (_gate) return _pages.Count; }
    }

    public int ThumbCount
    {
        get { lock (_gate) return _thumbs.Count; }
    }

    public bool TryGet(int pageIndex, int zoomKey, out T? value)
    {
        lock (_gate)
        {
            if (_pages.TryGetValue(new PageKey(pageIndex, zoomKey), out var found))
            {
                value = found;
                return true;
            }
        }

        value = null;
        return false;
    }

    public void Set(int pageIndex, int zoomKey, T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            _pages[new PageKey(pageIndex, zoomKey)] = value;
            TrimUnlocked();
        }
    }

    public bool TryGetThumb(int pageIndex, out T? value)
    {
        lock (_gate)
        {
            if (_thumbs.TryGetValue(pageIndex, out var found))
            {
                value = found;
                return true;
            }
        }

        value = null;
        return false;
    }

    public void SetThumb(int pageIndex, T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            _thumbs[pageIndex] = value;
        }
    }

    /// <summary>Drop main-page rasters only — sidebar thumbs stay.</summary>
    public void ClearPages()
    {
        lock (_gate)
        {
            _pages.Clear();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _pages.Clear();
            _thumbs.Clear();
        }
    }

    /// <summary>Evict farthest-from-focus page rasters down to the budget.</summary>
    public void Trim()
    {
        lock (_gate)
        {
            TrimUnlocked();
        }
    }

    private void TrimUnlocked()
    {
        while (_pages.Count > _maxPageEntries)
        {
            PageKey? worst = null;
            var worstDist = int.MinValue;
            foreach (var key in _pages.Keys)
            {
                var dist = Math.Abs(key.PageIndex - FocusPage);
                if (dist > worstDist)
                {
                    worstDist = dist;
                    worst = key;
                }
            }

            if (worst is { } victim)
                _pages.Remove(victim);
            else
                break;
        }
    }
}
