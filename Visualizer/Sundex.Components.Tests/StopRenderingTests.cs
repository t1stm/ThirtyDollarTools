using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Abstract;
using Xunit;
using System.Collections.Generic;
using Sundex.Engine.Renderer.Cameras;
using OpenTK.Mathematics;
using Sundex.Core;
using Shared;

namespace Sundex.Components.Tests;

public class StopRenderingTests
{
    private class TestUIContext : UIContext
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public TestUIContext()
        {
            var logger = new Serilog.LoggerConfiguration().CreateLogger();
            InjectForTesting(
                new Sundex.Engine.Asset_Management.AssetProvider(logger, [], new Sundex.Engine.GLInfo()),
                new MockFontProvider(),
                new MockTextProvider());
            Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(800, 600));
        }

        public List<List<IRenderable>> GetRenderQueue() => LayeredRenderQueue;
    }

    private class MockRenderable : Renderable
    {
        public override void Render(Camera camera) { }
        public override void Update() { }
    }

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
}
