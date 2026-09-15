using ClearPDF.Services;
using Xunit;

namespace ClearPDF.Tests;

public class RecentFilesServiceTests
{
    [Fact]
    public void FormatRelativeTime_hours_and_yesterday()
    {
        var now = new DateTime(2026, 9, 14, 17, 0, 0, DateTimeKind.Utc);
        Assert.Equal("2 hours ago", RecentFilesService.FormatRelativeTime(now.AddHours(-2), now));
        Assert.Equal("Yesterday", RecentFilesService.FormatRelativeTime(now.AddHours(-30), now));
    }

    [Fact]
    public void Add_dedupes_and_orders_most_recent_first()
    {
        var store = Path.Combine(Path.GetTempPath(), "clearpdf-recent-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var svc = new RecentFilesService(store);
            var a = Path.Combine(Path.GetTempPath(), "a-" + Guid.NewGuid().ToString("N") + ".pdf");
            var b = Path.Combine(Path.GetTempPath(), "b-" + Guid.NewGuid().ToString("N") + ".pdf");
            File.WriteAllText(a, "x");
            File.WriteAllText(b, "y");
            try
            {
                svc.Add(a);
                svc.Add(b);
                svc.Add(a);
                var list = svc.Load();
                Assert.Equal(2, list.Count);
                Assert.Equal(Path.GetFileName(a), list[0].DisplayName);
                Assert.Equal(Path.GetFileName(b), list[1].DisplayName);
            }
            finally
            {
                File.Delete(a);
                File.Delete(b);
            }
        }
        finally
        {
            if (File.Exists(store))
                File.Delete(store);
        }
    }

    [Fact]
    public void Add_caps_at_12_and_drops_oldest()
    {
        var store = Path.Combine(Path.GetTempPath(), "clearpdf-recent-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var svc = new RecentFilesService(store);
            var paths = Enumerable.Range(0, 13)
                .Select(i => Path.Combine(Path.GetTempPath(), $"clearpdf-cap-{i}-{Guid.NewGuid():N}.pdf"))
                .ToList();

            foreach (var path in paths)
                svc.Add(path);

            var list = svc.Load();
            Assert.Equal(12, list.Count);
            Assert.Equal(Path.GetFullPath(paths[12]), list[0].Path);
            Assert.Equal(Path.GetFullPath(paths[1]), list[11].Path);
            Assert.DoesNotContain(list, e =>
                string.Equals(e.Path, Path.GetFullPath(paths[0]), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(store))
                File.Delete(store);
        }
    }
}
