namespace ClearPDF.Pdfium;

public sealed class PdfPasswordException : Exception
{
    public PdfPasswordException(string message) : base(message) { }
}

public sealed class PdfOpenException : Exception
{
    public PdfOpenException(string message, Exception? inner = null) : base(message, inner) { }
}
