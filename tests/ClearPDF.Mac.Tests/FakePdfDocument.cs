using ClearPDF.Imaging;
using ClearPDF.Models;
using ClearPDF.Services;

namespace ClearPDF.Mac.Tests;

internal sealed class FakePdfDocument : IPdfDocument
{
    private readonly string[] _texts;

    public FakePdfDocument(string path, params string[] pageTexts)
    {
        FilePath = path;
        _texts = pageTexts.Length == 0 ? new[] { "hello world", "hello again" } : pageTexts;
    }

    public string FilePath { get; }
    public int PageCount => _texts.Length;
    public bool IsPasswordProtected => false;
    public int DisposeCount { get; private set; }

    public PdfPixelBuffer? RenderPage(int pageIndex, double zoom) =>
        PdfPixelBuffer.CreatePlaceholder(32, 32);

    public (double Width, double Height) GetPageSize(int pageIndex) => (72, 72);

    public string GetPageText(int pageIndex) =>
        pageIndex >= 0 && pageIndex < _texts.Length ? _texts[pageIndex] : string.Empty;

    public IReadOnlyList<BookmarkItem> GetBookmarks() => Array.Empty<BookmarkItem>();

    public void ExtractPages(IReadOnlyList<int> pageIndices0, string destPath)
    {
    }

    public void Dispose() => DisposeCount++;
}
