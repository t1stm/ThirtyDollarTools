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
        var dialog = new Panel(context);
        var inner = new Panel(context) { Background = new MockRenderable() };

        // Build the subtree detached (queues inner at a shallow index), then parent it
        // deeper — the modal-dialog pattern. The renderable must move, not duplicate.
        dialog.AddChild(inner);
        root.AddChild(dialog);

        var queue = context.GetRenderQueue();
        Assert.Equal(1, queue.Sum(layer => layer.Count(r => ReferenceEquals(r, inner.Background))));
        Assert.Contains(inner.Background, queue[inner.Index]);

        // Removing the subtree must leave no ghost in any layer.
        root.RemoveChild(dialog);
        Assert.All(queue, layer => Assert.DoesNotContain(inner.Background, layer));
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