using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Scroll;

/// <summary>
///     Vertical scroll bar: a draggable thumb on a track, self-positioned at the right
///     edge of its parent. Only reports <see cref="Percentage" /> (0..1) — the consumer
///     moves its own content (<see cref="ScrollView" /> does this). Dragging uses the
///     capture hooks; clicking the track jumps the thumb.
/// </summary>
public sealed class ScrollBar : Panel
{
    private static readonly Vector4 ThumbColor = new(0.478f, 0.635f, 0.968f, 1f); // #7aa2f7

    public readonly Panel ScrollBlock;

    public ScrollBar(UIContext context, Panel parent) : base(context)
    {
        ScrollBlock = new Panel(Context)
        {
            Background = new ColoredPlane
            {
                Color = ThumbColor
            },
            Height = 20,
            BorderRadius = 4
        };
        Parent = parent;
        Background = new ColoredPlane
        {
            Color = (0.3f, 0.3f, 0.3f, 1)
        };
        BorderRadius = 4;
        Children = [ScrollBlock];
    }

    public float Percentage { get; private set; }

    /// <summary>Fired when the user moves the thumb (not on programmatic <see cref="SetPercentage" />).</summary>
    public Action<ScrollBar>? OnScrolled { get; set; }

    public override LiteralOrComputable X
    {
        get
        {
            var p = Parent;
            if (p is null) return 0;
            var pc = p.Computed;
            return pc.Width - Computed.Width;
        }
        set => throw new NotSupportedException();
    }

    public override LiteralOrComputable Y
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override LiteralOrComputable Width { get; set; } = 8;

    public override LiteralOrComputable Height
    {
        get
        {
            var p = Parent;
            if (p is null) return 0;
            var pc = p.Computed;
            return pc.Height;
        }
        set => throw new NotSupportedException();
    }

    /// <summary>Moves the thumb without firing <see cref="OnScrolled" />.</summary>
    public void SetPercentage(float percentage)
    {
        percentage = Math.Clamp(percentage, 0, 1);
        if (Math.Abs(Percentage - percentage) < 0.0001f) return;
        Percentage = percentage;
        InvalidateLayout();
    }

    public override bool HandlePress(float x, float y)
    {
        MoveThumbTo(y);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        MoveThumbTo(y);
    }

    private float ThumbHeight => ScrollBlock.Height.IsPercentage
        ? Computed.Height * (ScrollBlock.Height.Value / 100f)
        : ScrollBlock.Height.Value;

    private void MoveThumbTo(float y)
    {
        var track = Computed.Height - ThumbHeight;
        if (track <= 0) return;

        var percentage = Math.Clamp((y - Computed.AbsoluteY - ThumbHeight / 2) / track, 0, 1);
        if (Math.Abs(Percentage - percentage) < 0.0001f) return;

        Percentage = percentage;
        InvalidateLayout();
        OnScrolled?.Invoke(this);
    }

    protected override void DoLayout()
    {
        ScrollBlock.X = 0;
        ScrollBlock.Y = Percentage * (Computed.Height - ThumbHeight);
        ScrollBlock.Width = Computed.Width;
        base.DoLayout();
    }
}
