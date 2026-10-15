using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class MacShortcutsTests
{
    [Theory]
    [InlineData("O", MacShortcuts.Command.Open)]
    [InlineData("S", MacShortcuts.Command.SaveAs)]
    [InlineData("P", MacShortcuts.Command.Print)]
    [InlineData("W", MacShortcuts.Command.Close)]
    [InlineData("+", MacShortcuts.Command.ZoomIn)]
    [InlineData("OemPlus", MacShortcuts.Command.ZoomIn)]
    [InlineData("-", MacShortcuts.Command.ZoomOut)]
    [InlineData("OemMinus", MacShortcuts.Command.ZoomOut)]
    [InlineData("0", MacShortcuts.Command.ActualSize)]
    [InlineData("D0", MacShortcuts.Command.ActualSize)]
    public void Meta_letter_maps_to_command(string key, MacShortcuts.Command expected)
    {
        Assert.Equal(expected, MacShortcuts.TryMatch(meta: true, shift: false, alt: false, key));
    }

    [Fact]
    public void Without_meta_is_not_a_shortcut()
    {
        Assert.Null(MacShortcuts.TryMatch(meta: false, shift: false, alt: false, "O"));
    }

    [Fact]
    public void Alt_chords_are_ignored()
    {
        Assert.Null(MacShortcuts.TryMatch(meta: true, shift: false, alt: true, "O"));
    }

    [Fact]
    public void Scanner_is_not_a_command()
    {
        foreach (var name in Enum.GetNames<MacShortcuts.Command>())
            Assert.False(name.Contains("Scan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_command_is_defined()
    {
        foreach (var command in Enum.GetValues<MacShortcuts.Command>())
            Assert.True(MacShortcuts.IsDefined(command));
    }
}
