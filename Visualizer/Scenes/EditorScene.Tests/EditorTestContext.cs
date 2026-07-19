using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EditorScene.Scenes;
using OpenTK.Mathematics;
using Serilog;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Tests;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;

namespace EditorScene.Tests;

/// <summary>
///     Headless UIContext over the EditorScene assembly's embedded assets, with the
///     shared mock font/text providers from Sundex.Components.Tests.
/// </summary>
public class EditorTestContext : UIContext
{
    [SetsRequiredMembers]
    public EditorTestContext()
    {
        InjectForTesting(
            new AssetProvider(new LoggerConfiguration().CreateLogger(),
                [typeof(EditorInterface).Assembly, Assembly.GetExecutingAssembly()], new GLInfo()),
            new MockFontProvider(),
            new MockTextProvider());
        Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
    }
}
