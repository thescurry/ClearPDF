using System.Runtime.InteropServices;

namespace ClearPDF.Helpers;

/// <summary>
/// Path identity for open-document tabs.
/// POSIX: case-sensitive; only <c>/private/var</c>, <c>/private/tmp</c>,
/// and <c>/private/etc</c> alias to the unprefixed form. Windows-shaped
/// paths (drive letter / UNC) are compared case-insensitively even when
/// tests run on Linux.
/// </summary>
public static class DocumentPath
{
    private static readonly string[] PrivateAliases = { "/private/var", "/private/tmp", "/private/etc" };

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        return LooksWindows(path) ? NormalizeWindows(path) : NormalizePosix(path);
    }

    public static bool Same(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            var comparison = LooksWindows(left) || LooksWindows(right)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(Normalize(left), Normalize(right), comparison);
        }
        catch
        {
            return false;
        }
    }

    internal static bool LooksWindows(string path)
    {
        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
            return true;
        return path.StartsWith(@"\\", StringComparison.Ordinal)
               || path.StartsWith("//", StringComparison.Ordinal);
    }

    private static string NormalizePosix(string path)
    {
        var unix = CollapsePosix(path.Replace('\\', '/'));
        unix = StripPrivateAlias(unix);
        unix = ResolveExisting(unix, posix: true);
        return StripPrivateAlias(CollapsePosix(unix.Replace('\\', '/')));
    }

    private static string NormalizeWindows(string path)
    {
        var s = path.Replace('/', '\\');
        s = CollapseWindows(s);
        if (s.Length >= 2 && s[1] == ':')
            s = char.ToUpperInvariant(s[0]) + s[1..];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            s = ResolveExisting(s, posix: false);

        return s;
    }

    private static string StripPrivateAlias(string unix)
    {
        foreach (var prefix in PrivateAliases)
        {
            if (unix.Equals(prefix, StringComparison.Ordinal)
                || unix.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                return unix["/private".Length..];
            }
        }

        return unix;
    }

    private static string CollapsePosix(string unix)
    {
        if (string.IsNullOrEmpty(unix))
            return unix;

        var absolute = unix.StartsWith('/');
        var parts = unix.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part is "." or "")
                continue;
            if (part == "..")
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(part);
        }

        var joined = string.Join('/', stack);
        return absolute ? "/" + joined : joined;
    }

    private static string CollapseWindows(string path)
    {
        var prefix = "";
        var rest = path;
        if (path.Length >= 2 && path[1] == ':')
        {
            prefix = path[..2];
            rest = path.Length > 2 ? path[2..] : "\\";
        }
        else if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            prefix = @"\\";
            rest = path[2..];
        }

        var parts = rest.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part is "." or "")
                continue;
            if (part == "..")
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(part);
        }

        var joined = string.Join("\\", stack);
        if (prefix.Length == 2 && prefix[1] == ':')
            return prefix + "\\" + joined;
        if (prefix == @"\\")
            return prefix + joined;
        return joined;
    }

    private static string ResolveExisting(string path, bool posix)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                return path;

            var full = Path.GetFullPath(path);
            var info = new FileInfo(full);
            if (info.Exists)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target != null)
                    full = target.FullName;
            }

            return posix ? full.Replace('\\', '/') : full;
        }
        catch
        {
            return path;
        }
    }
}
