using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Sundex.Components.Abstractions;

public enum LayoutDirection
{
    Horizontal,
    Vertical
}

public enum Align
{
    Start,
    Center,
    End,
    Stretch
}

public abstract class UIElement(UIContext context, float x, float y, float width, float height)
{
    private float _absoluteX;
    private float _absoluteY;
    private bool _coordinatesDirty = true;

    /// <summary>
    /// Whether the width of this element is automatically calculated.
    /// </summary>
    public bool AutoWidth
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }
    } = false;

    /// <summary>
    /// Whether the height of this element is automatically calculated.
    /// </summary>
    public bool AutoHeight
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }
    } = false;

    /// <summary>
    /// The context this element is associated with.
    /// </summary>
    public UIContext Context => context;

    /// <summary>
    /// The X coordinate relative to its parent.
    /// </summary>
    public virtual float X
    {
        get;
        set
        {
            field = value;
            InvalidateCoordinates();
            InvalidateLayout();
        }
    } = x;

    /// <summary>
    /// The Y coordinate relative to its parent.
    /// </summary>
    public virtual float Y
    {
        get;
        set
        {
            field = value;
            InvalidateCoordinates();
            InvalidateLayout();
        }
    } = y;

    /// <summary>
    /// The X coordinate relative to the screen.
    /// </summary>
    public virtual float AbsoluteX
    {
        get
        {
            if (_coordinatesDirty) UpdateAbsoluteCoordinates();
            return _absoluteX;
        }
    }

    /// <summary>
    /// The Y coordinate relative to the screen.
    /// </summary>
    public virtual float AbsoluteY
    {
        get
        {
            if (_coordinatesDirty) UpdateAbsoluteCoordinates();
            return _absoluteY;
        }
    }

    /// <summary>
    /// The index of this element in its parent.
    /// </summary>
    protected virtual int Index { get; set; }

    /// <summary>
    /// The width of this element.
    /// </summary>
    public virtual float Width
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = width;

    /// <summary>
    /// The height of this element.
    /// </summary>
    public virtual float Height
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = height;

    /// <summary>
    /// Whether this element is visible and should be rendered.
    /// </summary>
    public bool Visible
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }
    } = true;

    /// <summary>
    /// Whether the mouse is currently over this element.
    /// </summary>
    public bool IsHovered { get; set; }

    /// <summary>
    /// Whether this element is currently being pressed by the mouse.
    /// </summary>
    public bool IsPressed { get; set; }

    /// <summary>
    /// Whether to update the mouse cursor when this element is hovered.
    /// </summary>
    public bool UpdateCursorOnHover { get; set; }

    /// <summary>
    /// Whether this element needs its layout recalculated.
    /// </summary>
    public bool NeedsLayout { get; protected set; } = true;

    /// <summary>
    /// The parent of this element.
    /// </summary>
    public virtual UIElement? Parent
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Index = field?.Index + 1 ?? 0;
            InvalidateCoordinates();
            InvalidateLayout();
        }
    }

    /// <summary>
    /// The viewport this element is rendered in.
    /// </summary>
    public virtual Vector4i? Viewport
    {
        get => field ?? Parent?.Viewport;
        set;
    }

    /// <summary>
    /// Action invoked when this element is clicked.
    /// </summary>
    public Action<UIElement>? OnClick { get; set; }
    
    /// <summary>
    /// Action invoked when this element gets hovered.
    /// </summary>
    public Action<UIElement>? OnHoverEnter { get; set; }
    
    /// <summary>
    /// Action invoked when this element gets unhovered.
    /// </summary>
    public Action<UIElement>? OnHoverExit { get; set; }

    /// <summary>
    /// Tests mouse interaction with this element.
    /// </summary>
    /// <param name="mouse">The current mouse state.</param>
    /// <param name="scale">The UI scale.</param>
    public virtual void Test(MouseState mouse, Vector2 scale)
    {
        if (!Visible) return;

        var absX = AbsoluteX;
        var absY = AbsoluteY;

        var mouseX = mouse.X / scale.X;
        var mouseY = mouse.Y / scale.Y;

        var oldHovered = IsHovered;
        IsHovered = mouse.X >= absX && mouseX <= absX + Width &&
                    mouse.Y >= absY && mouseY <= absY + Height;

        switch (oldHovered, IsHovered)
        {
            case (false, true):
                OnHoverEnter?.Invoke(this);
                break;
            
            case (true, false):
                OnHoverExit?.Invoke(this);
                break;
        }
        
        IsPressed = false;
        switch (IsHovered)
        {
            case false:
                return;
            
            case true when mouse.IsButtonPressed(MouseButton.Left):
                OnClick?.Invoke(this);
                break;
            
            case true when mouse.IsButtonDown(MouseButton.Left):
                IsPressed = true;
                break;
        }
    }

    /// <summary>
    /// Updates this element's state.
    /// </summary>
    /// <param name="uiContext">The current UI context.</param>
    public virtual void Update(UIContext uiContext)
    {
        if (IsHovered && UpdateCursorOnHover)
            uiContext.RequestCursor(CursorType.Pointer);
    }

    /// <summary>
    /// Marks this element's layout as dirty and notifies the parent.
    /// </summary>
    public virtual void InvalidateLayout()
    {
        if (NeedsLayout) return;
        NeedsLayout = true;
        Parent?.InvalidateLayout();
    }

    /// <summary>
    /// Marks coordinates as dirty, requiring a recalculation.
    /// </summary>
    public virtual void InvalidateCoordinates()
    {
        _coordinatesDirty = true;
        NeedsLayout = true;
    }

    /// <summary>
    /// Recalculates the absolute coordinates based on the parent's position.
    /// </summary>
    protected virtual void UpdateAbsoluteCoordinates()
    {
        _absoluteX = Parent?.AbsoluteX + X ?? X;
        _absoluteY = Parent?.AbsoluteY + Y ?? Y;
        _coordinatesDirty = false;
    }

    /// <summary>
    /// Performs layout calculations if needed.
    /// </summary>
    public virtual void Layout()
    {
        if (!NeedsLayout) return;
        DoLayout();
        NeedsLayout = false;
    }

    /// <summary>
    /// Internal method to handle specific layout logic.
    /// </summary>
    protected virtual void DoLayout()
    {
    }

    /// <summary>
    /// Renders this element to the specified context.
    /// </summary>
    /// <param name="uiContext">The UI context to render into.</param>
    public virtual void DrawTo(UIContext uiContext)
    {
        if (!Visible) return;
        Layout();
        DrawSelf(uiContext);
    }

    /// <summary>
    /// Internal method to handle specific rendering logic.
    /// </summary>
    /// <param name="context">The UI context to render into.</param>
    protected abstract void DrawSelf(UIContext context);
}