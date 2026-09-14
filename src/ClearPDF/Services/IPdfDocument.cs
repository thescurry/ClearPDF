using System.Windows.Media.Imaging;
using ClearPDF.Models;

namespace ClearPDF.Services;

/// <summary>
/// Abstraction over the PDF engine so UI can run even when natives are missing.
/// </summary>
public interface IPdfDocument : IDisposable
{
    string FilePath { get; }
    int PageCount { get; }
    bool IsPasswordProtected { get; }

    /// <summary>
    /// Rasterize page at the given zoom (1.0 ≈ baseline sharpness).
    /// Implementation may cap DPI on large pages. Safe to call off the UI thread;
    /// returned bitmaps are frozen.
    /// </summary>
    BitmapSource? RenderPage(int pageIndex, double zoom);

    /// <summary>Page size in PDF points (1/72").</summary>
    (double Width, double Height) GetPageSize(int pageIndex);

    /// <summary>Extract plain text for find. May be empty if engine stubs.</summary>
    string GetPageText(int pageIndex);

    IReadOnlyList<BookmarkItem> GetBookmarks();
}

public interface IPdfDocumentFactory
{
    /// <summary>
    /// Opens a PDF. Throws <see cref="PdfPasswordException"/> if a password is required
    /// or wrong. Never stores the password.
    /// </summary>
    IPdfDocument Open(string path, string? password = null);
}

public sealed class PdfPasswordException : Exception
{
    public PdfPasswordException(string message) : base(message) { }
}

public sealed class PdfOpenException : Exception
{
    public PdfOpenException(string message, Exception? inner = null) : base(message, inner) { }
}
