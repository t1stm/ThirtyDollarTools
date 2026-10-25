using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Layout;
using EditorScene.Scenes.Views;
using EditorScene.State;
using Sundex.Components.Abstractions;
using Sundex.Components.File_Selector;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

/// <summary>
///     Every class or id named in C# must be defined by a sheet. Nothing throws on a mismatch -
///     the element just keeps its bare framework defaults and renders wrong - so it is caught here.
/// </summary>
public class StyleSelectorTests
{
    /// <summary>
    ///     Trees still assembled in C#, where an id names a rule the element needs. Both
    ///     ids and classes are checked for these.
    /// </summary>
    private static IEnumerable<UIElement> CodeBuilt()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();

        var selector = new InstrumentSelector(ctx, ctx.NewAtlasStore());
        selector.Fill([new Instrument { Name = "Lead" }]);
        yield return selector;
        // Sundex's, but its parts are styled by this editor's sheet; built the way DialogHost does.
        yield return new FileSelection(ctx, "song", ".tdwproj", "Save", title: "Save project")
            { ID = "file-dialog", Classes = ["dialog-frame"] };
        yield return new EditorTrack(ctx, track, state);
        yield return new TrackListPanel(ctx, state);
        // The rows, not the whole panel: the shell around them is markup.
        yield return ctx.NewInspector(state).Rows;
        // The two previews the Add a track dialog doesn't start on.
        yield return new FaithfulPreview(ctx, ctx.NewAtlasStore());
        yield return TrackKindPreview.Wave(ctx);
    }

    /// <summary>
    ///     Trees built from a markup document. Only their classes are checked: in markup an
    ///     id is a handle for whatever resolves the document (its logic, or the dialog's
    ///     own constructor), not a promise that a sheet styles it - transport-elapsed,
    ///     inspector-status-label and every dialog button have none. Classes still have to
    ///     resolve, and these trees are worth walking because they also carry classes set
    ///     from C# after the build, like a Checkbox's own label.
    /// </summary>
    private static IEnumerable<UIElement> MarkupBuilt()
    {
        var ctx = new EditorTestContext();

        yield return new ConfirmDialog(ctx, "Delete this?", "It goes for good.").Element;
        yield return new UnsavedChangesDialog(ctx).Element;
        yield return new DuplicateTrackDialog(ctx, "Track", "Track copy").Element;
        // Each way's preview and facts show only while it is picked. This file has a snapped
        // note, a lost action and more sounds than the project preview has lanes for.
        var sequence = Sequence.FromString("!speed@640|a|!stop@0.95|b|!stop@0.05|c|d|e|f|!divider");
        var import = new ImportDialog(ctx, ctx.NewAtlasStore(), "song.tdw", sequence,
            SequenceImporter.Preview(sequence, null), new Dictionary<string, Sound>(),
            [OpenTK.Mathematics.Vector4.One], "Song", true);
        foreach (var mode in new[] { ImportMode.Faithful, ImportMode.Track, ImportMode.Project })
        {
            import.Mode = mode;
            yield return import.Element;
        }

        yield return new TrackTypeDialog(ctx, ctx.NewAtlasStore(), [], OpenTK.Mathematics.Vector4.One).Element;
        // One per list the form builds: modes, common values, targets, colors, and none at all.
        var track = new FaithfulTrack(new TimingInfo(), 1);
        track.Items.Add(FaithfulItem.Parse("!target@1")!);
        foreach (var action in new[] { "!speed", "!stop", "!target", "!bg", "!pulse" })
            yield return new ActionValueDialog(ctx, FaithfulAction.Find(action)!, track, track.Items.Count).Element;
        yield return new ExportDialog(ctx).Element;
        // Every section kept, so the code-built cells and the overflow line are walked too.
        yield return new ImportWarningsDialog(ctx, "song.tdw", ImportMode.Track, new ImportWarnings(
            Enumerable.Range(0, 9).ToDictionary(i => $"!event{i}", i => i + 1), 3, ["mystery"])).Element;
    }

    [Fact]
    public void EverySelectorNamedInCode_IsDefinedByASheet()
    {
        var sheet = EditorTestContext.Styles;
        var missing = new List<string>();

        foreach (var root in CodeBuilt())
            foreach (var element in Walk(root))
                if (element.ID.Length > 0 && !sheet.IDTags.ContainsKey(element.ID))
                    missing.Add($"id {element.ID} ({element.GetType().Name})");

        foreach (var root in CodeBuilt().Concat(MarkupBuilt()))
            foreach (var element in Walk(root))
                foreach (var cls in element.Classes.Where(c => !sheet.Classes.ContainsKey(c)))
                    missing.Add($"class {cls} ({element.GetType().Name})");

        Assert.Empty(missing.Distinct());
    }

    /// <summary>
    ///     The faithful views are built in code, but they need an atlas store and a GL context
    ///     the headless harness has no business building - so their selectors are checked by
    ///     name instead of by walking them.
    /// </summary>
    [Theory]
    [InlineData("faithful-body")]
    [InlineData("faithful-palette")]
    [InlineData("faithful-instruments")]
    [InlineData("faithful-actions")]
    [InlineData("faithful-palette-grid")]
    [InlineData("faithful-palette-cell")]
    [InlineData("faithful-section-bar")]
    [InlineData("cell-caption")]
    [InlineData("cell-name")]
    [InlineData("cell-count")]
    [InlineData("faithful-sequence")]
    public void FaithfulViewClasses_AreDefinedByASheet(string name)
    {
        Assert.True(EditorTestContext.Styles.Classes.ContainsKey(name), $"sheet lost class \"{name}\"");
    }

    /// <summary>
    ///     The inspector's rows are built on demand rather than in its constructor, so its
    ///     row builder's selectors need a populated panel to show up in the walk above.
    /// </summary>
    [Fact]
    public void InspectorRowSelectors_AreDefinedByASheet()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var inspector = ctx.NewInspector(state);

        // Opening a track swaps the inspector into segment/note mode, which is what
        // builds the field, checkbox, action and card rows.
        var track = state.AddTrack();
        state.OpenTrack(track);
        inspector.Rebuild();

        var sheet = EditorTestContext.Styles;
        var missing = Walk(inspector.Rows)
            .SelectMany(e => e.Classes.Where(c => !sheet.Classes.ContainsKey(c))
                .Concat(e.ID.Length > 0 && !sheet.IDTags.ContainsKey(e.ID) ? [e.ID] : []))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    ///     The state classes, which no walk can find: they are named only inside the
    ///     SetClass call that adds them when the state turns on, so a typo in one is
    ///     invisible until someone selects a row or picks a tool and nothing changes color.
    ///     Each must both exist and override a property - a modifier that sets nothing new
    ///     is a rule that was gutted, not a state.
    /// </summary>
    [Theory]
    [InlineData("track-row-selected", "background")]
    [InlineData("tool-segment-idle", "background")]
    [InlineData("tool-segment-draw", "background")]
    [InlineData("tool-segment-select", "background")]
    [InlineData("light-label", "font-color")]
    [InlineData("key-chip-lit", "background")]
    [InlineData("key-chip-label-lit", "font-color")]
    [InlineData("header-key-button", "padding")]
    [InlineData("header-menu-button-open", "background")]
    [InlineData("menu-key-item", "horizontal-align")]
    [InlineData("key-chip-menu", "width")]
    [InlineData("lane-toggle-muted", "font-color")]
    [InlineData("lane-toggle-soloed", "font-color")]
    [InlineData("dark-label", "font-color")]
    public void EveryClassAddedAtRuntime_IsDefinedByASheet(string cls, string property)
    {
        Assert.True(EditorTestContext.Styles.Classes.TryGetValue(cls, out var properties),
            $"no sheet defines class {cls}");
        Assert.Contains(property, properties!.Keys);
    }

    private static IEnumerable<UIElement> Walk(UIElement element)
    {
        yield return element;
        if (element is not Sundex.Components.Panels.Panel panel) yield break;
        foreach (var child in panel.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }
}
