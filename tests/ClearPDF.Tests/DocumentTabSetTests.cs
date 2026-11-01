using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class TabKeysTests
{
    [Theory]
    [InlineData("W", false, true, TabAction.CloseCurrent)]
    [InlineData("W", true, true, TabAction.None)]
    [InlineData("OemOpenBrackets", true, true, TabAction.Previous)]
    [InlineData("Oem4", true, true, TabAction.Previous)]
    [InlineData("[", true, true, TabAction.Previous)]
    [InlineData("OemCloseBrackets", true, true, TabAction.Next)]
    [InlineData("Oem6", true, true, TabAction.Next)]
    [InlineData("]", true, true, TabAction.Next)]
    [InlineData("OemOpenBrackets", false, true, TabAction.None)]
    [InlineData("W", false, false, TabAction.None)]
    public void Resolve_mac_tab_gestures(string key, bool shift, bool command, TabAction expected)
    {
        Assert.Equal(expected, TabKeys.Resolve(key, shift, command));
    }
}

public class DocumentPathTests
{
    [Fact]
    public void Same_treats_private_var_as_var()
    {
        Assert.True(DocumentPath.Same("/var/tmp/a.pdf", "/private/var/tmp/a.pdf"));
        Assert.True(DocumentPath.Same("/Private/var/tmp/A.PDF", "/var/tmp/a.pdf"));
        Assert.False(DocumentPath.Same("/var/tmp/a.pdf", "/var/tmp/b.pdf"));
    }
}

public class DocumentTabSetTests
{
    [Fact]
    public void Open_adds_a_tab_and_selects_it()
    {
        var tabs = new DocumentTabSet();
        var result = tabs.Open("/docs/one.pdf");

        Assert.False(result.AlreadyOpen);
        Assert.Equal(1, tabs.Count);
        Assert.Same(result.Tab, tabs.Current);
        Assert.Equal("one.pdf", result.Tab.Title);
        Assert.False(result.Tab.Find.IsOpen);
    }

    [Fact]
    public void Open_same_path_switches_instead_of_duplicating()
    {
        var tabs = new DocumentTabSet();
        var first = tabs.Open("/docs/one.pdf");
        tabs.Open("/docs/two.pdf");
        var again = tabs.Open("/docs/one.pdf");

        Assert.True(again.AlreadyOpen);
        Assert.Equal(2, tabs.Count);
        Assert.Equal(first.Tab.Id, tabs.Current!.Id);
        Assert.Equal(0, tabs.CurrentIndex);
    }

    [Fact]
    public void Open_dedupes_macos_private_var_aliases()
    {
        var tabs = new DocumentTabSet();
        tabs.Open("/var/tmp/doc.pdf");
        var again = tabs.Open("/private/var/tmp/doc.pdf");

        Assert.True(again.AlreadyOpen);
        Assert.Equal(1, tabs.Count);
    }

    [Fact]
    public void Close_current_selects_a_neighbor()
    {
        var tabs = new DocumentTabSet();
        tabs.Open("/a.pdf");
        var b = tabs.Open("/b.pdf");
        tabs.Open("/c.pdf");
        Assert.True(tabs.Select(b.Tab.Id));
        Assert.True(tabs.CloseCurrent());

        Assert.Equal(2, tabs.Count);
        Assert.Equal("c.pdf", tabs.Current!.Title);
    }

    [Fact]
    public void Close_last_tab_clears_current()
    {
        var tabs = new DocumentTabSet();
        tabs.Open("/only.pdf");
        Assert.True(tabs.CloseCurrent());
        Assert.Equal(0, tabs.Count);
        Assert.Null(tabs.Current);
        Assert.False(tabs.CloseCurrent());
    }

    [Fact]
    public void Switch_next_and_previous_wrap()
    {
        var tabs = new DocumentTabSet();
        tabs.Open("/a.pdf");
        tabs.Open("/b.pdf");
        tabs.Open("/c.pdf");

        Assert.True(tabs.SelectNext());
        Assert.Equal("a.pdf", tabs.Current!.Title);
        Assert.True(tabs.SelectPrevious());
        Assert.Equal("c.pdf", tabs.Current.Title);
        Assert.True(tabs.SelectPrevious());
        Assert.Equal("b.pdf", tabs.Current.Title);
    }

    [Fact]
    public void Find_state_is_per_tab()
    {
        var tabs = new DocumentTabSet();
        var one = tabs.Open("/one.pdf");
        one.Tab.Find.Toggle();
        one.Tab.Find.Commit(new[] { "alpha hello" }, "hello");

        var two = tabs.Open("/two.pdf");
        two.Tab.Find.Toggle();
        two.Tab.Find.Commit(new[] { "unique term" }, "unique");

        tabs.Select(one.Tab.Id);
        Assert.Equal("hello", tabs.Current!.Find.Query);
        Assert.Equal("1 / 1", tabs.Current.Find.StatusText);

        tabs.Select(two.Tab.Id);
        Assert.Equal("unique", tabs.Current!.Find.Query);
        Assert.Equal("1 / 1", tabs.Current.Find.StatusText);
        Assert.NotEqual(one.Tab.Find, two.Tab.Find);
    }
}
