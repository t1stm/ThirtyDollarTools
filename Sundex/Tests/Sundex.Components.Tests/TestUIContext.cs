using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using OpenTK.Mathematics;
using Serilog;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;

namespace Sundex.Components.Tests;

public class TestUIContext : UIContext
{
    [SetsRequiredMembers]
    public TestUIContext() : this(new LoggerConfiguration().CreateLogger())
    {
    }

    [SetsRequiredMembers]
    public TestUIContext(ILogger logger)
    {
        InjectForTesting(
            new AssetProvider(logger, [Assembly.GetExecutingAssembly()], new GLInfo()),
            new MockFontProvider(),
            new MockTextProvider());
        Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
    }
}