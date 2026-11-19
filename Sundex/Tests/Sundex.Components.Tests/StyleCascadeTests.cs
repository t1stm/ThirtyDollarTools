using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Style.DSL;

namespace Sundex.Components.Tests;

/// <summary>Re-styling in the fork: classes that come off undo themselves, code wins, media blocks follow the viewport.</summary>
public class StyleCascadeTests
{
    private static StyleSheet Sheet(string source) => new(StyleParser.Parse(source));

    [Fact]
    public void RemovingAClassUndoesWhatItSet()
    {
        var context = new TestUIContext();
        var panel = new FlexPanel(context) { Classes = ["row"] };
        panel.ApplyStyleSheet(Sheet("class row { gap = 4px; } class wide { padding = 20px; gap = 8px; }"));
        Assert.Equal(4, panel.Gap);

        panel.SetClass("wide", true);
        Assert.Equal(20, panel.PaddingTop);
        Assert.Equal(8, panel.Gap);

        panel.SetClass("wide", false);
        Assert.Equal(0, panel.PaddingTop);
        Assert.Equal(4, panel.Gap);
    }

    [Fact]
    public void AValueSetByCodeOutlivesARestyle()
    {
        var context = new TestUIContext();
        var panel = new FlexPanel(context) { Classes = ["row"] };
        panel.ApplyStyleSheet(Sheet("class row { gap = 4px; } class active { opacity = 0.5; }"));

        panel.Gap = 12;
        panel.SetClass("active", true);

        Assert.Equal(12, panel.Gap);
        Assert.Equal(0.5f, panel.Opacity);
    }

    [Fact]
    public void AMediaBlockAppliesWhileTheViewportMatchesAndUndoesWhenItStops()
    {
        var context = new TestUIContext();
        var panel = new FlexPanel(context) { Classes = ["page"] };
        context.Camera.Width = 400;
        panel.ApplyStyleSheet(Sheet("class page { padding = 12px; media[min-width = 640px] = { padding = 24px; }; }"));
        Assert.Equal(12, panel.PaddingTop);

        context.Camera.Width = 1280;
        context.RefreshMedia();
        Assert.Equal(24, panel.PaddingTop);

        context.Camera.Width = 400;
        context.RefreshMedia();
        Assert.Equal(12, panel.PaddingTop);
    }

    [Fact]
    public void AMediaBlockOnlySetsWhatItNames()
    {
        var context = new TestUIContext();
        var panel = new FlexPanel(context) { Classes = ["page"] };
        context.Camera.Width = 1280;
        panel.ApplyStyleSheet(Sheet(
            "class page { gap = 3px; media[max-width = 639px] = { padding = 8px; }; }"));
        Assert.Equal(0, panel.PaddingTop);

        context.Camera.Width = 390;
        context.RefreshMedia();
        Assert.Equal(8, panel.PaddingTop);
        Assert.Equal(3, panel.Gap);
    }

    [Fact]
    public void TogglingAClassWhileHoveredDoesNotTakeTheHoverForCode()
    {
        var context = new TestUIContext();
        var panel = new FlexPanel(context) { Classes = ["card"] };
        panel.ApplyStyleSheet(Sheet(
            "class card { opacity = 1; state[hovered] = { opacity = 0.8; }; } class picked { gap = 2px; }"));

        panel.IsHovered = true;
        panel.SyncPointerState();
        Assert.Equal(0.8f, panel.Opacity, 3);

        panel.SetClass("picked", true);
        panel.IsHovered = false;
        panel.SyncPointerState();

        Assert.Equal(1f, panel.Opacity, 3);
        Assert.Equal(2, panel.Gap);
    }
}
