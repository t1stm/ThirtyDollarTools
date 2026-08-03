using EditorScene.Scenes.Components;
using OpenTK.Mathematics;

namespace EditorScene.Tests;

public class ViewNavigationTests
{
    private static ViewNavigation MakeArrangementNav()
    {
        return new ViewNavigation(6f, 96f) { Zoom = 24f };
    }

    [Fact]
    public void Wheel_PanXWithY_PansTimeInsteadOfScrollingY()
    {
        var nav = MakeArrangementNav();

        nav.Wheel(new Vector2(0, -5), false, true, 0);

        Assert.Equal(0f, nav.ScrollY);
        Assert.Equal(240f, nav.ScrollX);
    }

    [Fact]
    public void MiddlePan_WithZeroMaxScrollY_PinsYDuringADiagonalDrag()
    {
        var nav = MakeArrangementNav();
        nav.MiddlePan(true, 100f, 100f, true); // starts the pan

        var changed = nav.MiddlePan(true, 80f, 130f, true); // diagonal drag

        Assert.True(changed);
        Assert.Equal(20f, nav.ScrollX); // 100 - 80
        Assert.Equal(0f, nav.ScrollY); // MaxScrollY = 0 pins it
    }

    [Fact]
    public void ZoomAt_ClampsToTheArrangementZoomRange()
    {
        var nav = MakeArrangementNav();

        nav.ZoomAt(0f, 100f);
        Assert.Equal(96f, nav.Zoom);

        nav.ZoomAt(0f, -100f);
        Assert.Equal(6f, nav.Zoom);
    }
}