using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class DesktopThemeTests
{
    [Fact]
    public void Follows_system_appearance()
    {
        Assert.True(DesktopTheme.FollowsSystemAppearance);
        Assert.Equal("Default", DesktopTheme.SystemVariant);
        Assert.True(DesktopTheme.IsSystemFollowVariant("Default"));
        Assert.True(DesktopTheme.IsSystemFollowVariant("system"));
        Assert.False(DesktopTheme.IsSystemFollowVariant("Dark"));
        Assert.False(DesktopTheme.IsSystemFollowVariant("Light"));
    }
}
