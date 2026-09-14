namespace ClearPDF.Helpers;

/// <summary>
/// Pure zoom math for fit-width / fit-page and step zoom. UI-free for unit tests.
/// </summary>
public static class ZoomHelper
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;
    public const double DefaultZoom = 1.0;
    public const double ZoomStep = 1.25;

    public static double Clamp(double zoom) =>
        Math.Clamp(zoom, MinZoom, MaxZoom);

    public static double ZoomIn(double current) =>
        Clamp(current * ZoomStep);

    public static double ZoomOut(double current) =>
        Clamp(current / ZoomStep);

    /// <summary>
    /// Scale so page width fits the viewport width (accounting for padding).
    /// </summary>
    public static double FitWidth(double pageWidth, double viewportWidth, double padding = 24)
    {
        if (pageWidth <= 0 || viewportWidth <= 0)
            return DefaultZoom;

        var available = Math.Max(1, viewportWidth - padding);
        return Clamp(available / pageWidth);
    }

    /// <summary>
    /// Scale so the whole page fits both width and height of the viewport.
    /// </summary>
    public static double FitPage(
        double pageWidth,
        double pageHeight,
        double viewportWidth,
        double viewportHeight,
        double padding = 24)
    {
        if (pageWidth <= 0 || pageHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            return DefaultZoom;

        var availW = Math.Max(1, viewportWidth - padding);
        var availH = Math.Max(1, viewportHeight - padding);
        var scale = Math.Min(availW / pageWidth, availH / pageHeight);
        return Clamp(scale);
    }
}
