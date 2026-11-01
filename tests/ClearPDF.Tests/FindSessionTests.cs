using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class FindKeysTests
{
    [Theory]
    [InlineData("Enter", false, false, FindAction.CommitNext)]
    [InlineData("Return", false, false, FindAction.CommitNext)]
    [InlineData("Enter", true, false, FindAction.Previous)]
    [InlineData("Return", true, false, FindAction.Previous)]
    [InlineData("G", false, true, FindAction.Next)]
    [InlineData("G", true, true, FindAction.Previous)]
    [InlineData("F", false, true, FindAction.Toggle)]
    [InlineData("Escape", false, false, FindAction.Close)]
    [InlineData("Esc", false, false, FindAction.Close)]
    [InlineData("A", false, false, FindAction.None)]
    [InlineData("Enter", false, true, FindAction.None)]
    public void Resolve_mac_find_gestures(string key, bool shift, bool command, FindAction expected)
    {
        Assert.Equal(expected, FindKeys.Resolve(key, shift, command));
    }

    [Fact]
    public void Return_is_treated_as_Enter_because_Avalonia_macOS_reports_Return()
    {
        Assert.Equal(FindKeys.Resolve("Enter", false, false), FindKeys.Resolve("Return", false, false));
        Assert.Equal(FindKeys.Resolve("Enter", true, false), FindKeys.Resolve("Return", true, false));
    }
}

public class FindSessionTests
{
    private static readonly string[] Pages = { "alpha hello", "hello beta hello", "nothing" };

    [Fact]
    public void Commit_then_next_highlights_first_hit_and_shows_count()
    {
        var find = new FindSession();
        find.Toggle();
        find.Commit(Pages, "HELLO");

        Assert.True(find.IsOpen);
        Assert.Equal(3, find.Hits.Count);
        Assert.Equal(0, find.CurrentIndex);
        Assert.Equal(0, find.CurrentPage);
        Assert.Equal("1 / 3", find.StatusText);
    }

    [Fact]
    public void Enter_then_enter_advances_across_pages()
    {
        var find = new FindSession();
        find.Toggle();
        find.Commit(Pages, "hello");
        find.Next(Pages, "hello");
        Assert.Equal(1, find.CurrentIndex);
        Assert.Equal(1, find.CurrentPage);
        find.Next(Pages, "hello");
        Assert.Equal(2, find.CurrentIndex);
        Assert.Equal(1, find.CurrentPage);
        find.Next(Pages, "hello");
        Assert.Equal(0, find.CurrentIndex);
        Assert.Equal(0, find.CurrentPage);
    }

    [Fact]
    public void Previous_wraps_to_last_hit()
    {
        var find = new FindSession();
        find.Toggle();
        find.Commit(Pages, "hello");
        find.Previous(Pages, "hello");
        Assert.Equal(2, find.CurrentIndex);
        Assert.Equal("3 / 3", find.StatusText);
    }

    [Fact]
    public void Empty_query_is_no_matches()
    {
        var find = new FindSession();
        find.Toggle();
        find.Commit(Pages, "   ");
        Assert.Empty(find.Hits);
        Assert.Equal(-1, find.CurrentIndex);
        Assert.Equal(-1, find.CurrentPage);
        Assert.Equal("No matches", find.StatusText);
    }

    [Fact]
    public void Esc_closes_and_clears_highlight_but_keeps_query()
    {
        var find = new FindSession();
        find.Toggle();
        find.Commit(Pages, "hello");
        find.Close();

        Assert.False(find.IsOpen);
        Assert.Equal(-1, find.CurrentIndex);
        Assert.Equal(-1, find.CurrentPage);
        Assert.Equal("hello", find.Query);
        Assert.Equal(string.Empty, find.StatusText);
    }

    [Fact]
    public void Toggle_reopens_with_previous_query()
    {
        var find = new FindSession();
        find.Toggle();
        find.Commit(Pages, "beta");
        find.Close();
        find.Toggle();

        Assert.True(find.IsOpen);
        Assert.Equal("beta", find.Query);
    }
}
