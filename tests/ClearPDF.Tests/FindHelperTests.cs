using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class FindHelperTests
{
    [Fact]
    public void FindAll_is_case_insensitive_across_pages()
    {
        var pages = new[] { "Hello World", "hello again", "nothing" };
        var hits = FindHelper.FindAll(pages, "HELLO");
        Assert.Equal(2, hits.Count);
        Assert.Equal(0, hits[0].PageIndex);
        Assert.Equal(1, hits[1].PageIndex);
    }

    [Fact]
    public void FindAll_empty_query_returns_none()
    {
        Assert.Empty(FindHelper.FindAll(new[] { "abc" }, ""));
    }

    [Fact]
    public void NextIndex_wraps()
    {
        Assert.Equal(-1, FindHelper.NextIndex(0, -1));
        Assert.Equal(0, FindHelper.NextIndex(3, -1));
        Assert.Equal(1, FindHelper.NextIndex(3, 0));
        Assert.Equal(0, FindHelper.NextIndex(3, 2));
    }

    [Fact]
    public void PrevIndex_wraps()
    {
        Assert.Equal(-1, FindHelper.PrevIndex(0, 0));
        Assert.Equal(2, FindHelper.PrevIndex(3, 0));
        Assert.Equal(1, FindHelper.PrevIndex(3, 2));
    }
}
