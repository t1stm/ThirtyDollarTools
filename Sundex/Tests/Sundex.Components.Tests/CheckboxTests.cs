using OpenTK.Mathematics;
using Sundex.Components.Inputs;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class CheckboxTests
{
    private static (TestUIContext ctx, Panel root, Checkbox box) NewCheckbox(string label = "", bool isChecked = false)
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        var box = new Checkbox(ctx, label, isChecked) { X = 10, Y = 10 };
        root.Children = [box];
        root.Layout();
        return (ctx, root, box);
    }

    private static void Click(TestUIContext ctx, Panel root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, x, y, false, false, true, Vector2.Zero);
    }

    [Fact]
    public void Click_TogglesAndFiresCallback()
    {
        var (ctx, root, box) = NewCheckbox();
        var fired = 0;
        box.OnCheckedChanged = _ => fired++;

        Click(ctx, root, 15, 15); // inside the 18x18 box at (10,10)
        Assert.True(box.Checked);
        Assert.Equal(1, fired);

        Click(ctx, root, 15, 15);
        Assert.False(box.Checked);
        Assert.Equal(2, fired);
    }

    [Fact]
    public void ClickOnLabel_AlsoToggles()
    {
        var (ctx, root, box) = NewCheckbox("Mute");

        // Label starts after the box (18) + spacing (8): x = 36 absolute.
        Click(ctx, root, 40, 15);
        Assert.True(box.Checked);
    }

    [Fact]
    public void ProgrammaticSet_FiresCallbackOnlyOnChange()
    {
        var (_, _, box) = NewCheckbox(isChecked: true);
        var fired = 0;
        box.OnCheckedChanged = _ => fired++;

        box.Checked = true; // no change
        Assert.Equal(0, fired);

        box.Checked = false;
        Assert.Equal(1, fired);
    }
}