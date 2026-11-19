using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;

namespace Sundex.Components.Panels;

public class WindowFrame : Panel
{
    protected readonly Panel Container;
    protected readonly FlexPanel Header;
    protected CursorType RequestedCursor;
    private UIElement? _child;

    private byte _resizingXMode;
    private byte _resizingYMode;

    public WindowFrame(UIContext context) : base(context)
    {
        Header = new FlexPanel(context)
        {
            Background = new ColoredPlane
            {
                Color = (0.1f, 0.1f, 0.1f, 1.0f)
            },
            VerticalAlign = Align.Center,
            HorizontalAlign = Align.End,
            Padding = 10,
            Children =
            [
                new Label(Context, "X")
            ]
        };
        Container = new FlexPanel(context)
        {
            Children = [Header],
            Direction = LayoutDirection.Vertical
        };

        Children = [Container];
    }

    public override LiteralOrComputable Width
    {
        get => Container.Width;
        set => Container.Width = value;
    }

    public override LiteralOrComputable Height
    {
        get => Container.Height;
        set => Container.Height = value;
    }

    public bool Resizable { get; set; }

    public UIElement? Child
    {
        get => _child;
        set
        {
            if (value != null)
                SetChild(value);
            else _child = null;
        }
    }

    public override void Test(MouseState mouse, Vector2 scale)
    {
        base.Test(mouse, scale);

        if (Header.IsPressed)
            ComputeHeaderPressed(mouse);
        else if (_resizingXMode != 0 || _resizingYMode != 0)
            HandleActiveResize(mouse);
        else if (Resizable && IsHovered) ComputeResize(mouse);

        if (mouse.IsButtonDown(MouseButton.Left)) return;
        _resizingXMode = 0;
        _resizingYMode = 0;
    }

    protected void ComputeHeaderPressed(MouseState mouse)
    {
        X = Computed.X + mouse.Delta.X;
        Y = Computed.Y + mouse.Delta.Y;
    }

    protected void ComputeResize(MouseState mouse)
    {
        const float resizeMargin = 10; // px

        var mouseX = mouse.X;
        var mouseY = mouse.Y;

        var x = Computed.AbsoluteX;
        var y = Computed.AbsoluteY;
        var right = x + Computed.Width;
        var bottom = y + Computed.Height;

        var x_negative = mouseX > x - resizeMargin && mouseX <= x + resizeMargin;
        var x_positive = mouseX >= right - resizeMargin && mouseX < right + resizeMargin;

        var y_negative = mouseY > y && mouseY <= y + resizeMargin;
        var y_positive = mouseY >= bottom - resizeMargin && mouseY < bottom + resizeMargin;

        var resizesX = x_positive || x_negative;
        var resizesY = y_positive || y_negative;

        if (!resizesX && !resizesY)
            return;

        if (!mouse.IsButtonDown(MouseButton.Left)) return;
        _resizingXMode = (byte)(x_positive ? 1 : x_negative ? 2 : 0);
        _resizingYMode = (byte)(y_positive ? 1 : y_negative ? 2 : 0);
    }

    protected void SetChild(UIElement child)
    {
        _child = child;
        Container.Children = [Header, child];
    }

    protected void HandleActiveResize(MouseState mouse)
    {
        switch (_resizingXMode)
        {
            case 1:
                Width = Computed.Width + mouse.Delta.X;
                break;
            case 2:
                X = Computed.X + mouse.Delta.X;
                Width = Computed.Width - mouse.Delta.X;
                break;
        }

        switch (_resizingYMode)
        {
            case 1:
                Height = Computed.Height + mouse.Delta.Y;
                break;
            case 2:
                Y = Computed.Y - mouse.Delta.Y;
                Height = Computed.Height + mouse.Delta.Y;
                break;
        }
    }

    public override void Update(UIContext uiContext)
    {
        if (RequestedCursor != CursorType.Default)
            uiContext.RequestCursor.Invoke(RequestedCursor);
    }
}