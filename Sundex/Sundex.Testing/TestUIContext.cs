using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using OpenTK.Mathematics;
using Serilog;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;

namespace Sundex.Testing;

/// <summary>A UI context with mock font providers. Run tests that use it sequentially.</summary>
public class TestUIContext : UIContext
{
    [SetsRequiredMembers]
    public TestUIContext() : this(new LoggerConfiguration().CreateLogger())
    {
    }

    [SetsRequiredMembers]
    public TestUIContext(ILogger logger) : this(logger, [])
    {
    }

    [SetsRequiredMembers]
    public TestUIContext(ILogger logger, Assembly[] assetAssemblies)
    {
        InjectForTesting(
            new AssetProvider(logger, [.. assetAssemblies, ComponentsAssembly.Assembly], new GLInfo()),
            new MockFontProvider(),
            new MockTextProvider());
        Camera = new UICamera(Vector3.Zero, new Vector2i(1920, 1080));
    }

    /// <summary>Everything currently queued to draw, flattened across depth layers.</summary>
    public IEnumerable<IRenderable> QueuedRenderables()
    {
        return LayeredRenderQueue.SelectMany(layer => layer);
    }

    /// <summary>Whether the element has queued its drawing.</summary>
    public static bool IsDrawn(UIElement element) => element.Drawn;
}
