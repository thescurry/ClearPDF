namespace ClearPDF.Pdfium;

/// <summary>
/// UI-free PDF engine. Same contract as the WPF <c>IPdfDocument</c> minus
/// framework bitmaps — the Mac Avalonia shell and tests share this.
/// </summary>
public interface IPdfRasterDocument : IDisposable
{
    string FilePath { get; }
    int PageCount { get; }
    bool IsPasswordProtected { get; }

    /// <summary>
    /// Rasterize page at the given zoom (1.0 ≈ baseline sharpness).
    /// Safe to call off the UI thread. Implementation caps DPI on large pages.
    /// </summary>
    PdfRaster? RenderPage(int pageIndex, double zoom);

    /// <summary>Page size in PDF points (1/72").</summary>
    (double Width, double Height) GetPageSize(int pageIndex);

    /// <summary>Extract plain text for find. May be empty if the engine stubs.</summary>
    string GetPageText(int pageIndex);

    /// <summary>
    /// Write a new PDF of the given 0-based pages (page extract, not an edit
    /// of the open file). Throws if natives are missing or the set is empty.
    /// </summary>
    void ExtractPages(IReadOnlyList<int> pageIndices0, string destPath);
}

public interface IPdfRasterDocumentFactory
{
    /// <summary>
    /// Opens a PDF. Throws <see cref="PdfPasswordException"/> if a password is
    /// required or wrong. Never stores the password.
    /// </summary>
    IPdfRasterDocument Open(string path, string? password = null);
}
