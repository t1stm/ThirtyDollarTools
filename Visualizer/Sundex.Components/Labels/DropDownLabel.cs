using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Panels;

namespace Sundex.Components.Labels;

public sealed class DropDownLabel : Panel
{
    public DropDownLabel(UIContext context, string text, List<UIElement> panelChildren, bool hoverChildren = true) :
        base(context)
    {
        if (hoverChildren)
            panelChildren.ForEach(child => child.UpdateCursorOnHover = true);

        Panel = new FlexPanel(context)
        {
            Parent = this,
            Children = panelChildren,
            Direction = LayoutDirection.Vertical,
            Visible = false,
            Background = new ColoredPlane
            {
                Color = (0.2f, 0.2f, 0.2f, 1f)
            },
            Spacing = 4,
            Padding = 4,
            Width = LiteralOrComputable.AutoSize,
            Height = LiteralOrComputable.AutoSize
        };

        Label = new Label(context, text)
        {
            Parent = this,
            UpdateCursorOnHover = true,
            OnClick = _ => { Panel.Visible = !Panel.Visible; }
        };

        Children = [Label, Panel];
    }

    public FlexPanel Panel { get; }
    public Label Label { get; }

    public override LiteralOrComputable Width => Label?.Width ?? LiteralOrComputable.AutoSize;
    public override LiteralOrComputable Height => Label?.Height ?? LiteralOrComputable.AutoSize;

    protected override void DoLayout()
    {
        Label.Layout();

        Panel.Y = Computed.Height + 10;
        Panel.Layout();
    }

    public override (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        // Desired size equals the label's desired size
        return Label.Measure(parentWidth, parentHeight);
    }

    public override void Test(MouseState mouse, Vector2 scale)
    {
        var hide_panel = mouse.IsButtonPressed(MouseButton.Left);
        Label.Test(mouse, scale);
        Panel.Test(mouse, scale);

        if (hide_panel && !Label.IsHovered)
            Panel.Visible = false;
    }

    protected override void DrawSelf(UIContext context)
    {
    }

    #region IText

    [NamedSetting("text-value")]
    public ReadOnlySpan<char> Value
    {
        get => Label.Value;
        set => Label.Value = value;
    }

    [NamedSetting("font-size")] public LiteralOrComputable FontSizePx => Label.FontSizePx;

    public void SetTextContents(string text)
    {
        Label.SetTextContents(text);
    }

    #endregion
}