using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.UI;

public class WindowFrame : Panel
{
    protected readonly Panel Container;
    protected readonly FlexPanel Header;
    protected CursorType RequestedCursor;
    private UIElement? _child;

    private byte _resizingXMode;
    private byte _resizingYMode;

    public override float Width
    {
        get => Container.Width;
        set => Container.Width = value;
    }

    public override float Height
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

    public WindowFrame(float x = 0, float y = 0, float w = 600, float h = 400) : base(x, y, w, h)
    {
        Header = new FlexPanel(0, 0, w, 30)
        {
            Background = new ColoredPlane((0.1f, 0.1f, 0.1f, 1.0f)),
            VerticalAlign = Align.Center,
            HorizontalAlign = Align.End,
            AutoWidth = true,
            Padding = 10,
            Children =
            [
                new Label("X")
            ]
        };
        Container = new FlexPanel(x, y, w, h)
        {
            Children = [Header],
            Direction = LayoutDirection.Vertical,
        };
        
        Children = [Container];
    }

    public override void Test(MouseState mouse)
    {
        base.Test(mouse);

        if (Header.IsPressed)
        {
            ComputeHeaderPressed(mouse);
        }
        else if (_resizingXMode != 0 || _resizingYMode != 0)
        {
            HandleActiveResize(mouse);
        }
        else if (Resizable && IsHovered)
        {
            ComputeResize(mouse);
        }

        if (mouse.IsButtonDown(MouseButton.Left)) return;
        _resizingXMode = 0;
        _resizingYMode = 0;
    }

    protected void ComputeHeaderPressed(MouseState mouse)
    {
        X += mouse.Delta.X;
        Y += mouse.Delta.Y;

        Layout();
    }

    protected void ComputeResize(MouseState mouse)
    {
        const float rt = 10; // px

        var mx = mouse.X;
        var my = mouse.Y;

        var x = AbsoluteX;
        var y = AbsoluteY;
        var xw = x + Width;
        var yh = y + Height;

        var x_negative = mx > x - rt && mx <= x + rt;
        var x_positive = mx >= xw - rt && mx < xw + rt;

        var y_negative = my > y && my <= y + rt;
        var y_positive = my >= yh - rt && my < yh + rt;

        var xr = x_positive || x_negative;
        var yr = y_positive || y_negative;

        if (!xr && !yr)
            return;

        if (!mouse.IsButtonDown(MouseButton.Left)) return;
        _resizingXMode = (byte)(x_positive ? 1 : x_negative ? 2 : 0);
        _resizingYMode = (byte)(y_positive ? 1 : y_negative ? 2 : 0);

        Layout();
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
                Width += mouse.Delta.X;
                break;
            case 2:
                X += mouse.Delta.X;
                Width -= mouse.Delta.X;
                break;
        }

        switch (_resizingYMode)
        {
            case 1:
                Height += mouse.Delta.Y;
                break;
            case 2:
                Y -= mouse.Delta.Y;
                Height += mouse.Delta.Y;
                break;
        }

        Layout();
    }

    public override void Update(UIContext context)
    {
        if (RequestedCursor != CursorType.Normal)
            context.RequestCursor.Invoke(RequestedCursor);
    }
}