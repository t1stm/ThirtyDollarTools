using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using EditorScene.Scenes.Views;

// UIContext stores its providers in static fields (see Sundex.Components.Tests),
// so this suite must also run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EditorScene.Tests;

/// <summary>
///     Headless smoke test: the editor's snx layout + stylesheet parse and the ids the
///     code-behind looks up exist. A stylesheet typo otherwise only surfaces when the
///     scene is opened.
/// </summary>
public class EditorInterfaceMarkupTests
{
    private readonly EditorTestContext _context = new();

    /// <summary>Loads a document by its path under Scenes/ (e.g. "Layout/Hint Bar/HintBar").</summary>
    private static string Load(UIContext context, string name)
    {
        return context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = $"Scenes/{name}.snx.xml",
                Storage = StorageLocation.Assembly
            }
        }).Value;
    }

    /// <summary>
    ///     Mirrors EditorInterface's constructor: every name the root resolves - factory
    ///     tags and imported components alike - registered before the root is built.
    /// </summary>
    private SundexContext BuildableContext()
    {
        var markup = new SundexContext(_context);
        // Stubs, not the real views: this suite is about the markup, and the views need
        // an EditorState and a GL context the headless harness has no business building.
        markup.RegisterElementFactory("track-list", ctx => new ScrollView(ctx));
        foreach (var tag in new[] { "arrangement-view", "lane-header", "track-editor-view", "sound-picker" })
            markup.RegisterElementFactory(tag, ctx => new Panel(ctx));
        foreach (var name in new[]
                 {
                     "Layout/Editor Header/EditorHeader",
                     "Layout/Transport Section/TransportSection",
                     "Layout/Hint Bar/HintBar",
                     "Layout/Arrangement Panel/ArrangementPanel",
                     "Layout/Track Editor Panel/TrackEditorPanel",
                     "Layout/Inspector Shell/InspectorShell",
                     "Dialogs/Sound Filter/SoundFilter"
                 })
            markup.NewComponent(Load(_context, name));
        return markup;
    }

    /// <summary>The imported region that owns <paramref name="id" />, by name.</summary>
    private static SundexComponent Child(SundexComponent root, string name)
    {
        return (SundexComponent)Assert.Single(root.Children, child => child.Name == name);
    }

    [Fact]
    public void EditorInterfaceMarkup_ParsesAndRegistersAllWiredIds()
    {
        var component = BuildableContext().NewComponent(Load(_context, "Layout/Editor Interface/EditorInterface"));

        string[] wired = ["track-column", "grid-area", "inspector-column"];
        foreach (var id in wired)
            Assert.True(component.RegisteredIDs.ContainsKey(id), $"markup lost id \"{id}\"");
    }

    /// <summary>
    ///     A sub-component's ids live in its own map, never the host's - that is what each
    ///     region's .snx.csx reaches through, so a rename there is silent until the scene
    ///     opens.
    /// </summary>
    [Theory]
    [InlineData("editor-header", "project-name", "project-bpm", "load-button", "save-button", "export-button")]
    [InlineData("transport-section", "transport-progress", "transport-elapsed", "transport-total",
        "transport-play", "transport-stop", "transport-back")]
    [InlineData("hint-bar", "hint-bar", "hint-gutter", "hint-label")]
    [InlineData("arrangement-panel", "arrangement-panel", "arrangement-tool-draw", "arrangement-tool-select")]
    [InlineData("track-editor-panel", "track-editor-panel", "opened-track-name", "instrument-button",
        "back-to-arrangement", "editor-tool-draw", "editor-tool-select")]
    [InlineData("inspector-shell", "inspector-panel", "inspector-rows", "inspector-status-bar",
        "inspector-status-label")]
    [InlineData("sound-filter", "sound-filter-modal", "sound-filter-frame", "sound-filter-list",
        "sound-filter-done")]
    public void ImportedRegions_RegisterTheirIdsOnTheChildComponent(string name, params string[] wired)
    {
        var root = BuildableContext().NewComponent(Load(_context, "Layout/Editor Interface/EditorInterface"));
        var child = Child(root, name);

        foreach (var id in wired)
        {
            Assert.True(child.RegisteredIDs.ContainsKey(id), $"markup lost id \"{id}\"");
            Assert.DoesNotContain(id, root.RegisteredIDs.Keys);
        }
    }

    /// <summary>
    ///     A class in markup that no sheet defines is silent - the element keeps its bare
    ///     framework defaults and renders wrong. StyleSelectorTests guards that for
    ///     code-built elements, but every region of the editor is markup now, so the same
    ///     check has to run here. Against each component's *own* sheet, not the root's:
    ///     EditorHeader.snx.ss defines header-divider locally and the root no longer sees it.
    ///     Ids are deliberately not checked - a markup id is a handle for its document's
    ///     logic (transport-elapsed, inspector-status-label), not a promise of a rule.
    /// </summary>
    [Fact]
    public void EveryClassInMarkup_IsDefinedByItsOwnComponentsSheet()
    {
        var root = BuildableContext().NewComponent(Load(_context, "Layout/Editor Interface/EditorInterface"));
        var missing = new List<string>();

        foreach (var component in root.Children.Cast<SundexComponent>().Prepend(root))
            foreach (var cls in component.RegisteredClasses.Keys)
                if (component.StyleSheet?.Classes.ContainsKey(cls) != true)
                    missing.Add($"{component.Name ?? "root"}: class {cls}");

        Assert.Empty(missing);
    }

    /// <summary>
    ///     Registration order is load-bearing and a second build must not depend on the
    ///     first - the builder rebuilds each usage site from the retained document.
    /// </summary>
    [Fact]
    public void Root_BuildsTwiceWithoutThrowing()
    {
        var markup = BuildableContext();
        markup.NewComponent(Load(_context, "Layout/Editor Interface/EditorInterface"));
        markup.NewComponent(Load(_context, "Layout/Editor Interface/EditorInterface"));
    }

    /// <summary>
    ///     A factory-built view keeps the class it set on itself and is styled through it.
    ///     Both halves are load-bearing and neither is visible from the markup: the grid's
    ///     line and block colors are settings on the view (class note-canvas, set in its
    ///     constructor), and the builder used to assign markup's class list over the
    ///     element's own - which dropped it, leaving the whole grid painting transparent.
    /// </summary>
    [Fact]
    public void AFactoryBuiltView_KeepsItsOwnClass_AndIsStyledThroughIt()
    {
        var state = new EditorState();
        var view = new TrackEditorView(_context, state);
        var markup = new SundexContext(_context);
        markup.RegisterElementFactory("track-editor-view", _ => view);
        markup.NewComponent(Load(_context, "Layout/Track Editor Panel/TrackEditorPanel"));

        // grid-view from the markup, note-canvas from the constructor - in that order.
        Assert.Equal(["note-canvas", "grid-view"], view.Classes);
        Assert.NotEqual(default, view.StepLineColor);
        Assert.NotEmpty(view.SoundPalette);
    }
}