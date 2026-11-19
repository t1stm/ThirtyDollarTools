using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Core.Renderer.Planes;
using Sundex.Style.DSL;

namespace Sundex.Components.Tests;

public class PanelFillStyleTests
{
    [Fact]
    public void AnAbsoluteShadeFromASheetHasAVisibleBox()
    {
        var ctx = new TestUIContext();
        var sheet = new StyleSheet(StyleParser.Parse("class shade { position = \"absolute\"; left = 0px; top = 0px; right = 0px; bottom = 0px; fill = #06060dbd; z-index = 1; }"));
        var root = new FlexPanel(ctx) { Width = 200, Height = 100 };
        var shade = new Panel(ctx);
        shade.Classes.Add("shade");
        shade.ApplyStyleSheet(sheet);
        root.AddChild(shade);
        root.DrawTo(ctx);
        root.Layout();

        var box = Assert.IsType<BoxPlane>(shade.Background);
        Assert.True(box.IsVisible);
        Assert.Equal(0.74f, box.Color.W, 2);
        Assert.Equal(200, box.Scale.X, 1);
    }
}
