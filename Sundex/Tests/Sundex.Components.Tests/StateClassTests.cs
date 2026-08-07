using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Panels;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Tests;

/// <summary>
///     Runtime state as a class: a modifier class added over a base one, and removed
///     again. This is how a selected row or an active toggle is expressed - the element
///     re-styles itself from the sheet it was last given, instead of code reaching into
///     its renderables.
/// </summary>
public class StateClassTests
{
    private static readonly Vector4 Base = new ColorValue("#101010").Vector;
    private static readonly Vector4 Modified = new ColorValue("#202020").Vector;
    private static readonly Vector4 Hovered = new ColorValue("#303030").Vector;

    private static Vector4 FillOf(Panel panel)
    {
        return ((ColoredPlane)panel.Background!).Color;
    }

    private static StyleSheet Sheet()
    {
        var holder = new StyleSheetHolder
        {
            Classes =
            {
                ["row"] = new Dictionary<string, IStyleValue>
                {
                    { "background", new ColorValue("#101010") },
                    {
                        "state[hovered]",
                        new BlockValue(new Dictionary<string, IStyleValue>
                            { { "background", new ColorValue("#303030") } })
                    }
                },
                ["row-selected"] = new Dictionary<string, IStyleValue>
                {
                    { "background", new ColorValue("#202020") }
                }
            }
        };
        return new StyleSheet(holder);
    }

    [Fact]
    public void AddingAModifierClass_OverridesTheBaseRule_AndRemovingItRestoresIt()
    {
        var context = new TestUIContext();
        var panel = new Panel(context) { Classes = ["row"] };
        panel.ApplyStyleSheet(Sheet());
        Assert.Equal(Base, FillOf(panel));

        // Applied after "row" because it is listed after it - that ordering is the whole
        // reason Classes is a list.
        Assert.True(panel.SetClass("row-selected", true));
        Assert.Equal(Modified, FillOf(panel));

        // And the base rule still declares the property, so dropping the modifier and
        // re-styling gives it back.
        Assert.True(panel.SetClass("row-selected", false));
        Assert.Equal(Base, FillOf(panel));

        // A no-op toggle neither reports a change nor re-styles.
        Assert.False(panel.SetClass("row-selected", false));
    }

    [Fact]
    public void TogglingAClassWhileHovered_KeepsTheHoverOverride()
    {
        // A row is commonly selected by a click, i.e. while the pointer is on it: the
        // re-style has to land on the base values and then re-apply the hover block, not
        // leave the element stuck on its resting fill until the pointer moves.
        var context = new TestUIContext();
        var panel = new Panel(context) { Classes = ["row"], Width = 100, Height = 20 };
        panel.ApplyStyleSheet(Sheet());
        panel.DrawTo(context);

        context.UpdatePointer(panel, 50, 10, false, false, false, Vector2.Zero);
        Assert.Equal(Hovered, FillOf(panel));

        panel.SetClass("row-selected", true);
        Assert.Equal(Hovered, FillOf(panel));

        // Un-hovering falls back to the modifier's fill, not the base one.
        context.UpdatePointer(panel, 500, 500, false, false, false, Vector2.Zero);
        Assert.Equal(Modified, FillOf(panel));
    }
}
