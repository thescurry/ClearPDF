namespace ClearPDF.Helpers;

/// <summary>
/// Per-document find box state. UI-free so Linux tests can lock Enter / next /
/// previous / Esc without Avalonia or WPF.
/// </summary>
public sealed class FindSession
{
    private bool _hasSearched;

    public bool IsOpen { get; private set; }
    public string Query { get; private set; } = string.Empty;
    public IReadOnlyList<FindHelper.Hit> Hits { get; private set; } = Array.Empty<FindHelper.Hit>();
    public int CurrentIndex { get; private set; } = -1;

    public int CurrentPage => FindHelper.CurrentHitPage(Hits, CurrentIndex);

    public string StatusText
    {
        get
        {
            if (!IsOpen || !_hasSearched)
                return string.Empty;
            if (Hits.Count == 0)
                return "No matches";
            if (CurrentIndex < 0)
                return $"{Hits.Count} matches";
            return $"{CurrentIndex + 1} / {Hits.Count}";
        }
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            IsOpen = true;
    }

    public void Open() => IsOpen = true;

    public void Close()
    {
        IsOpen = false;
        CurrentIndex = -1;
    }

    /// <summary>Enter: re-run the query from the start, then jump to the first hit.</summary>
    public void Commit(IReadOnlyList<string> pageTexts, string query)
    {
        Search(pageTexts, query, resetIndex: true);
        CurrentIndex = FindHelper.NextIndex(Hits.Count, CurrentIndex);
    }

    public void Next(IReadOnlyList<string> pageTexts, string query)
    {
        Search(pageTexts, query, resetIndex: false);
        CurrentIndex = FindHelper.NextIndex(Hits.Count, CurrentIndex);
    }

    public void Previous(IReadOnlyList<string> pageTexts, string query)
    {
        Search(pageTexts, query, resetIndex: false);
        CurrentIndex = FindHelper.PrevIndex(Hits.Count, CurrentIndex);
    }

    private void Search(IReadOnlyList<string> pageTexts, string query, bool resetIndex)
    {
        Query = query ?? string.Empty;
        Hits = FindHelper.FindAll(pageTexts, Query.Trim());
        _hasSearched = true;
        if (resetIndex || Hits.Count == 0)
            CurrentIndex = -1;
        else if (CurrentIndex >= Hits.Count)
            CurrentIndex = Hits.Count - 1;
    }
}
