namespace ClearPDF.Helpers;

/// <summary>
/// Converts Docnet/PDFium page dimensions to PDF points (1/72").
/// <para>
/// Docnet <c>PageDimensions(scalingFactor)</c> uses scalingFactor as pixels-per-point
/// (PPI/72). With the app's baseline <see cref="DocnetRenderScale"/> of 2.0 (~144 DPI),
/// <c>GetPageWidth/Height</c> return <b>pixels</b>, not points. Fit-width/fit-page and
/// print must use points — divide pixel sizes by the same scale factor.
/// </para>
/// </summary>
public static class PdfPageSize
{
    /// <summary>
    /// Must match <c>new PageDimensions(DocnetRenderScale)</c> used when opening docs.
    /// </summary>
    public const double DocnetRenderScale = 2.0;

    /// <summary>US Letter in PDF points.</summary>
    public const double LetterWidthPoints = 612;

    /// <summary>US Letter in PDF points.</summary>
    public const double LetterHeightPoints = 792;

    /// <summary>
    /// Convert Docnet pixel dimensions (at <paramref name="renderScale"/>) to PDF points.
    /// </summary>
    public static (double Width, double Height) FromDocnetPixels(
        double widthPixels,
        double heightPixels,
        double renderScale = DocnetRenderScale)
    {
        if (renderScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(renderScale));
        return (widthPixels / renderScale, heightPixels / renderScale);
    }
}
