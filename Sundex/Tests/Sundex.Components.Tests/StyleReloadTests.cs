using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Tests;

/// <summary>
///     Re-applying an edited stylesheet to a tree that is already on screen - the cheap half
///     of hot reload. The hard case is a rule that has been deleted since the last pass:
///     applying a sheet only ever writes the properties it mentions, so without
///     <see cref="UIElement.TrackPristineStyles" /> a value stays behind after the rule that
///     set it is gone, and the screen shows a style the source no longer contains.
/// </summary>
public class StyleReloadTests : IDisposable
{
    private static readonly Vector4 Red = new ColorValue("#ff0000").Vector;
    private static readonly Vector4 Blue = new ColorValue("#0000ff").Vector;

    private readonly TestUIContext _context = new();
    private readonly bool _tracked = UIElement.TrackPristineStyles;

    public StyleReloadTests()
    {
        UIElement.TrackPristineStyles = true;
    }

    public void Dispose()
    {
        UIElement.TrackPristineStyles = _tracked;
    }

    private static Vector4 FillOf(Panel panel)
    {
        return ((ColoredPlane)panel.Background!).Color;
    }

    /// <summary>A sheet giving class "card" the listed properties, and nothing else.</summary>
    private static StyleSheet Sheet(params (string Property, IStyleValue Value)[] properties)
    {
        var holder = new StyleSheetHolder();
        holder.Classes["card"] = properties.ToDictionary(p => p.Property, p => p.Value);
        return new StyleSheet(holder);
    }

    private Panel Card()
    {
        var panel = new Panel(_context) { Classes = ["card"] };
        panel.DrawTo(_context);
        return panel;
    }

    [Fact]
    public void ReapplyingAnEditedValueTakesEffect()
    {
        var card = Card();
        card.ApplyStyleSheet(Sheet(("background", new ColorValue("#ff0000"))));
        Assert.Equal(Red, FillOf(card));

        card.ApplyStyleSheet(Sheet(("background", new ColorValue("#0000ff"))));
        Assert.Equal(Blue, FillOf(card));
    }

    [Fact]
    public void DeletedRuleRevertsToThePreStyleValue()
    {
        // A panel has no fill until a sheet gives it one, so "before" here is null - and
        // that is what deleting the rule has to get back to. Reverting to some remembered
        // color instead would leave the panel painted by a rule that no longer exists.
        var card = Card();
        Assert.Null(card.Background);

        card.ApplyStyleSheet(Sheet(("background", new ColorValue("#ff0000"))));
        Assert.Equal(Red, FillOf(card));

        // What a reload does: revert the tree, then apply the edited sheet - which no
        // longer mentions background at all.
        card.ResetStyles();
        card.ApplyStyleSheet(Sheet(("width", new NumberValue(10, "px"))));
        Assert.Null(card.Background);
    }

    [Fact]
    public void DeletedRuleIsLeftInPlaceWhenTrackingIsOff()
    {
        // What a Release build does, and the reason the flag exists: the reload path is the
        // only thing that ever re-applies a sheet, so nothing else pays for this.
        UIElement.TrackPristineStyles = false;

        var card = Card();
        card.ApplyStyleSheet(Sheet(("background", new ColorValue("#ff0000"))));
        card.ResetStyles();
        card.ApplyStyleSheet(Sheet(("width", new NumberValue(10, "px"))));

        Assert.Equal(Red, FillOf(card));
    }

    [Fact]
    public void PropertiesTheSheetNeverSetAreLeftAlone()
    {
        // A width measured and assigned from code after the style pass must survive a
        // stylesheet reload; only what the sheet itself wrote is reverted.
        var card = Card();
        card.ApplyStyleSheet(Sheet(("background", new ColorValue("#ff0000"))));

        card.Width = 321f;
        card.ApplyStyleSheet(Sheet(("background", new ColorValue("#0000ff"))));

        Assert.Equal(321f, card.Width.Value);
        Assert.Equal(Blue, FillOf(card));
    }

    [Fact]
    public void ReloadCascadesThroughTheSubtree()
    {
        var child = new Panel(_context) { Classes = ["card"] };
        var root = new Panel(_context) { Children = [child] };
        root.DrawTo(_context);

        root.ApplyStyleSheet(Sheet(("background", new ColorValue("#ff0000"))));
        Assert.Equal(Red, FillOf(child));

        root.ApplyStyleSheet(Sheet(("background", new ColorValue("#0000ff"))));
        Assert.Equal(Blue, FillOf(child));
    }

    /// <summary>A sheet giving class "inner" the listed properties, and nothing else.</summary>
    private static StyleSheet InnerSheet(params (string Property, IStyleValue Value)[] properties)
    {
        var holder = new StyleSheetHolder();
        holder.Classes["inner"] = properties.ToDictionary(p => p.Property, p => p.Value);
        return new StyleSheet(holder);
    }

    [Fact]
    public void AHostSheetDoesNotUndoAnImportedComponentsStyling()
    {
        // How a composed screen is built: the imported document styles its own subtree, and
        // the host's sheet is then applied over the whole tree. The host has no rules for
        // the import's elements - its sheet covers its own document - so applying it must
        // leave what the import did alone. Reverting on every apply wiped exactly this, and
        // the editor's header labels came out at the default size and colour.
        var inner = new Panel(_context) { Classes = ["inner"] };
        var root = new Panel(_context) { Children = [inner] };
        root.DrawTo(_context);

        inner.ApplyStyleSheet(InnerSheet(("background", new ColorValue("#ff0000"))));
        root.ApplyStyleSheet(Sheet(("background", new ColorValue("#0000ff"))));

        Assert.Equal(Red, FillOf(inner));
    }

    [Fact]
    public void ResetGoesBackToBeforeTheFirstSheetNotTheLast()
    {
        var inner = new Panel(_context) { Classes = ["inner"] };
        var root = new Panel(_context) { Children = [inner] };
        root.DrawTo(_context);

        inner.ApplyStyleSheet(InnerSheet(("background", new ColorValue("#ff0000"))));
        root.ApplyStyleSheet(Sheet(("width", new NumberValue(10, "px"))));

        root.ResetStyles();

        // Not back to the imported sheet's red - back to no fill at all, so the whole
        // cascade can be re-run from nothing.
        Assert.Null(inner.Background);
    }

    [Fact]
    public void ResetThenReapplyReproducesTheOriginalCascade()
    {
        var inner = new Panel(_context) { Classes = ["inner", "card"] };
        var root = new Panel(_context) { Children = [inner] };
        root.DrawTo(_context);

        // "card" is the host's rule and comes second, so it wins - the order a rebuild
        // uses, and the order a reload has to repeat.
        inner.ApplyStyleSheet(InnerSheet(("background", new ColorValue("#ff0000"))));
        root.ApplyStyleSheet(Sheet(("background", new ColorValue("#0000ff"))));
        Assert.Equal(Blue, FillOf(inner));

        root.ResetStyles();
        inner.ApplyStyleSheet(InnerSheet(("background", new ColorValue("#ff0000"))));
        root.ApplyStyleSheet(Sheet(("background", new ColorValue("#0000ff"))));

        Assert.Equal(Blue, FillOf(inner));
    }

    [Fact]
    public void ResetRecursesThroughPanels()
    {
        var child = new Panel(_context) { Classes = ["card"] };
        var root = new Panel(_context) { Children = [child] };
        root.DrawTo(_context);

        root.ApplyStyleSheet(Sheet(("background", new ColorValue("#ff0000"))));
        root.ResetStyles();

        Assert.Null(child.Background);
    }

    [Fact]
    public void RevertedFillLeavesNoStaleRenderableQueued()
    {
        // The revert swaps a renderable, and a swap that skipped the queue would leave the
        // old plane painting forever - the failure mode HandleRenderableSwap exists for.
        var card = Card();
        card.ApplyStyleSheet(Sheet(("background", new ColorValue("#ff0000"))));
        var styled = card.Background!;

        card.ResetStyles();

        Assert.DoesNotContain(styled, _context.QueuedRenderables());
    }
}
