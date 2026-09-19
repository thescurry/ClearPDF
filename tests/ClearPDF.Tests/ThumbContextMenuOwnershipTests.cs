using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class ThumbContextMenuOwnershipTests
{
    [Fact]
    public void One_menu_must_not_be_shared_across_thumbs()
    {
        Assert.False(ThumbContextMenuOwnership.CanShareOneInstanceAcrossThumbs);
        Assert.Equal(12, ThumbContextMenuOwnership.InstanceCountForThumbs(12));
        Assert.NotEqual(1, ThumbContextMenuOwnership.InstanceCountForThumbs(12));
        Assert.Equal(0, ThumbContextMenuOwnership.InstanceCountForThumbs(0));
        Assert.Equal(0, ThumbContextMenuOwnership.InstanceCountForThumbs(-3));
    }

    [Fact]
    public void Shared_child_is_rejected_on_a_second_thumb()
    {
        var parents = new Dictionary<object, object>();
        var sharedMenu = new object();

        Assert.True(ThumbContextMenuOwnership.TryClaimParent(parents, sharedMenu, "thumb-0"));
        Assert.False(ThumbContextMenuOwnership.TryClaimParent(parents, sharedMenu, "thumb-1"));
    }

    [Fact]
    public void Per_row_menus_attach_to_every_thumb()
    {
        var parents = new Dictionary<object, object>();
        var n = ThumbContextMenuOwnership.InstanceCountForThumbs(5);

        for (var i = 0; i < n; i++)
        {
            Assert.True(ThumbContextMenuOwnership.TryClaimParent(
                parents, child: new object(), parent: $"thumb-{i}"));
        }

        Assert.Equal(n, parents.Count);
    }

    [Fact]
    public void Reopening_on_the_same_thumb_is_allowed()
    {
        var parents = new Dictionary<object, object>();
        var menu = new object();
        Assert.True(ThumbContextMenuOwnership.TryClaimParent(parents, menu, "thumb-0"));
        Assert.True(ThumbContextMenuOwnership.TryClaimParent(parents, menu, "thumb-0"));
    }
}
