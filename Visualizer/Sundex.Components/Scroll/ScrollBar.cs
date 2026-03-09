using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
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

    public override LiteralOrComputable X
    {
        get
        {
            var p = Parent;
            if (p is null) return 0;
            var pc = p.Computed;
            if (pc is null) return 0;
            return pc.Width - Computed.Width;
        }
        set => throw new NotSupportedException();
    }

    public override LiteralOrComputable Y
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override LiteralOrComputable Width { get; set; } = 20;

    public override LiteralOrComputable Height
    {
        get
        {
            var p = Parent;
            if (p is null) return 0;
            var pc = p.Computed;
            if (pc is null) return 0;
            return pc.Height;
        }
        set => throw new NotSupportedException();
    }

    public override void Test(MouseState mouse, Vector2 scale)
    {
        if (!Visible) return;
        base.Test(mouse, scale);
        ScrollBlock.Test(mouse, scale);
        if (!ScrollBlock.IsPressed) return;

        var delta_y = mouse.Delta.Y;
        var percentage_diff = delta_y / Computed.Height;

        Percentage += percentage_diff;
        Percentage = Math.Clamp(Percentage, 0, 1);
        var sbh = ScrollBlock.Height.IsPercentage
            ? Computed.Height * (ScrollBlock.Height.Value / 100f)
            : ScrollBlock.Height.Value;
        var innerH = Computed.Height - sbh;
        ScrollBlock.Y = Percentage * innerH;
    }

    protected override void DoLayout()
    {
        ScrollBlock.X = 0;
        var sbh = ScrollBlock.Height.IsPercentage
            ? Computed.Height * (ScrollBlock.Height.Value / 100f)
            : ScrollBlock.Height.Value;
        var innerH = Computed.Height - sbh;
        ScrollBlock.Y = Percentage * innerH;
        ScrollBlock.Width = Computed.Width;
        base.DoLayout();
    }

    protected override void DrawSelf(UIContext context)
    {
        // 
    }
}