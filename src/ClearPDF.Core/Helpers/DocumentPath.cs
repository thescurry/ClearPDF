namespace ClearPDF.Helpers;

/// <summary>
/// Path identity for open-document tabs. macOS often aliases
/// <c>/var/...</c> and <c>/private/var/...</c> as the same file.
/// </summary>
public static class DocumentPath
{
    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        const string prefix = "/private";
        if (full.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            return full[prefix.Length..];
        return full;
    }

    public static bool Same(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
