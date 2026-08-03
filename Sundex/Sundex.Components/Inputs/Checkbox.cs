using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Abstract.Extensions;

namespace Sundex.Components.Inputs;

/// <summary>
///     Checkbox with an optional label; clicking anywhere on it toggles.
///     Consumers listen through <see cref="OnCheckedChanged" /> - <see cref="UIElement.OnClick" />
///     is used internally for the toggle, don't overwrite it.
/// </summary>
public class Checkbox : StackPanel
{
    private const float BoxSize = 18;
    private const float CheckInset = 4;

    private readonly Panel _box;

    // Off-tree plane like TextInput's caret: always queued, hidden via Scale = 0.
    private readonly ColoredPlane _check = new() { Color = new Vector4(0.9f, 0.9f, 0.9f, 1f) };

    public Checkbox(UIContext context, string label = "", bool isChecked = false) : base(context)
    {
        Spacing = 8;
        UpdateCursorOnHover = true;

        _box = new Panel(context)
        {
            Width = BoxSize,
            Height = BoxSize,
            Background = new ColoredPlane { Color = new Vector4(1, 1, 1, 0.25f) }
        };
        Label = new Label(context, label) { FontSizePx = 16f };
        Children = [_box, Label];

        Checked = isChecked;
        OnClick = _ => Checked = !Checked;
    }

    public override string Tag => "checkbox";
    public override LiteralOrComputable Width { get; set; } = LiteralOrComputable.AutoSize;
    public override LiteralOrComputable Height { get; set; } = LiteralOrComputable.AutoSize;

    public Label Label { get; }

    public Action<Checkbox>? OnCheckedChanged { get; set; }

    public bool Checked
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
            OnCheckedChanged?.Invoke(this);
        }
    }

    public Vector4 CheckColor
    {
        get => _check.Color;
        set => _check.Color = value;
    }

    protected override void DoLayout()
    {
        // Center the label vertically against the box before the stack positions it.
        Label.Y = (BoxSize - Label.Height.Resolve(0)) / 2;
        base.DoLayout();

        _check.SetPosition((_box.Computed.AbsoluteX + CheckInset, _box.Computed.AbsoluteY + CheckInset, 0));
        _check.Scale = Checked
            ? new Vector3(BoxSize - 2 * CheckInset, BoxSize - 2 * CheckInset, 1)
            : Vector3.Zero;
    }

    protected override void DrawSelf(UIContext ctx)
    {
        base.DrawSelf(ctx);
        ctx.QueueRender(_check, Index + 2);
    }

    public override void DrawTo(UIContext ctx)
    {
        if (!Visible) return;
        base.DrawTo(ctx);
        _check.Update();
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        ApplyAnimations(_check);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        Context.DequeueRender(_check, Index + 2);
    }

    public override void ApplyClip(Vector4i? clip)
    {
        base.ApplyClip(clip);
        _check.ClipRect = clip;
    }
}