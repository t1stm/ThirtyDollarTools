using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Layout;
using Sundex.Components.Abstractions;

namespace EditorScene.Tests;

/// <summary>
///     Guards the one failure mode the migration to stylesheets introduced: a class or id
///     named in C# that no sheet defines. Nothing throws on that - the element just keeps
///     its bare framework defaults and renders wrong - so the mismatch has to be caught here.
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

        yield return new InstrumentSelector(ctx);
        yield return new EditorTrack(ctx, track, state);
        yield return new TrackListPanel(ctx, state);
        // The rows, not the whole panel: the shell around them is markup.
        yield return ctx.NewInspector(state).Rows;
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

        yield return new ConfirmDialog(ctx, "Delete this?").Element;
        yield return new UnsavedChangesDialog(ctx).Element;
        yield return new TrackContextMenu(ctx, "Copy").Element;
        yield return new ImportDialog(ctx, "song.tdw").Element;
        yield return new ExportDialog(ctx).Element;
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
    ///     Two classes on one element must not set the same property: Classes is a HashSet,
    ///     so which one wins is undefined. Variants override on the id instead - see the
    ///     rule at the top of Scenes/Styles/Controls.snx.ss.
    /// </summary>
    [Fact]
    public void NoElement_CombinesTwoClassesThatSetTheSameProperty()
    {
        var sheet = EditorTestContext.Styles;
        var clashes = new List<string>();

        foreach (var root in CodeBuilt().Concat(MarkupBuilt()))
            foreach (var element in Walk(root).Where(e => e.Classes.Count > 1))
            {
                var seen = new Dictionary<string, string>();
                foreach (var cls in element.Classes)
                {
                    if (!sheet.Classes.TryGetValue(cls, out var properties)) continue;
                    foreach (var property in properties.Keys)
                        if (!seen.TryAdd(property, cls))
                            clashes.Add($"{seen[property]} and {cls} both set {property}");
                }
            }

        Assert.Empty(clashes.Distinct());
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
