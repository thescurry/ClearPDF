namespace ClearPDF.Helpers;

/// <summary>
/// Path identity for open-document tabs. macOS often aliases
/// <c>/var/...</c> and <c>/private/var/...</c> as the same file.
/// POSIX absolute paths are compared lexically so Windows
/// <see cref="Path.GetFullPath(string)"/> cannot map them onto
/// different drive roots (that broke installer CI).
/// </summary>
public static class DocumentPath
{
    public static string Normalize(string path)
    {
        var unix = path.Replace('\\', '/');
        if (unix.StartsWith("/private/", StringComparison.OrdinalIgnoreCase))
            unix = unix["/private".Length..];

        if (unix.StartsWith('/'))
            return unix;

        try
        {
            var full = Path.GetFullPath(path).Replace('\\', '/');
            if (full.StartsWith("/private/", StringComparison.OrdinalIgnoreCase))
                return full["/private".Length..];
            return full;
        }
        catch
        {
            return unix;
        }
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
