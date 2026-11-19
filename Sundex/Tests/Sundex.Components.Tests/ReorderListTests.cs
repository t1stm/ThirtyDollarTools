using OpenTK.Mathematics;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class ReorderListTests
{
    // Three 20px rows from y=0: middles at 10, 30, 50.
    private static (TestUIContext ctx, FlexPanel root, ReorderList list, List<FlexPanel> rows) NewList()
    {
        var ctx = new TestUIContext();
        var root = new FlexPanel(ctx) { Width = 400, Height = 300 };
        var list = new ReorderList(ctx) { Width = 200 };
        var rows = new List<FlexPanel>();
        for (var index = 0; index < 3; index++)
        {
            var row = new FlexPanel(ctx) { Width = 200, Height = 20 };
            rows.Add(row);
            list.AddChild(row);
        }

        root.AddChild(list);
        root.Layout();
        return (ctx, root, list, rows);
    }

    [Fact]
    public void DraggingARowPastAnotherMovesIt()
    {
        var (ctx, root, list, rows) = NewList();
        (int, int)? moved = null;
        list.OnMove = (from, to) => moved = (from, to);

        ctx.UpdatePointer(root, 50, 10, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, 50, 55, true, false, false, Vector2.Zero);
        Assert.Contains("dragging", rows[0].Classes);
        Assert.Contains("drop-after", rows[2].Classes);

        ctx.UpdatePointer(root, 50, 55, false, false, true, Vector2.Zero);
        Assert.Equal((0, 2), moved);
        Assert.DoesNotContain("dragging", rows[0].Classes);
        Assert.DoesNotContain("drop-after", rows[2].Classes);
        Assert.Equal(0, rows[0].TranslateY);
    }

    [Fact]
    public void APressThatDoesntMoveClicksTheRow()
    {
        var (ctx, root, list, rows) = NewList();
        var moved = false;
        var clicked = -1;
        list.OnMove = (_, _) => moved = true;
        for (var position = 0; position < rows.Count; position++)
        {
            var index = position;
            rows[position].OnClick = _ => clicked = index;
        }

        ctx.UpdatePointer(root, 50, 30, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, 50, 32, true, false, false, Vector2.Zero);
        ctx.UpdatePointer(root, 50, 32, false, false, true, Vector2.Zero);

        Assert.Equal(1, clicked);
        Assert.False(moved);
    }

    [Fact]
    public void AButtonInsideARowKeepsItsPress()
    {
        var (ctx, root, list, rows) = NewList();
        var button = new Button(ctx, "x") { Width = 20, Height = 20 };
        var pressed = false;
        button.OnClick = _ => pressed = true;
        rows[0].AddChild(button);
        list.OnMove = (_, _) => { };
        root.Layout();

        ctx.UpdatePointer(root, 5, 10, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, 5, 10, false, false, true, Vector2.Zero);
        Assert.True(pressed);
    }
}
