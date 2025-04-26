using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.UI;

public class WindowFrame : Panel
{
    protected readonly FlexPanel Container;
    protected readonly FlexPanel Header;
    protected CursorType RequestedCursor;
    private UIElement? _child;
    
    private bool _isResizingX;
    private bool _isResizingY;

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
        else if (_isResizingX || _isResizingY)
        {
            HandleActiveResize(mouse);
        }
        else if (Resizable && IsHovered)
        {
            ComputeResize(mouse);
        }
        
        if (mouse.IsButtonDown(MouseButton.Left)) return;
        _isResizingX = false;
        _isResizingY = false;
    }

    protected void ComputeHeaderPressed(MouseState mouse)
    {
        X += mouse.Delta.X;
        Y += mouse.Delta.Y;
        
        Layout();
    }

    protected void ComputeResize(MouseState mouse)
    {
        Console.Clear();
        const float rt = 10; // px

        var mx = mouse.X;
        var my = mouse.Y;

        var x = AbsoluteX;
        var y = AbsoluteY;
        var xw = x + Width;
        var yh = y + Height;

        var x_resize =
            mx > x - rt && mx <= x + rt ||
            mx >= xw - rt && mx < xw + rt;

        var y_resize = my > y && my <= y + rt ||
                  my >= yh - rt && my < yh + rt;
        
        if (x_resize)
            RequestedCursor = CursorType.ResizeX;
        else if (y_resize)
            RequestedCursor = CursorType.ResizeY;
        else
        {
            RequestedCursor = CursorType.Normal;
            return;
        }

        if (!mouse.IsButtonDown(MouseButton.Left)) return;
        _isResizingX = x_resize;
        _isResizingY = y_resize;
        
        Layout();
    }

    protected void SetChild(UIElement child)
    {
        _child = child;
        Container.Children = [Header, child];
    }

    protected void HandleActiveResize(MouseState mouse)
    {
        if (_isResizingX)
            Width += mouse.Delta.X;
            
        if (_isResizingY)
            Height += mouse.Delta.Y;
        
        Console.WriteLine($"W: {Width} H: {Height}");
        
        Layout();
    }
    
    public override void Update(UIContext context)
    {
        if (RequestedCursor != CursorType.Normal)
            context.RequestCursor.Invoke(RequestedCursor);
    }
}