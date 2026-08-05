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
    /// <summary>Every component that styles itself through a class or an id.</summary>
    private static IEnumerable<UIElement> Components()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();

        yield return new ConfirmDialog(ctx, "Delete this?");
        yield return new UnsavedChangesDialog(ctx);
        yield return new TrackContextMenu(ctx, "Copy");
        yield return new ImportDialog(ctx, "song.tdw");
        yield return new ExportDialog(ctx);
        yield return new InstrumentSelector(ctx);
        yield return new EditorTrack(ctx, track, state);
        yield return new TrackListPanel(ctx, state);
        yield return new InspectorPanel(ctx, state);
    }

    [Fact]
    public void EverySelectorNamedInCode_IsDefinedByASheet()
    {
        var sheet = EditorTestContext.Styles;
        var missing = new List<string>();

        foreach (var root in Components())
            foreach (var element in Walk(root))
            {
                if (element.ID.Length > 0 && !sheet.IDTags.ContainsKey(element.ID))
                    missing.Add($"id {element.ID} ({element.GetType().Name})");

                foreach (var cls in element.Classes.Where(c => !sheet.Classes.ContainsKey(c)))
                    missing.Add($"class {cls} ({element.GetType().Name})");
            }

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
        var inspector = EditorTestContext.Styled(new InspectorPanel(ctx, state));

        // Opening a track swaps the inspector into segment/note mode, which is what
        // builds the field, checkbox, action and card rows.
        var track = state.AddTrack();
        state.OpenTrack(track);
        inspector.Rebuild();

        var sheet = EditorTestContext.Styles;
        var missing = Walk(inspector)
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

        foreach (var root in Components())
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
