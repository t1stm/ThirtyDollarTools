using Sundex.Components.Abstractions;
using Sundex.Markup.Abstract;
using Sundex.Markup.Builders;
using Sundex.Markup.Document;
using Sundex.Style.DSL;

namespace Sundex.Markup;

public class SundexComponent : ISundexComponent
{
    public required string Version { get; set; }

    /// <summary>
    ///     Runs this component's logic block, then cascades into imported children so a
    ///     document composed of sub-components wires all of them from one call.
    /// </summary>
    public Action<object?>? RunLogic { get; set; }

    /// <summary>
    ///     The sheet this component was built with, or null when the document declared no
    ///     style. Exposed so code-built UI can style itself from the same sheet, and so
    ///     values that aren't element properties (colors used in raw draw calls) can be
    ///     read out of it instead of being compiled in.
    ///     <para>
    ///         Settable within the assembly rather than init-only so
    ///         <see cref="ReloadStyleSheet" /> can swap in a re-read sheet; callers outside
    ///         still only read it.
    ///     </para>
    /// </summary>
    public StyleSheet? StyleSheet { get; internal set; }

    public Dictionary<string, UIElement> RegisteredIDs { get; init; } = [];
    public Dictionary<string, List<UIElement>> RegisteredClasses { get; init; } = [];
    public required ISundexContext Context { get; init; }
    public required UIElement Element { get; set; }
    public required SundexDocument Document { get; init; }

    public HashSet<ISundexComponent> Dependencies { get; init; } = [];
    public List<ISundexComponent> Children { get; init; } = [];

    public string? Name { get; init; }

    /// <summary>
    ///     Re-reads this component's stylesheet from disk and applies it to the tree that is
    ///     already on screen, without rebuilding a single element. The cheap half of hot
    ///     reload: everything a sheet controls - colors, sizes, spacing, animations - is a
    ///     styled property, so an edited sheet lands without losing scroll positions,
    ///     selection, or anything else the live tree is holding.
    ///     <para>
    ///         Imported sub-components are reloaded first and this component's sheet applied
    ///         over the whole tree afterwards, matching the order the original build used -
    ///         a host's rules are meant to win over the ones its imports declare.
    ///     </para>
    ///     <para>
    ///         Rules deleted since the last pass only revert if
    ///         <see cref="UIElement.TrackPristineStyles" /> was on when the tree was styled.
    ///         Anything the sheet cannot reach still needs a full rebuild: the layout itself,
    ///         and the planes that progress bars, sliders and buttons take as constructor
    ///         arguments.
    ///     </para>
    /// </summary>
    public void ReloadStyleSheet()
    {
        // Once, over the whole tree, before anything is applied. Reverting per sheet
        // instead would undo an imported component's styling on the pass that applies its
        // host's - the host's rules cover its own document, not the one it imported, so
        // there would be nothing left to put the sub-component's look back.
        Element.ResetStyles();
        ReapplyStyleSheet();
    }

    /// <summary>
    ///     Re-reads and applies the sheets, imports first and this component's own last -
    ///     the order <see cref="ComponentBuilderV1" /> builds them in, and the order that
    ///     makes a host's rules win over the ones its imports declare.
    /// </summary>
    private void ReapplyStyleSheet()
    {
        foreach (var child in Children)
            if (child is SundexComponent component) component.ReapplyStyleSheet();
            else child.ReloadStyleSheet();

        var styleSheet = ComponentBuilderV1.BuildStyleSheet(Document, Context);
        if (styleSheet is null) return;

        StyleSheet = styleSheet;
        Element.ApplyStyleSheet(styleSheet);
    }

    public T GetID<T>(string id) where T : UIElement
    {
        if (!RegisteredIDs.TryGetValue(id, out var element))
            throw new Exception($"Unable to find element with id: {id}");
        return element as T ?? throw new Exception($"Element with id: {id} is not of type {typeof(T)}");
    }
}