namespace ClearPDF.Models;

public sealed class BookmarkItem
{
    public string Title { get; init; } = string.Empty;
    public int PageIndex { get; init; }
    public IList<BookmarkItem> Children { get; init; } = new List<BookmarkItem>();
}
