using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EditorScene.Scenes;
using EditorScene.Scenes.Layout;
using OpenTK.Mathematics;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Serilog;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Tests;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Core;
using Sundex.Style.DSL;

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

    /// <summary>
    ///     The editor's real stylesheet, imports and all. Components under test are built
    ///     the way <see cref="EditorInterface" /> builds them - detached, then styled - so
    ///     anything asserting a size, a color or a corner has to apply this first, or it is
    ///     measuring an unstyled element. <see cref="Styled{T}" /> does both.
    /// </summary>
    public static StyleSheet Styles { get; } = LoadStyles();

    /// <summary>Applies <see cref="Styles" /> to a freshly built component and returns it.</summary>
    public static T Styled<T>(T element) where T : UIElement
    {
        element.ApplyStyleSheet(Styles);
        return element;
    }

    /// <summary>
    ///     An <see cref="InspectorPanel" /> over a standalone InspectorShell build. The
    ///     editor gets these four handles from the root document's logic, which needs a live
    ///     <see cref="EditorInterface" />; this resolves them straight off the component
    ///     instead. The shell's own &lt;style&gt; styles it as it is built, so no
    ///     <see cref="Styled{T}" /> call is needed on top.
    /// </summary>
    public InspectorPanel NewInspector(EditorState state)
    {
        var markup = AssetProvider.Load<StringAsset, StringInfo>(
            StringInfo.CreateFromUnknownStorage("Scenes/Layout/Inspector Shell/InspectorShell.snx.xml")).Value;
        var component = new SundexContext(this).NewComponent(markup);
        return new InspectorPanel(this, state,
            component.GetID<Panel>("inspector-panel"),
            component.GetID<ScrollView>("inspector-rows"),
            component.GetID<ProgressBar>("inspector-status-bar"),
            component.GetID<Label>("inspector-status-label"));
    }

    private static StyleSheet LoadStyles()
    {
        var provider = new AssetProvider(new LoggerConfiguration().CreateLogger(),
            [typeof(EditorInterface).Assembly], new GLInfo());

        string Read(string path)
        {
            using var stream = provider.Load<AssetStream, AssetInfo>(new AssetInfo { Location = path }).Stream;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        return new StyleSheet(StyleParser.Parse(Read("Scenes/Layout/Editor Interface/EditorInterface.snx.ss"), Read));
    }

    /// <summary>
    ///     The render layer a renderable is actually queued at, or -1 if it isn't queued
    ///     anywhere - for asserting a hidden-then-shown element re-queued at its correct depth
    ///     instead of staying stuck at its stale construction-time layer.
    /// </summary>
    public int LayerOf(IRenderable renderable)
    {
        for (var i = 0; i < LayeredRenderQueue.Count; i++)
            if (LayeredRenderQueue[i].Any(r => ReferenceEquals(r, renderable)))
                return i;
        return -1;
    }

    /// <summary>
    ///     Every queued renderable that would actually issue a draw call this frame -
    ///     <see cref="UIContext.Render" />'s three culls (invisible, zero scale, empty clip
    ///     rect) applied without a GL context. One entry is one draw call: a batch counts
    ///     once no matter how many instances it holds.
    /// </summary>
    public List<IRenderable> DrawCalls()
    {
        var drawn = new List<IRenderable>();
        foreach (var queue in LayeredRenderQueue)
        foreach (var renderable in queue)
        {
            if (renderable is Renderable { IsVisible: false }) continue;
            if (renderable is Renderable { Scale: var scale } && (scale.X <= 0 || scale.Y <= 0)) continue;
            if ((renderable as IClippable)?.ClipRect is { } clip && (clip.Z <= clip.X || clip.W <= clip.Y)) continue;
            drawn.Add(renderable);
        }

        return drawn;
    }
}