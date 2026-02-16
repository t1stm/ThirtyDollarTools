using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;

namespace Sundex.Components.Scroll;

public sealed class ScrollBar : Panel
{
    public readonly Panel ScrollBlock;

    public ScrollBar(UIContext context, Panel parent) : base(context)
    {
        ScrollBlock = new Panel(Context)
        {
            Background = new ColoredPlane
            {
                Color = Vector4.One
            },
            Height = 20
        };
        Parent = parent;
        Background = new ColoredPlane
        {
            Color = (0.3f, 0.3f, 0.3f, 1)
        };
        Children = [ScrollBlock];
    }

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

    public override void Test(MouseState mouse, Vector2 scale)
    {
        if (!Visible) return;
        base.Test(mouse, scale);
        ScrollBlock.Test(mouse, scale);
        if (!ScrollBlock.IsPressed) return;

        var delta_y = mouse.Delta.Y;
        var percentage_diff = delta_y / Height;

        Percentage += percentage_diff;
        Percentage = Math.Clamp(Percentage, 0, 1);
        ScrollBlock.Y = Percentage * (Height - ScrollBlock.Height);
    }

    protected override void DoLayout()
    {
        ScrollBlock.X = 0;
        ScrollBlock.Y = Percentage * (Height - ScrollBlock.Height);
        ScrollBlock.Width = Width;
        base.DoLayout();
    }

    protected override void DrawSelf(UIContext context)
    {
        // 
    }
}