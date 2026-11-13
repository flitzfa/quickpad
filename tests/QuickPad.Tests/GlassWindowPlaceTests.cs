using System.Windows;
using QuickPad.UI;

namespace QuickPad.Tests;

public class GlassWindowPlaceTests
{
    private static readonly Rect Work = new(0, 0, 1920, 1040);
    private static readonly Size Window = new(520, 300);

    [Fact]
    public void Goes_right_of_the_anchor_growing_from_its_top_left_corner()
    {
        var (topLeft, origin) = GlassWindow.Place(new Rect(100, 200, 300, 24), Window, Work);
        Assert.Equal(new Point(408, 200), topLeft);
        Assert.Equal(new Point(0, 0), origin);
    }

    [Fact]
    public void Goes_left_when_the_right_side_does_not_fit()
    {
        var (topLeft, origin) = GlassWindow.Place(new Rect(1500, 200, 300, 24), Window, Work);
        Assert.Equal(new Point(1500 - 8 - 520, 200), topLeft);
        Assert.Equal(1, origin.X);
    }

    [Fact]
    public void Stays_inside_the_work_area_near_the_bottom()
    {
        var (topLeft, origin) = GlassWindow.Place(new Rect(100, 1000, 300, 24), Window, Work);
        Assert.Equal(1040 - 300, topLeft.Y);
        Assert.Equal(new Point(0, 1), origin);
    }
}
