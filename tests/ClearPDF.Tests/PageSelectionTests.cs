using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class PageSelectionTests
{
    [Fact]
    public void Click_selects_one_page_and_sets_anchor()
    {
        var set = PageSelection.Click(3, pageCount: 10);
        Assert.Equal(new[] { 3 }, set.Pages);
        Assert.Equal(3, set.Anchor);
        Assert.False(PageSelection.IsMulti(set));
    }

    [Fact]
    public void Click_clamps_out_of_range()
    {
        var set = PageSelection.Click(99, pageCount: 4);
        Assert.Equal(new[] { 3 }, set.Pages);
        Assert.Equal(3, set.Anchor);
    }

    [Fact]
    public void Click_on_empty_doc_is_empty()
    {
        var set = PageSelection.Click(0, pageCount: 0);
        Assert.Empty(set.Pages);
    }

    [Fact]
    public void Toggle_adds_then_removes_and_orders_by_index()
    {
        var set = PageSelection.Click(4, 10);
        set = PageSelection.Toggle(set, 1, 10);
        Assert.Equal(new[] { 1, 4 }, set.Pages);
        Assert.Equal(1, set.Anchor);
        Assert.True(PageSelection.IsMulti(set));

        set = PageSelection.Toggle(set, 4, 10);
        Assert.Equal(new[] { 1 }, set.Pages);
        Assert.Equal(4, set.Anchor);
        Assert.False(PageSelection.IsMulti(set));
    }

    [Fact]
    public void Toggle_can_clear_the_last_page()
    {
        var set = PageSelection.Click(2, 5);
        set = PageSelection.Toggle(set, 2, 5);
        Assert.Empty(set.Pages);
        Assert.Equal(2, set.Anchor);
    }

    [Fact]
    public void Range_is_inclusive_from_anchor_and_keeps_anchor()
    {
        var set = PageSelection.Range(anchor: 2, page: 5, pageCount: 12);
        Assert.Equal(new[] { 2, 3, 4, 5 }, set.Pages);
        Assert.Equal(2, set.Anchor);
        Assert.True(PageSelection.IsMulti(set));
    }

    [Fact]
    public void Range_works_backwards()
    {
        var set = PageSelection.Range(anchor: 6, page: 3, pageCount: 10);
        Assert.Equal(new[] { 3, 4, 5, 6 }, set.Pages);
        Assert.Equal(6, set.Anchor);
    }

    [Fact]
    public void FollowCurrent_matches_click()
    {
        var set = PageSelection.FollowCurrent(7, 9);
        Assert.Equal(new[] { 7 }, set.Pages);
        Assert.Equal(7, set.Anchor);
    }

    [Fact]
    public void Contains_is_true_only_for_selected_indices()
    {
        var set = PageSelection.Range(1, 3, 8);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(3));
        Assert.False(set.Contains(0));
        Assert.False(set.Contains(4));
    }
}
