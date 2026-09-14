namespace ClearPDF.Models;

public sealed class RecentFileEntry
{
    public string Path { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTime OpenedUtc { get; init; }
}
