namespace ClearPDF.Helpers;

/// <summary>
/// Avalonia theme contract for the Intel Mac MVP: follow the OS appearance
/// (light / dark). No forced-light ship and no custom night theme.
/// </summary>
public static class DesktopTheme
{
    /// <summary>
    /// Avalonia <c>RequestedThemeVariant</c> name that tracks System Settings.
    /// </summary>
    public const string SystemVariant = "Default";

    public static bool FollowsSystemAppearance => true;

    public static bool IsSystemFollowVariant(string? variant) =>
        string.Equals(variant, SystemVariant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(variant, "System", StringComparison.OrdinalIgnoreCase);
}
