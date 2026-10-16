using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanSessionTests
{
    [Fact]
    public void Add_and_AddRange_preserve_order()
    {
        var session = new ScanSession();
        var a = ScanTestPages.Solid(8, 10, 150, tag: 1);
        var b = ScanTestPages.Solid(8, 10, 150, tag: 2);
        var c = ScanTestPages.Solid(8, 10, 150, tag: 3);

        session.Add(a);
        session.AddRange(new[] { b, c });

        Assert.Equal(new[] { 1, 2, 3 }, session.Pages.Select(ScanTestPages.Tag));
    }

    [Fact]
    public void Delete_removes_by_index()
    {
        var session = Session(1, 2, 3);
        session.Delete(1);
        Assert.Equal(new[] { 1, 3 }, session.Pages.Select(ScanTestPages.Tag));
    }

    [Fact]
    public void Delete_out_of_range_throws()
    {
        var session = Session(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Delete(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Delete(1));
    }

    [Fact]
    public void Move_reorders_to_the_destination_index()
    {
        var session = Session(1, 2, 3, 4);
        session.Move(fromIndex: 3, toIndex: 0);
        Assert.Equal(new[] { 4, 1, 2, 3 }, session.Pages.Select(ScanTestPages.Tag));

        session.Move(fromIndex: 1, toIndex: 3);
        Assert.Equal(new[] { 4, 2, 3, 1 }, session.Pages.Select(ScanTestPages.Tag));
    }

    [Fact]
    public void MoveUp_and_MoveDown_swap_neighbors()
    {
        var session = Session(1, 2, 3);
        session.MoveDown(0);
        Assert.Equal(new[] { 2, 1, 3 }, session.Pages.Select(ScanTestPages.Tag));
        session.MoveUp(2);
        Assert.Equal(new[] { 2, 3, 1 }, session.Pages.Select(ScanTestPages.Tag));
    }

    [Fact]
    public void MoveUp_at_start_and_MoveDown_at_end_are_noops()
    {
        var session = Session(1, 2);
        session.MoveUp(0);
        session.MoveDown(1);
        Assert.Equal(new[] { 1, 2 }, session.Pages.Select(ScanTestPages.Tag));
    }

    [Fact]
    public void Rotate_turns_in_90_degree_steps_and_wraps()
    {
        var session = Session(1);
        session.Rotate(0, 90);
        Assert.Equal(90, session.Pages[0].RotationDegrees);
        session.Rotate(0, 90);
        session.Rotate(0, 90);
        session.Rotate(0, 90);
        Assert.Equal(0, session.Pages[0].RotationDegrees);
        session.Rotate(0, -90);
        Assert.Equal(270, session.Pages[0].RotationDegrees);
    }

    [Fact]
    public void Clear_empties_the_session()
    {
        var session = Session(1, 2);
        session.Clear();
        Assert.Empty(session.Pages);
    }

    private static ScanSession Session(params int[] tags)
    {
        var session = new ScanSession();
        session.AddRange(tags.Select(t => ScanTestPages.Solid(4, 6, 150, tag: t)));
        return session;
    }
}
