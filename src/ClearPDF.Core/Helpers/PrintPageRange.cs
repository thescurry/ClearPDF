namespace ClearPDF.Helpers;

/// <summary>Print-dialog range kind. Selection is the left-rail current page.</summary>
public enum PrintRangeMode
{
    AllPages = 0,
    /// <summary>Windows Print dialog "Selection" — current highlighted thumb page.</summary>
    Selection = 1,
    UserPages = 2
}

/// <summary>
/// Maps a print-dialog choice to a 0-based inclusive PDF page span.
/// UI-free so Linux tests can lock "Selection = current left-rail page".
/// </summary>
public static class PrintPageRange
{
    /// <summary>
    /// Resolve the pages to print.
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
}
