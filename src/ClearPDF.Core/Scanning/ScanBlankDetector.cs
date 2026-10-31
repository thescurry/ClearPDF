namespace ClearPDF.Scanning;

/// <summary>
/// Conservative blank check. Walks every pixel in tiles and keeps the page
/// if any tile has a real mark (a line, a signature, a faint pencil stroke).
/// Downsampling is not used — a 1-pixel line must survive.
/// </summary>
public static class ScanBlankDetector
{
    public const byte InkBelow = 236;
    public const int TileSize = 32;
    public const int MinTileInk = 8;
    public const int MinTotalInk = 80;

    public static bool IsBlank(ScannedPage page)
    {
        if (!ScanRaster.TryGetRgb(page, out var w, out var h, out var rgb))
            return false;
        return IsBlank(rgb, w, h);
    }

    public static bool IsBlank(byte[] rgb, int width, int height)
    {
        if (rgb == null || width <= 0 || height <= 0)
            return true;
        if (rgb.Length < width * height * 3)
            return true;

        var totalInk = 0;
        var maxTileInk = 0;
        for (var ty = 0; ty < height; ty += TileSize)
        {
            var y1 = Math.Min(height, ty + TileSize);
            for (var tx = 0; tx < width; tx += TileSize)
            {
                var x1 = Math.Min(width, tx + TileSize);
                var tileInk = 0;
                for (var y = ty; y < y1; y++)
                {
                    var row = y * width * 3;
                    for (var x = tx; x < x1; x++)
                    {
                        var p = row + x * 3;
                        if (ScanRaster.Luma(rgb[p], rgb[p + 1], rgb[p + 2]) < InkBelow)
                            tileInk++;
                    }
                }

                totalInk += tileInk;
                if (tileInk > maxTileInk)
                    maxTileInk = tileInk;
                if (maxTileInk >= MinTileInk)
                    return false;
            }
        }

        return totalInk < MinTotalInk;
    }
}
