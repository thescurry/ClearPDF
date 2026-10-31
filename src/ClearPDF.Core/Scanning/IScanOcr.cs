namespace ClearPDF.Scanning;

/// <summary>
/// Optional searchable-PDF hook. ClearPDF does not ship an OCR engine —
/// Tesseract (and its tessdata) is a large native bundle and not a clean
/// offline default for this app. Implement this to add one later.
/// </summary>
public interface IScanOcr
{
    bool IsAvailable { get; }
    string UnavailableReason { get; }
    Task<string> MakeSearchableAsync(string pdfPath, CancellationToken cancellationToken);
}

public sealed class UnavailableScanOcr : IScanOcr
{
    public static readonly UnavailableScanOcr Instance = new();

    public bool IsAvailable => false;

    public string UnavailableReason =>
        "Searchable PDF (OCR) is not bundled. A future build can plug in an offline engine via IScanOcr.";

    public Task<string> MakeSearchableAsync(string pdfPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(pdfPath);
    }
}
