namespace ClearPDF.Helpers;

/// <summary>
/// Thumbnail multi-select set. Ordered by page index for extract/print.
/// Click / Ctrl-toggle / Shift-range — UI-free so Linux tests can lock it.
/// </summary>
public readonly struct PageSet
{
    public static PageSet Empty { get; } = new(Array.Empty<int>(), 0);

    public PageSet(IReadOnlyList<int> pages, int anchor)
    {
        Pages = pages;
        Anchor = anchor;
    }

    /// <summary>0-based page indices, sorted, unique.</summary>
    public IReadOnlyList<int> Pages { get; }

    /// <summary>Shift-range start. Last click or toggle target.</summary>
    public int Anchor { get; }

    public int Count => Pages.Count;

    public bool Contains(int page)
    {
        for (var i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == page)
                return true;
        }

        return false;
    }
}

/// <summary>
/// Applies click / Ctrl-toggle / Shift-range to a <see cref="PageSet"/>.
/// Selection is always ordered by page index.
/// </summary>
public static class PageSelection
{
    /// <summary>Plain click — one page, new anchor.</summary>
    public static PageSet Click(int page, int pageCount)
    {
        if (pageCount <= 0)
            return PageSet.Empty;

        var p = Math.Clamp(page, 0, pageCount - 1);
        return new PageSet(new[] { p }, p);
    }

    /// <summary>Ctrl/Cmd+click — toggle <paramref name="page"/> in the set.</summary>
    public static PageSet Toggle(PageSet current, int page, int pageCount)
    {
        if (pageCount <= 0)
            return PageSet.Empty;

        var p = Math.Clamp(page, 0, pageCount - 1);
        var next = new List<int>(current.Pages.Count + 1);
        var removed = false;
        foreach (var i in current.Pages)
        {
            if (i < 0 || i >= pageCount)
                continue;
            if (i == p)
            {
                removed = true;
                continue;
            }

            next.Add(i);
        }

        if (!removed)
            next.Add(p);

        next.Sort();
        return new PageSet(next, p);
    }

    /// <summary>
    /// Shift+click — contiguous range from <paramref name="anchor"/> to
    /// <paramref name="page"/> (inclusive). Replaces the set; keeps the anchor.
    /// </summary>
    public static PageSet Range(int anchor, int page, int pageCount)
    {
        if (pageCount <= 0)
            return PageSet.Empty;

        var a = Math.Clamp(anchor, 0, pageCount - 1);
        var p = Math.Clamp(page, 0, pageCount - 1);
        var lo = Math.Min(a, p);
        var hi = Math.Max(a, p);
        var pages = new int[hi - lo + 1];
        for (var i = 0; i < pages.Length; i++)
            pages[i] = lo + i;

        return new PageSet(pages, a);
    }

    /// <summary>
    /// Scroll / keyboard when the set is a single page: move selection with
    /// the current viewer page (today's left-rail highlight).
    /// </summary>
    public static PageSet FollowCurrent(int currentPage, int pageCount) =>
        Click(currentPage, pageCount);

    /// <summary>
    /// Multi-select is "active" when more than one page is in the set.
    /// A lone current-page highlight is not a Save As choice trigger.
    /// </summary>
    public static bool IsMulti(PageSet set) => set.Count > 1;
}
