namespace ClearPDF.Pdfium;

/// <summary>BGRA32 page raster. UI layers turn this into WPF/Avalonia bitmaps.</summary>
public readonly record struct PdfRaster(byte[] Bgra32, int Width, int Height)
{
    public int Stride => Width * 4;

    public bool IsEmpty => Bgra32.Length == 0 || Width <= 0 || Height <= 0;
}
