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
///     Documents a real gotcha hit by the editor's status bar (a <see cref="ProgressBar" />
///     constructed hidden, then shown later): a renderable set via a Panel's
///     <c>Background</c> property queues immediately, at whatever <see cref="UIElement.Index" />
///     the element has at that construction moment — regardless of <see cref="UIElement.Visible" />.
///     Attaching the element into its real parent later updates <c>Index</c> but never re-queues
///     the already-queued renderable, and flipping <c>Visible</c> back to true doesn't either
///     (<c>Layout()</c>, run every frame, never touches the render queue). Only a fresh
///     <see cref="UIElement.DrawTo" /> call re-queues at the current, correct layer. Any element
///     built hidden and shown later needs an explicit <c>DrawTo</c> when it becomes visible, or it
///     stays rendered at its stale construction-time depth — effectively invisible if anything
///     else covers that layer.
/// </summary>
public class ProgressBarVisibilityToggleTests
{
    private class TestContext : UIContext
    {
        [SetsRequiredMembers]
        public TestContext() { Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080)); }
        public List<List<IRenderable>> Q => LayeredRenderQueue;

        public int LayerOf(IRenderable r)
        {
            for (var i = 0; i < Q.Count; i++)
                if (Q[i].Any(x => ReferenceEquals(x, r)))
                    return i;
            return -1;
        }
    }

    [Fact]
    public void ProgressBar_ConstructedInvisible_ThenParented_ThenShown_EndsUpAtCorrectLayer()
    {
        var context = new TestContext();

        // Mimic a nested tree several levels deep, like InspectorPanel's real structure.
        var root = new Panel(context) { Width = 300, Height = 600 };
        var mid1 = new Panel(context) { Width = LiteralOrComputable.Percent(100), Height = LiteralOrComputable.Percent(100) };
        var mid2 = new FlexPanel(context) { Width = LiteralOrComputable.Percent(100), Height = LiteralOrComputable.Percent(100) };
        root.AddChild(mid1);
        mid1.AddChild(mid2);
        root.DrawTo(context); // root is now "Drawn" — mirrors _inspectorColumn already being live

        var bar = new ProgressBar(context, new ColoredPlane { Color = Vector4.One }, new ColoredPlane { Color = Vector4.UnitX })
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 6,
            Visible = false // constructed hidden, like the idle status bar
        };
        var plane = bar.BackgroundPanel.Background!;

        // Attach into the live, already-drawn tree — mirrors mid2.Children = [..., bar].
        mid2.AddChild(bar);

        var expectedLayer = bar.BackgroundPanel.Index;
        var actualLayerAfterAttach = context.LayerOf(plane);
        Assert.NotEqual(expectedLayer, actualLayerAfterAttach); // bug: stuck at its construction-time layer

        // Now show it the naive way (just flipping Visible, as SetStatus originally did).
        bar.Visible = true;
        Assert.NotEqual(expectedLayer, context.LayerOf(plane)); // still stuck — Visible alone never re-queues

        // The fix: an explicit DrawTo re-queues at the current (correct) Index.
        bar.DrawTo(context);
        Assert.Equal(expectedLayer, context.LayerOf(plane));
    }
}
