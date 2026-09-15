using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class PrintPageRangeTests
{
    [Fact]
    public void Selection_is_the_current_left_rail_page()
    {
        var (start, end) = PrintPageRange.Resolve(
            PrintRangeMode.Selection, currentPage0: 4, pageCount: 12);
        Assert.Equal(4, start);
        Assert.Equal(4, end);
    }

    [Fact]
    public void Selection_clamps_out_of_range_current_page()
    {
        var (start, end) = PrintPageRange.Resolve(
            PrintRangeMode.Selection, currentPage0: 99, pageCount: 3);
        Assert.Equal(2, start);
        Assert.Equal(2, end);
    }

    [Fact]
    public void AllPages_spans_the_document()
    {
        var (start, end) = PrintPageRange.Resolve(
            PrintRangeMode.AllPages, currentPage0: 4, pageCount: 8);
        Assert.Equal(0, start);
        Assert.Equal(7, end);
    }

    [Fact]
    public void UserPages_maps_1_based_dialog_range()
    {
        var (start, end) = PrintPageRange.Resolve(
            PrintRangeMode.UserPages, currentPage0: 0, pageCount: 10,
            userFrom1Based: 3, userTo1Based: 5);
        Assert.Equal(2, start);
        Assert.Equal(4, end);
    }

    [Fact]
    public void UserPages_raises_inverted_end_to_start()
    {
        var (start, end) = PrintPageRange.Resolve(
            PrintRangeMode.UserPages, currentPage0: 0, pageCount: 10,
            userFrom1Based: 8, userTo1Based: 2);
        // from 8 → 7, to 2 → 1, then end < start → end = start
        Assert.Equal(7, start);
        Assert.Equal(7, end);
    }

    [Fact]
    public void Empty_document_returns_empty_span()
    {
        var (start, end) = PrintPageRange.Resolve(PrintRangeMode.Selection, 0, 0);
        Assert.Equal(0, start);
        Assert.Equal(-1, end);
    }

    [Fact]
    public void ResolvePages_Selection_size_1_is_current_page()
    {
        var pages = PrintPageRange.ResolvePages(
            PrintRangeMode.Selection, currentPage0: 4, pageCount: 12,
            selectedPages0: new[] { 4 });
        Assert.Equal(new[] { 4 }, pages);
    }

    [Fact]
    public void ResolvePages_Selection_size_1_uses_current_not_stale_thumb()
    {
        // Scroll may move current while a lone thumb stays — size 1 = current.
        var pages = PrintPageRange.ResolvePages(
            PrintRangeMode.Selection, currentPage0: 7, pageCount: 12,
            selectedPages0: new[] { 2 });
        Assert.Equal(new[] { 7 }, pages);
    }

    [Fact]
    public void ResolvePages_Selection_multi_is_exact_ordered_set()
    {
        var pages = PrintPageRange.ResolvePages(
            PrintRangeMode.Selection, currentPage0: 0, pageCount: 12,
            selectedPages0: new[] { 8, 1, 3 });
        Assert.Equal(new[] { 1, 3, 8 }, pages);
    }

    [Fact]
    public void ResolvePages_UserPages_stays_contiguous()
    {
        var pages = PrintPageRange.ResolvePages(
            PrintRangeMode.UserPages, currentPage0: 0, pageCount: 10,
            selectedPages0: new[] { 0, 8 },
            userFrom1Based: 3, userTo1Based: 5);
        Assert.Equal(new[] { 2, 3, 4 }, pages);
    }

    [Fact]
    public void DialogPageRange_prefills_min_max_of_selection()
    {
        var (from, to) = PrintPageRange.DialogPageRange(new[] { 8, 1, 3 }, 0, 12);
        Assert.Equal(2, from);
        Assert.Equal(9, to);
    }

    [Fact]
    public void ResolvePages_AllPages_and_empty_doc()
    {
        Assert.Equal(new[] { 0, 1, 2 },
            PrintPageRange.ResolvePages(PrintRangeMode.AllPages, 1, 3));
        Assert.Empty(PrintPageRange.ResolvePages(PrintRangeMode.Selection, 0, 0));
    }
}
