using System.Diagnostics.CodeAnalysis;
using OpenTK.Mathematics;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Bars;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Abstract;

namespace Sundex.Components.Tests;

/// <summary>
///     The render queue is retained, so <see cref="UIElement.Visible" /> has to maintain it:
///     a renderable set via a Panel's <c>Background</c> queues at whatever
///     <see cref="UIElement.Index" /> the element had at construction time, and attaching it
///     into its real parent later updates <c>Index</c> without re-queueing (<c>Layout()</c>,
///     run every frame, never touches the queue). The <c>Visible</c> setter therefore calls
///     <see cref="UIElement.DrawTo" />/<c>StopRendering</c> itself - covering both the editor's
///     status bar (built hidden, shown later) and rows whose visibility flips during layout,
///     like the arrangement's M/S toggles when it scrolls.
/// </summary>
public class ProgressBarVisibilityToggleTests
{
    [Fact]
    public void ProgressBar_ConstructedInvisible_ThenParented_ThenShown_EndsUpAtCorrectLayer()
    {
        var context = new TestContext();

        // Mimic a nested tree several levels deep, like InspectorPanel's real structure.
        var root = new Panel(context) { Width = 300, Height = 600 };
        var mid1 = new Panel(context)
            { Width = LiteralOrComputable.Percent(100), Height = LiteralOrComputable.Percent(100) };
        var mid2 = new FlexPanel(context)
            { Width = LiteralOrComputable.Percent(100), Height = LiteralOrComputable.Percent(100) };
        root.AddChild(mid1);
        mid1.AddChild(mid2);
        root.DrawTo(context); // root is now "Drawn" - mirrors _inspectorColumn already being live

        var bar = new ProgressBar(context, new ColoredPlane { Color = Vector4.One },
            new ColoredPlane { Color = Vector4.UnitX })
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 6,
            Visible = false // constructed hidden, like the idle status bar
        };
        var plane = bar.BackgroundPanel.Background!;

        // Attach into the live, already-drawn tree - mirrors mid2.Children = [..., bar].
        mid2.AddChild(bar);

        var expectedLayer = bar.BackgroundPanel.Index;
        Assert.Equal(-1, context.LayerOf(plane)); // hidden: not queued at all

        // Showing it re-queues the subtree at the current (correct) Index.
        bar.Visible = true;
        Assert.Equal(expectedLayer, context.LayerOf(plane));

        // Hiding it dequeues again, instead of leaving it rendered forever.
        bar.Visible = false;
        Assert.Equal(-1, context.LayerOf(plane));
    }

    private class TestContext : UIContext
    {
        [SetsRequiredMembers]
        public TestContext()
        {
            Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
        }

        public List<List<IRenderable>> Q => LayeredRenderQueue;

        public int LayerOf(IRenderable r)
        {
            for (var i = 0; i < Q.Count; i++)
                if (Q[i].Any(x => ReferenceEquals(x, r)))
                    return i;
            return -1;
        }
    }
}