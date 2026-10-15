using ClearPDF.Scanning;

namespace ClearPDF.Tests;

/// <summary>
/// Tiny tagged scan pages for session / PDF tests. Tag is tracked by page Id
/// in this test assembly only.
/// </summary>
internal static class ScanTestPages
{
    private static readonly Dictionary<Guid, int> Tags = new();

    public static ScannedPage Solid(
        int width,
        int height,
        int dpi,
        int tag = 0,
        ScanColorMode colorMode = ScanColorMode.Color)
    {
        var page = MockScanner.CreateSolidPage(width, height, dpi, colorMode, (byte)(tag & 0xFF), 40, 80);
        Tags[page.Id] = tag;
        return page;
    }

    public static int Tag(ScannedPage page) =>
        Tags.TryGetValue(page.Id, out var tag) ? tag : 0;
}
