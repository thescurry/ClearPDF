using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class PageRenderPlannerTests
{
    [Fact]
    public void Thumbs_are_never_rebuilt_on_zoom()
    {
        Assert.False(PageRenderPlanner.ShouldRebuildThumbsOnZoom);
        Assert.False(PageRenderPlanner.ThumbsSharePageCache);
    }

    [Fact]
    public void SharpWindow_clamps_at_document_edges()
    {
        Assert.Equal((0, 2), PageRenderPlanner.SharpWindow(currentPage: 0, pageCount: 10));
        Assert.Equal((7, 9), PageRenderPlanner.SharpWindow(currentPage: 9, pageCount: 10));
        Assert.Equal((3, 7), PageRenderPlanner.SharpWindow(currentPage: 5, pageCount: 10));
        Assert.Equal((0, -1), PageRenderPlanner.SharpWindow(0, pageCount: 0));
    }

    [Fact]
    public void BuildJobs_current_first_then_neighbors_then_previews()
    {
        var jobs = PageRenderPlanner.BuildJobs(currentPage: 5, pageCount: 10, includePreviews: true);
        Assert.Equal((5, true), jobs[0]);
        Assert.Contains(jobs, j => j is { Page: 4, Sharp: true });
        Assert.Contains(jobs, j => j is { Page: 6, Sharp: true });
        Assert.Contains(jobs, j => j is { Page: 0, Sharp: false });
        Assert.DoesNotContain(jobs, j => j is { Page: 5, Sharp: false });
        Assert.Equal(10, jobs.Count);
    }

    [Fact]
    public void BuildJobs_scroll_omits_previews()
    {
        var jobs = PageRenderPlanner.BuildJobs(currentPage: 2, pageCount: 8, includePreviews: false);
        Assert.All(jobs, j => Assert.True(j.Sharp));
        Assert.Equal(5, jobs.Count); // 0..4 around page 2
    }

    [Fact]
    public void Scroll_does_not_cancel_in_window_rasters()
    {
        Assert.False(PageRenderPlanner.ShouldCancelInFlight(
            includePreviews: false, pageIndex: 4, newCurrentPage: 5, pageCount: 20));
        Assert.True(PageRenderPlanner.ShouldCancelInFlight(
            includePreviews: false, pageIndex: 0, newCurrentPage: 5, pageCount: 20));
        Assert.True(PageRenderPlanner.ShouldCancelInFlight(
            includePreviews: true, pageIndex: 5, newCurrentPage: 5, pageCount: 20));
    }

    [Fact]
    public void Preview_does_not_replace_sharp_frame()
    {
        Assert.True(PageRenderPlanner.ShouldKeepExistingBitmap(true, alreadyShowingSharp: true));
        Assert.False(PageRenderPlanner.ShouldKeepExistingBitmap(false, alreadyShowingSharp: true));
        Assert.False(PageRenderPlanner.ShouldKeepExistingBitmap(true, alreadyShowingSharp: false));
    }

    [Fact]
    public void Debounce_and_dpi_caps_stay_in_place()
    {
        Assert.InRange(PageRenderPlanner.ZoomDebounceMs, 80, 150);
        Assert.InRange(PageRenderPlanner.ScrollRenderDebounceMs, 40, 120);
        Assert.True(PdfRenderScale.MaxEdgePixels <= 4096);
        Assert.True(PdfRenderScale.MaxPixels <= 8_000_000);
        Assert.Equal(0.05, PdfRenderScale.QuantizeStep);
    }
}
