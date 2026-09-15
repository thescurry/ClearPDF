namespace ClearPDF.Helpers;

/// <summary>
/// Case-insensitive find-next / find-previous over a flat page text index.
/// UI-free for unit tests.
/// </summary>
public static class FindHelper
{
    public readonly record struct Hit(int PageIndex, int CharOffset, int Length);

    /// <summary>
    /// Finds all occurrences of <paramref name="query"/> in <paramref name="pageTexts"/>
    /// (one string per page, in order). Empty query yields no hits.
    /// </summary>
    public static IReadOnlyList<Hit> FindAll(IReadOnlyList<string> pageTexts, string query)
    {
        if (string.IsNullOrEmpty(query) || pageTexts.Count == 0)
            return Array.Empty<Hit>();

        var hits = new List<Hit>();
        for (var page = 0; page < pageTexts.Count; page++)
        {
            var text = pageTexts[page] ?? string.Empty;
            var start = 0;
            while (start < text.Length)
            {
                var idx = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    break;
                hits.Add(new Hit(page, idx, query.Length));
                start = idx + Math.Max(1, query.Length);
            }
        }

        return hits;
    }

    /// <summary>
    /// Next hit after <paramref name="currentIndex"/>, wrapping to 0. Returns -1 if none.
    /// </summary>
    public static int NextIndex(int hitCount, int currentIndex)
    {
        if (hitCount <= 0)
            return -1;
        if (currentIndex < 0 || currentIndex >= hitCount - 1)
            return 0;
        return currentIndex + 1;
    }

    /// <summary>
    /// Previous hit before <paramref name="currentIndex"/>, wrapping to last. Returns -1 if none.
    /// </summary>
    public static int PrevIndex(int hitCount, int currentIndex)
    {
        if (hitCount <= 0)
            return -1;
        if (currentIndex <= 0 || currentIndex >= hitCount)
            return hitCount - 1;
        return currentIndex - 1;
    }

    /// <summary>Page that should show the muted find-hit accent, or -1.</summary>
    public static int CurrentHitPage(IReadOnlyList<Hit> hits, int currentIndex)
    {
        if (currentIndex < 0 || currentIndex >= hits.Count)
            return -1;
        return hits[currentIndex].PageIndex;
    }
}
