using OpenTK.Mathematics;
using Sundex.Components.Inputs;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class SliderTests
{
    // Slider at (10,10), 200x8: track spans x 10..210.
    private static (TestUIContext ctx, Panel root, Slider slider) NewSlider(double min = 0, double max = 100)
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        var slider = new Slider(ctx) { X = 10, Y = 10, Width = 200, Height = 8, Min = min, Max = max };
        root.Children = [slider];
        root.Layout();
        return (ctx, root, slider);
    }

    [Fact]
    public void Press_SetsValueFromPosition()
    {
        var (ctx, root, slider) = NewSlider();

        ctx.UpdatePointer(root, 110, 14, true, true, false, Vector2.Zero);
        Assert.Equal(50, slider.Value);
        Assert.Equal(0.5f, slider.Progress, 3);
    }

    [Fact]
    public void Drag_UpdatesAndClampsOutsideBounds()
    {
        var (ctx, root, slider) = NewSlider();

        ctx.UpdatePointer(root, 110, 14, true, true, false, Vector2.Zero);
        // Capture keeps the drag alive even far outside the bar.
        ctx.UpdatePointer(root, 400, 300, true, false, false, Vector2.Zero);
        Assert.Equal(100, slider.Value);

        ctx.UpdatePointer(root, 0, 14, true, false, false, Vector2.Zero);
        Assert.Equal(0, slider.Value);

        ctx.UpdatePointer(root, 0, 14, false, false, true, Vector2.Zero);
        Assert.Equal(0, slider.Value); // release keeps the value
    }

    [Fact]
    public void Step_SnapsToIncrements()
    {
        var (ctx, root, slider) = NewSlider();
        slider.Step = 10;

        ctx.UpdatePointer(root, 116, 14, true, true, false, Vector2.Zero); // t=0.53 -> 53 -> 50
        Assert.Equal(50, slider.Value);
    }

    [Fact]
    public void ValueSetter_ClampsAndFiresOnChangeOnly()
    {
        var (_, _, slider) = NewSlider();
        var fired = 0;
        slider.OnValueChanged = _ => fired++;

        slider.Value = 150;
        Assert.Equal(100, slider.Value);
        Assert.Equal(1, fired);

        slider.Value = 120; // clamps to the same 100
        Assert.Equal(1, fired);
    }

    [Fact]
    public void FractionalStep_ProducesCleanValues()
    {
        var (_, _, slider) = NewSlider(0, 1);
        slider.Step = 0.1;

        slider.Value = 0.33;
        Assert.Equal(0.3, slider.Value);
    }
}