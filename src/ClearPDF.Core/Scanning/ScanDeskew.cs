namespace ClearPDF.Scanning;

/// <summary>
/// Small-angle straighten via horizontal projection variance on a
/// downsampled grayscale. Typical feeder skew is a few degrees; we search
/// ±12°. Uniform pages (no edges) stay put.
/// </summary>
public static class ScanDeskew
{
    public const double MaxDegrees = 12;
    public const double StepDegrees = 0.5;
    public const double SkipBelow = 0.4;

    public static double EstimateAngle(byte[] rgb, int width, int height)
    {
        if (rgb == null || width < 8 || height < 8)
            return 0;

        var (gray, w, h) = ScanRaster.DownsampleGray(rgb, width, height, 160);
        if (IsNearlyUniform(gray))
            return 0;

        double bestVar = -1;
        double worstVar = double.MaxValue;
        var best = 0.0;
        for (var angle = -MaxDegrees; angle <= MaxDegrees + 1e-9; angle += StepDegrees)
        {
            var variance = RowProjectionVariance(gray, w, h, angle);
            if (variance > bestVar)
            {
                bestVar = variance;
                best = angle;
            }

            if (variance < worstVar)
                worstVar = variance;
        }

        if (bestVar < worstVar * 1.12)
            return 0;
        if (Math.Abs(best) < SkipBelow)
            return 0;
        return best;
    }

    public static (byte[] Rgb, int Width, int Height, double AngleDegrees) Straighten(
        byte[] rgb, int width, int height)
    {
        var angle = EstimateAngle(rgb, width, height);
        if (Math.Abs(angle) < SkipBelow)
            return (rgb, width, height, 0);

        var rotated = ScanRaster.Rotate(rgb, width, height, -angle);
        return (rotated.Rgb, rotated.Width, rotated.Height, angle);
    }

    private static bool IsNearlyUniform(byte[] gray)
    {
        long sum = 0;
        long sq = 0;
        for (var i = 0; i < gray.Length; i++)
        {
            sum += gray[i];
            sq += gray[i] * gray[i];
        }

        var n = (double)gray.Length;
        var mean = sum / n;
        var var = sq / n - mean * mean;
        return var < 25;
    }

    private static double RowProjectionVariance(byte[] gray, int width, int height, double degrees)
    {
        var rad = degrees * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var cx = (width - 1) / 2.0;
        var cy = (height - 1) / 2.0;
        var rows = new double[height];
        for (var y = 0; y < height; y++)
        {
            var ink = 0.0;
            var dy = y - cy;
            for (var x = 0; x < width; x++)
            {
                var dx = x - cx;
                var sx = dx * cos - dy * sin + cx;
                var sy = dx * sin + dy * cos + cy;
                var ix = (int)Math.Round(sx);
                var iy = (int)Math.Round(sy);
                if ((uint)ix >= (uint)width || (uint)iy >= (uint)height)
                    continue;
                ink += 255 - gray[iy * width + ix];
            }

            rows[y] = ink;
        }

        var mean = rows.Average();
        var acc = 0.0;
        for (var i = 0; i < rows.Length; i++)
        {
            var d = rows[i] - mean;
            acc += d * d;
        }

        return acc / rows.Length;
    }
}
