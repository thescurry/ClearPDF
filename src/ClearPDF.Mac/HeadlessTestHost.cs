namespace ClearPDF.Mac;

/// <summary>Set by Avalonia.Headless tests so App does not create a second window.</summary>
internal static class HeadlessTestHost
{
    internal static bool Enabled { get; set; }
}