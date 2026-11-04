using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClearPDF.Mac.Views;
using ClearPDF.Models;
using ClearPDF.Services;
using Xunit;

namespace ClearPDF.Mac.Tests;

public class RecentListTests
{
    [AvaloniaFact]
    public void Empty_recent_list_caps_at_12_after_adding_13()
    {
        var store = NewStorePath();
        var svc = new RecentFilesService(store);
        for (var i = 0; i < 13; i++)
            svc.Add($"/docs/recent-{i:00}-file.pdf");

        MacWindowHarness.WithWindow(window =>
        {
            window.RefreshRecentListForTests();
            Pump();

            Assert.Equal(12, svc.Load().Count);
            Assert.Equal(12, window.RecentRowCount);
            Assert.True(window.EmptyViewIsVisible);
            Assert.DoesNotContain(
                svc.Load(),
                e => e.DisplayName == "recent-00-file.pdf");
            Assert.Equal("recent-12-file.pdf", svc.Load()[0].DisplayName);
        }, svc);
    }

    [AvaloniaFact]
    public void Long_filename_and_timestamp_are_not_clipped()
    {
        const string longName =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ-very-long-clearpdf-recent-name-that-must-keep-its-leading-characters.pdf";
        var store = NewStorePath();
        var opened = DateTime.UtcNow.AddMinutes(-15);
        var entries = new[]
        {
            new RecentFileEntry
            {
                Path = "/docs/" + longName,
                DisplayName = longName,
                OpenedUtc = opened
            }
        };
        File.WriteAllText(store, JsonSerializer.Serialize(entries));
        MacWindowHarness.WithWindow(window =>
        {
            window.RefreshRecentListForTests();
            window.InvalidateMeasure();
            window.InvalidateArrange();
            window.UpdateLayout();
            Pump();

            Assert.Equal(1, window.RecentRowCount);
            var name = FindText(window.RecentListControl, t => t.Text == longName);
            var allowedTimes = new[]
            {
                RecentFilesService.FormatRelativeTime(opened, DateTime.UtcNow.AddMinutes(-1)),
                RecentFilesService.FormatRelativeTime(opened, DateTime.UtcNow),
                RecentFilesService.FormatRelativeTime(opened, DateTime.UtcNow.AddMinutes(1))
            };
            var time = FindText(window.RecentListControl, t => t.Text != null && allowedTimes.Contains(t.Text));

            Assert.NotNull(name);
            Assert.NotNull(time);
            Assert.True(
                name!.TextAlignment is TextAlignment.Left or TextAlignment.Start,
                $"filename alignment {name.TextAlignment} is not left");
            Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
            Assert.Equal(TextWrapping.NoWrap, name.TextWrapping);
            Assert.StartsWith("ABCDEFGHIJKLMNOPQRSTUVWXYZ", name.Text);

            Assert.Equal(TextTrimming.None, time!.TextTrimming);
            Assert.Equal(TextWrapping.NoWrap, time.TextWrapping);
            var timeText = new FormattedText(
                time.Text ?? string.Empty,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(time.FontFamily, time.FontStyle, time.FontWeight),
                time.FontSize,
                time.Foreground);
            Assert.True(
                timeText.Width <= time.Bounds.Width + 1.0,
                $"timestamp clipped: text {timeText.Width} > bounds {time.Bounds.Width}");
            Assert.True(time.Bounds.Width >= 112, $"timestamp column {time.Bounds.Width} < 112");

            var row = name.FindAncestorOfType<Grid>();
            Assert.NotNull(row);
            Assert.True(name.Bounds.Left >= row!.Bounds.Left - 0.5, "filename leading edge is clipped");
            var nameText = new FormattedText(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(name.FontFamily, name.FontStyle, name.FontWeight),
                name.FontSize,
                name.Foreground);
            Assert.True(
                nameText.Width <= name.Bounds.Width + 1.0,
                "filename leading characters do not fit in the arranged width");
        }, new RecentFilesService(store));
    }

    private static string NewStorePath() =>
        Path.Combine(Path.GetTempPath(), "clearpdf-recent-ui-" + Guid.NewGuid().ToString("N") + ".json");

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private static TextBlock? FindText(Visual root, Func<TextBlock, bool> match) =>
        root.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(match);
}
