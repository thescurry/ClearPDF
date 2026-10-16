namespace ClearPDF.Scanning;

/// <summary>
/// In-memory strip of scanned pages: add, delete, reorder, rotate.
/// Does not talk to hardware or write PDFs.
/// </summary>
public sealed class ScanSession
{
    private readonly List<ScannedPage> _pages = new();

    public IReadOnlyList<ScannedPage> Pages => _pages;

    public int Count => _pages.Count;

    public void Add(ScannedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _pages.Add(page);
    }

    public void AddRange(IEnumerable<ScannedPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        foreach (var page in pages)
            Add(page);
    }

    public void Delete(int index)
    {
        EnsureIndex(index);
        _pages.RemoveAt(index);
    }

    /// <summary>
    /// Move the page at <paramref name="fromIndex"/> so it ends at
    /// <paramref name="toIndex"/> (the destination index in the list after removal).
    /// </summary>
    public void Move(int fromIndex, int toIndex)
    {
        EnsureIndex(fromIndex);
        if (toIndex < 0 || toIndex >= _pages.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));
        if (fromIndex == toIndex)
            return;

        var page = _pages[fromIndex];
        _pages.RemoveAt(fromIndex);
        _pages.Insert(toIndex, page);
    }

    public void MoveUp(int index)
    {
        EnsureIndex(index);
        if (index == 0)
            return;
        Move(index, index - 1);
    }

    public void MoveDown(int index)
    {
        EnsureIndex(index);
        if (index >= _pages.Count - 1)
            return;
        Move(index, index + 1);
    }

    public void Rotate(int index, int deltaDegrees = 90)
    {
        EnsureIndex(index);
        _pages[index] = _pages[index].RotateBy(deltaDegrees);
    }

    public void Clear() => _pages.Clear();

    private void EnsureIndex(int index)
    {
        if (index < 0 || index >= _pages.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
    }
}
