using System.Diagnostics.CodeAnalysis;
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

    private class TestUIContext : UIContext
    {
        [SetsRequiredMembers]
        public TestUIContext()
        {
            var logger = new LoggerConfiguration().CreateLogger();
            InjectForTesting(
                new AssetProvider(logger, [], new GLInfo()),
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