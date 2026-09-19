namespace ClearPDF.Helpers;

/// <summary>Print-dialog range kind. Selection is the thumb set (or current page when size 1).</summary>
public enum PrintRangeMode
{
    AllPages = 0,
    /// <summary>
    /// Windows Print dialog "Selection" — multi-select thumbs when size &gt; 1,
    /// otherwise the current left-rail / viewer page.
    /// </summary>
    Selection = 1,
    UserPages = 2
}

/// <summary>
/// Maps a print-dialog choice to PDF pages.
/// UI-free so Linux tests can lock "Selection = current page when size 1,
/// selected indices when multi".
/// </summary>
public static class PrintPageRange
{
    /// <summary>
    /// Resolve a contiguous 0-based inclusive span.
    /// Selection (this overload) is always the current page — keep for
    /// size-1 / legacy. Prefer <see cref="ResolvePages"/> when a set exists.
    /// <paramref name="userFrom1Based"/> / <paramref name="userTo1Based"/> are
    /// only used for <see cref="PrintRangeMode.UserPages"/> (dialog is 1-based).
    /// </summary>
    public static (int StartInclusive, int EndInclusive) Resolve(
        PrintRangeMode mode,
        int currentPage0,
        int pageCount,
        int userFrom1Based = 1,
        int userTo1Based = 1)
    {
        if (pageCount <= 0)
            return (0, -1);

        var last = pageCount - 1;
        switch (mode)
        {
            case PrintRangeMode.Selection:
                var cur = Math.Clamp(currentPage0, 0, last);
                return (cur, cur);

            case PrintRangeMode.UserPages:
                var start = Math.Clamp(userFrom1Based - 1, 0, last);
                var end = Math.Clamp(userTo1Based - 1, 0, last);
                if (end < start)
                    end = start;
                return (start, end);

            default:
                return (0, last);
        }
    }

    /// <summary>
    /// Pages to send to the paginator (0-based, document order).
    /// Selection size ≤ 1 → current page (today). Multi-select → exact set
    /// (holes allowed). UserPages stays a contiguous from–to span.
    /// </summary>
    public static IReadOnlyList<int> ResolvePages(
        PrintRangeMode mode,
        int currentPage0,
        int pageCount,
        IEnumerable<int>? selectedPages0 = null,
        int userFrom1Based = 1,
        int userTo1Based = 1)
    {
        if (pageCount <= 0)
            return Array.Empty<int>();

        if (mode == PrintRangeMode.Selection)
        {
            var selected = PdfPageExtract.Normalize(selectedPages0, pageCount);
            if (selected.Count > 1)
                return selected;
            var cur = Math.Clamp(currentPage0, 0, pageCount - 1);
            return new[] { cur };
        }

        var (start, end) = Resolve(mode, currentPage0, pageCount, userFrom1Based, userTo1Based);
        if (end < start)
            return Array.Empty<int>();

        var pages = new int[end - start + 1];
        for (var i = 0; i < pages.Length; i++)
            pages[i] = start + i;
        return pages;
    }

    /// <summary>
    /// 1-based From/To to prefill the print dialog page-range control.
    /// Multi-select uses min..max of the set (holes only print via Selection).
    /// </summary>
    public static (int From1Based, int To1Based) DialogPageRange(
        IEnumerable<int>? selectedPages0,
        int currentPage0,
        int pageCount)
    {
        if (pageCount <= 0)
            return (1, 1);

        var selected = PdfPageExtract.Normalize(selectedPages0, pageCount);
        if (selected.Count > 0)
            return (selected[0] + 1, selected[selected.Count - 1] + 1);

        var cur = Math.Clamp(currentPage0, 0, pageCount - 1) + 1;
        return (cur, cur);
    }
}
