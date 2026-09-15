namespace ClearPDF.Helpers;

/// <summary>
/// Picks the current page from stacked page tops + a viewport center.
/// UI-free — WPF supplies DIP coordinates.
/// </summary>
public static class ScrollPagePicker
{
    /// <summary>
    /// Page whose top is nearest <paramref name="viewportCenterY"/> (same space as tops).
    /// </summary>
    public static int PickCurrentPage(IReadOnlyList<double> pageTops, double viewportCenterY)
    {
        if (pageTops.Count == 0)
            return 0;

        var best = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < pageTops.Count; i++)
        {
            var dist = Math.Abs(pageTops[i] - viewportCenterY);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// Fast path while scrolling: only inspect <paramref name="hint"/> ± 2.
    /// Falls back to a full scan if the winner sits on the local-window edge
    /// (the fling jumped more than two pages).
    /// </summary>
    public static int PickCurrentPageNear(
        IReadOnlyList<double> pageTops,
        double viewportCenterY,
        int hint)
    {
        if (pageTops.Count == 0)
            return 0;

        hint = Math.Clamp(hint, 0, pageTops.Count - 1);
        var lo = Math.Max(0, hint - 2);
        var hi = Math.Min(pageTops.Count - 1, hint + 2);

        var best = hint;
        var bestDist = Math.Abs(pageTops[hint] - viewportCenterY);
        for (var i = lo; i <= hi; i++)
        {
            var dist = Math.Abs(pageTops[i] - viewportCenterY);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        var onInnerEdge = (best == lo && lo > 0) || (best == hi && hi < pageTops.Count - 1);
        return onInnerEdge
            ? PickCurrentPage(pageTops, viewportCenterY)
            : best;
    }
}
