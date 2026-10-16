namespace ClearPDF.Helpers;

/// <summary>
/// Argument list for macOS/CUPS <c>lp</c>. UI-free so Linux tests can lock
/// the print command without talking to a printer or signing into Apple.
/// </summary>
public static class CupsPrintArgs
{
    public const string LpExecutable = "lp";
    public const string LprExecutable = "lpr";

    /// <summary>
    /// <c>lp [-d printer] -- path</c>. <paramref name="pdfPath"/> is the file
    /// already narrowed to the pages the user chose (whole doc or a Split).
    /// </summary>
    public static IReadOnlyList<string> BuildLp(string pdfPath, string? printer = null)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("PDF path is required.", nameof(pdfPath));

        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(printer))
        {
            args.Add("-d");
            args.Add(printer.Trim());
        }

        args.Add("--");
        args.Add(pdfPath);
        return args;
    }

    public static IReadOnlyList<string> BuildLpr(string pdfPath, string? printer = null)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("PDF path is required.", nameof(pdfPath));

        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(printer))
        {
            args.Add("-P");
            args.Add(printer.Trim());
        }

        args.Add("--");
        args.Add(pdfPath);
        return args;
    }
}
