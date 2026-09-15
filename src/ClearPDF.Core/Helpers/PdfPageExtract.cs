namespace ClearPDF.Helpers;

/// <summary>
/// Page-extract planning: normalize a 0-based set, Docnet 1-based range
/// strings, and Save As / Save pages as… filename hints. Does not write PDFs
/// (Docnet Split lives in the WPF document service).
/// </summary>
public static class PdfPageExtract
{
    /// <summary>
    /// Sort, unique, clamp to <c>[0, pageCount)</c>. Drops junk indices.
    /// </summary>
    public static IReadOnlyList<int> Normalize(IEnumerable<int>? pages, int pageCount)
    {
        if (pageCount <= 0 || pages == null)
            return Array.Empty<int>();

        var set = new SortedSet<int>();
        foreach (var p in pages)
        {
            if (p >= 0 && p < pageCount)
                set.Add(p);
        }

        return set.Count == 0 ? Array.Empty<int>() : set.ToArray();
    }

    /// <summary>
    /// Pages to extract. Empty selection falls back to the current viewer page
    /// (Save pages as… with only the left-rail page).
    /// </summary>
    public static IReadOnlyList<int> PagesForExtract(
        IEnumerable<int>? selected,
        int currentPage0,
        int pageCount)
    {
        var pages = Normalize(selected, pageCount);
        if (pages.Count > 0)
            return pages;
        if (pageCount <= 0)
            return Array.Empty<int>();
        return new[] { Math.Clamp(currentPage0, 0, pageCount - 1) };
    }

    /// <summary>
    /// Plain Save As offers selected vs whole document only when more than one
    /// page is selected. Size 0/1 keeps today's whole-file copy.
    /// </summary>
    public static bool ShouldOfferSaveAsChoice(int selectedCount) =>
        selectedCount > 1;

    /// <summary>
    /// Docnet <c>Split(bytes, pageRange)</c> string — <b>1-based</b>,
    /// e.g. <c>1,3,5-7</c>. Empty set → empty string.
    /// </summary>
    public static string ToDocnetPageRange(IReadOnlyList<int> pages0)
    {
        if (pages0 == null || pages0.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        var runStart = pages0[0];
        var runEnd = pages0[0];
        for (var i = 1; i < pages0.Count; i++)
        {
            var p = pages0[i];
            if (p == runEnd + 1)
            {
                runEnd = p;
                continue;
            }

            parts.Add(FormatRun(runStart, runEnd));
            runStart = runEnd = p;
        }

        parts.Add(FormatRun(runStart, runEnd));
        return string.Join(",", parts);
    }

    private static string FormatRun(int start0, int end0)
    {
        var from = start0 + 1;
        var to = end0 + 1;
        return from == to ? from.ToString() : $"{from}-{to}";
    }

    /// <summary>
    /// Suggested destination name next to the source stem
    /// (<c>report-page-3.pdf</c>, <c>report-pages-2-5.pdf</c>).
    /// </summary>
    public static string SuggestFileName(string? sourcePath, IReadOnlyList<int> pages0)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(stem))
            stem = "document";

        if (pages0 == null || pages0.Count == 0)
            return stem + ".pdf";

        if (pages0.Count == 1)
            return $"{stem}-page-{pages0[0] + 1}.pdf";

        var first = pages0[0] + 1;
        var last = pages0[pages0.Count - 1] + 1;
        var contiguous = pages0.Count == last - first + 1;
        if (contiguous)
            return $"{stem}-pages-{first}-{last}.pdf";

        if (pages0.Count <= 5)
            return $"{stem}-pages-{string.Join("-", pages0.Select(p => p + 1))}.pdf";

        return $"{stem}-pages-{first}-x{pages0.Count}.pdf";
    }
}
