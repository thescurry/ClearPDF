using System.IO;
using System.Text.Json;
using ClearPDF.Models;

namespace ClearPDF.Services;

/// <summary>
/// Local recent-file list (no cloud). Stored under LocalApplicationData/ClearPDF.
/// </summary>
public sealed class RecentFilesService
{
    private const int MaxEntries = 12;
    private readonly string _storePath;

    public RecentFilesService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClearPDF");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "recent.json");
    }

    // Test seam
    internal RecentFilesService(string storePath) => _storePath = storePath;

    public IReadOnlyList<RecentFileEntry> Load()
    {
        try
        {
            if (!File.Exists(_storePath))
                return Array.Empty<RecentFileEntry>();

            var json = File.ReadAllText(_storePath);
            var list = JsonSerializer.Deserialize<List<RecentFileEntry>>(json);
            return list ?? new List<RecentFileEntry>();
        }
        catch
        {
            return Array.Empty<RecentFileEntry>();
        }
    }

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var full = Path.GetFullPath(path);
        var list = Load().ToList();
        list.RemoveAll(e => string.Equals(e.Path, full, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, new RecentFileEntry
        {
            Path = full,
            DisplayName = Path.GetFileName(full),
            OpenedUtc = DateTime.UtcNow
        });

        if (list.Count > MaxEntries)
            list = list.Take(MaxEntries).ToList();

        Save(list);
    }

    public static string FormatRelativeTime(DateTime openedUtc, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var span = now - openedUtc;
        if (span.TotalMinutes < 1)
            return "Just now";
        if (span.TotalHours < 1)
            return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalHours < 24)
            return span.TotalHours < 2 ? "1 hour ago" : $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 2)
            return "Yesterday";
        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays} days ago";
        return openedUtc.ToLocalTime().ToString("MMM d");
    }

    private void Save(List<RecentFileEntry> list)
    {
        var dir = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_storePath, json);
    }
}
