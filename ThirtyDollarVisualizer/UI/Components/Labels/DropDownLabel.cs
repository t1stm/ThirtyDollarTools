using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.UI.Abstractions;
using ThirtyDollarVisualizer.UI.Components.Panels;

namespace ThirtyDollarVisualizer.UI.Components.Labels;

public sealed class DropDownLabel : Panel
{
    public DropDownLabel(string text, List<UIElement> panelChildren, bool hoverChildren = true) : base(0, 0, 0, 0)
    {
        if (hoverChildren)
            panelChildren.ForEach(child => child.UpdateCursorOnHover = true);
        Panel = new FlexPanel
        {
            Parent = this,
            AutoWidth = true,
            AutoHeight = true,
            AutoSizeSelf = true,
            Children = panelChildren,
            Direction = LayoutDirection.Vertical,
            Visible = false,
            Background = new ColoredPlane
            {
                Color = (0.2f, 0.2f, 0.2f, 1f)
            },
            Spacing = 4,
            Padding = 4
        };

        Label = new Label(text)
        {
            Parent = this,
            UpdateCursorOnHover = true,
            OnClick = _ => { Panel.Visible = !Panel.Visible; }
        };

        Children = [Label, Panel];
    }

    public FlexPanel Panel { get; }
    public Label Label { get; }

    public override float Width => Label.Width;
    public override float Height => Label.Height;

    public override void Layout()
    {
        Label.Layout();

        Panel.Y = Height + 10;
        Panel.Layout();
    }

    public override void Test(MouseState mouse)
    {
        var hide_panel = mouse.IsButtonPressed(MouseButton.Left);
        Label.Test(mouse);
        Panel.Test(mouse);

        if (hide_panel && !Label.IsHovered)
            Panel.Visible = false;
    }

    protected override void DrawSelf(UIContext context)
    {
    }

    #region IText

    public ReadOnlySpan<char> Value
    {
        get => Label.Value;
        set => Label.Value = value;
    }

    public float FontSizePx => Label.FontSizePx;

    public void SetTextContents(string text)
    {
        Label.SetTextContents(text);
    }

    #endregion
}