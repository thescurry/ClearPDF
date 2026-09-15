namespace ClearPDF.Helpers;

/// <summary>
/// Pure planning for async page rasters + thumbnail cache. UI-free so Linux
/// tests can lock the v1.1 feel contract: sharp window, no thumb rebuild on
/// zoom, scroll does not cancel in-window work.
/// </summary>
public static class PageRenderPlanner
{
    /// <summary>Pages on either side of current that get sharp bitmaps.</summary>
    public const int SharpWindowRadius = 2;

    /// <summary>Zoom-settle debounce (tightened from 150ms).</summary>
    public const int ZoomDebounceMs = 120;

    /// <summary>
    /// Scroll only updates chrome immediately; sharp rasters wait this long
    /// so a fast fling does not enqueue a job per page crossing.
    /// </summary>
    public const int ScrollRenderDebounceMs = 80;

    /// <summary>Cap concurrent Docnet rasters (PDFium is process-wide gated anyway).</summary>
    public const int MaxConcurrentRasters = 2;

    /// <summary>Max main-page cache entries (sharp + preview). Thumbs are separate.</summary>
    public const int MaxCachedPageRasters = 24;

    /// <summary>Thumbnails are filled once at open and never rebuilt on zoom.</summary>
    public static bool ShouldRebuildThumbsOnZoom => false;

    /// <summary>Thumb bitmaps live in a separate map — never share page-zoom keys.</summary>
    public static bool ThumbsSharePageCache => false;

    public static (int Lo, int Hi) SharpWindow(
        int currentPage,
        int pageCount,
        int radius = SharpWindowRadius)
    {
        if (pageCount <= 0)
            return (0, -1);

        var cur = Math.Clamp(currentPage, 0, pageCount - 1);
        var lo = Math.Max(0, cur - radius);
        var hi = Math.Min(pageCount - 1, cur + radius);
        return (lo, hi);
    }

    public static bool IsInSharpWindow(
        int pageIndex,
        int currentPage,
        int pageCount,
        int radius = SharpWindowRadius)
    {
        var (lo, hi) = SharpWindow(currentPage, pageCount, radius);
        return pageIndex >= lo && pageIndex <= hi;
    }

    /// <summary>
    /// Work order: current, then neighbors, then optional low-res previews.
    /// </summary>
    public static IReadOnlyList<(int Page, bool Sharp)> BuildJobs(
        int currentPage,
        int pageCount,
        bool includePreviews,
        int radius = SharpWindowRadius)
    {
        var work = new List<(int Page, bool Sharp)>();
        if (pageCount <= 0)
            return work;

        var cur = Math.Clamp(currentPage, 0, pageCount - 1);
        var (lo, hi) = SharpWindow(cur, pageCount, radius);
        work.Add((cur, true));
        for (var i = lo; i <= hi; i++)
        {
            if (i != cur)
                work.Add((i, true));
        }

        if (includePreviews)
        {
            for (var i = 0; i < pageCount; i++)
            {
                if (i >= lo && i <= hi)
                    continue;
                work.Add((i, false));
            }
        }

        return work;
    }

    /// <summary>
    /// Zoom/open may cancel everything. Scroll must not cancel an in-flight
    /// raster that is still inside the new sharp window.
    /// </summary>
    public static bool ShouldCancelInFlight(
        bool includePreviews,
        int pageIndex,
        int newCurrentPage,
        int pageCount,
        int radius = SharpWindowRadius)
    {
        if (includePreviews)
            return true;
        return !IsInSharpWindow(pageIndex, newCurrentPage, pageCount, radius);
    }

    /// <summary>
    /// Skip applying a preview bitmap when a sharp frame at the committed zoom
    /// is already showing.
    /// </summary>
    public static bool ShouldKeepExistingBitmap(bool incomingIsPreview, bool alreadyShowingSharp) =>
        incomingIsPreview && alreadyShowingSharp;
}
