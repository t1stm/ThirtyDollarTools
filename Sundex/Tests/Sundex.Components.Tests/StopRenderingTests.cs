using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using OpenTK.Mathematics;
using Serilog;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Core;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Cameras;

namespace Sundex.Components.Tests;

public class StopRenderingTests
{
    [Fact]
    public void TestRemoveChild_CallsStopRendering()
    {
        var context = new TestUIContext();
        var parent = new Panel(context);
        parent.DrawTo(context);
        var child = new Panel(context)
        {
            Background = new MockRenderable()
        };

        parent.AddChild(child);

        // Ensure it's queued
        var queue = context.GetRenderQueue();
        Assert.Contains(child.Background, queue[child.Index]);

        // Remove child
        parent.RemoveChild(child);

        // Ensure it's dequeued
        Assert.DoesNotContain(child.Background, queue[child.Index]);
    }

    [Fact]
    public void TestRemoveChild_DequeuesTheWholeSubtree()
    {
        var context = new TestUIContext();
        var root = new Panel(context);
        root.DrawTo(context);
        var middle = new Panel(context);
        var leaf = new Panel(context) { Background = new MockRenderable() };

        middle.AddChild(leaf);
        root.AddChild(middle);

        var queue = context.GetRenderQueue();
        Assert.Contains(leaf.Background, queue[leaf.Index]);

        root.RemoveChild(middle);
        Assert.DoesNotContain(leaf.Background, queue[leaf.Index]);
    }

    [Fact]
    public void TestReparentedSubtree_MovesAndFullyDequeues()
    {
        var context = new TestUIContext();
        var root = new Panel(context);
        root.DrawTo(context);
        var dialog = new Panel(context);
        var inner = new Panel(context) { Background = new MockRenderable() };

        // Build the subtree detached, then parent it deeper — the modal-dialog
        // pattern. The renderable must appear exactly once, at the final depth.
        dialog.AddChild(inner);
        root.AddChild(dialog);

        var queue = context.GetRenderQueue();
        Assert.Equal(1, queue.Sum(layer => layer.Count(r => ReferenceEquals(r, inner.Background))));
        Assert.Contains(inner.Background, queue[inner.Index]);

        // Removing the subtree must leave no ghost in any layer.
        root.RemoveChild(dialog);
        Assert.All(queue, layer => Assert.DoesNotContain(inner.Background, layer));
    }

    [Fact]
    public void AddChild_OnANeverDrawnPanel_DoesNotDrawTheChild_UntilTheSubtreeIsDrawn()
    {
        // Renderables that queue through DrawSelf (e.g. a Label's text) must not
        // appear while composing a subtree off-tree — they'd draw at 0,0 over
        // whatever scene is live. (Background renderables queue through their
        // property setter instead; that separate path is unchanged.)
        var context = new TestUIContext();
        var detached = new Panel(context);
        var probe = new DrawSelfProbe(context);

        detached.AddChild(probe);
        var queue = context.GetRenderQueue();
        Assert.All(queue, layer => Assert.DoesNotContain(probe.Renderable, layer));

        // Parenting the subtree into a drawn tree queues the whole thing.
        var root = new Panel(context);
        root.DrawTo(context);
        root.AddChild(detached);
        Assert.Contains(probe.Renderable, queue[probe.Index]);

        // And removing it dequeues again, so the cycle can repeat (view swapping).
        root.RemoveChild(detached);
        Assert.All(queue, layer => Assert.DoesNotContain(probe.Renderable, layer));
        root.AddChild(detached);
        Assert.Contains(probe.Renderable, queue[probe.Index]);
    }

    private class DrawSelfProbe(UIContext context) : UIElement(context)
    {
        public readonly MockRenderable Renderable = new();

        public override string Tag => "probe";

        protected override void DrawSelf(UIContext context)
        {
            context.QueueRender(Renderable, Index);
        }

        public override void StopRendering()
        {
            Context.DequeueRender(Renderable, Index);
        }
    }

    private class TestUIContext : UIContext
    {
        [SetsRequiredMembers]
        public TestUIContext()
        {
            var logger = new LoggerConfiguration().CreateLogger();
            InjectForTesting(
                new AssetProvider(logger, [Assembly.GetExecutingAssembly()], new GLInfo()),
                new MockFontProvider(),
                new MockTextProvider());
            Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(800, 600));
        }

        public List<List<IRenderable>> GetRenderQueue()
        {
            return LayeredRenderQueue;
        }
    }

    private class MockRenderable : Renderable
    {
        public override void Render(Camera camera)
        {
        }

        public override void Update()
        {
        }
    }
}