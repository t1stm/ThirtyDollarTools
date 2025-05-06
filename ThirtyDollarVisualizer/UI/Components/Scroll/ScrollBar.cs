using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.UI.Components.Scroll;

public sealed class ScrollBar : Panel
{
    public readonly Panel ScrollBlock = new()
    {
        Background = new ColoredPlane
        {
            Color = Vector4.One
        },
        Height = 20
    };

    public float Percentage { get; private set; }

    public override float X
    {
        get => Parent?.Width - Width ?? 0;
        set => throw new NotSupportedException();
    }

    public override float Y
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override float Width { get; set; } = 20;

    public override float Height
    {
        get => Parent?.Height ?? 0;
        set => throw new NotSupportedException();
    }

    public ScrollBar(Panel parent)
    {
        Parent = parent;
        Background = new ColoredPlane
        {
            Color = (0.3f, 0.3f, 0.3f, 1)
        };
        Children = [ScrollBlock];
    }

    public override void Test(MouseState mouse)
    {
        ScrollBlock.Test(mouse);
        if (!ScrollBlock.IsPressed) return;

        var delta_y = mouse.Delta.Y;
        var percentage_diff = delta_y / Height;
        
        Percentage += percentage_diff;
        Percentage = Math.Clamp(Percentage, 0, 1);
        ScrollBlock.Y = Percentage * (Height - ScrollBlock.Height);
    }

    public override void Layout()
    {
        ScrollBlock.X = X;
        ScrollBlock.Y = Percentage * (Height - ScrollBlock.Height);
        ScrollBlock.Width = Width;
    }

    protected override void DrawSelf(UIContext context)
    {
        // 
    }
}