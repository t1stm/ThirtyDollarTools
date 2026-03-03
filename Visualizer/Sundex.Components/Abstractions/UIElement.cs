using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

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

public abstract class UIElement
{
    protected UIElement(UIContext context)
    {
        Context = context;
        Computed = new ComputedRectangle(this);
    }

    public abstract string Tag { get; }
    public UIContext Context { get; }

    public string ID { get; set; } = "";
    public HashSet<string> Classes { get; set; } = [];
    public ComputedRectangle Computed { get; }

    private void UpdateSetDirty<T>(out T field, T value)
    {
        field = value;
        NeedsLayout = true;
    }

    [NamedSetting("x")]
    public virtual LiteralOrPercentage X
    {
        get;
        set => UpdateSetDirty(out field, value);
    }

    [NamedSetting("y")]
    public virtual LiteralOrPercentage Y
    {
        get;
        set => UpdateSetDirty(out field, value);
    }

    [NamedSetting("width")]
    public virtual LiteralOrPercentage Width
    {
        get;
        set => UpdateSetDirty(out field, value);
    }

    [NamedSetting("height")]
    public virtual LiteralOrPercentage Height
    {
        get;
        set => UpdateSetDirty(out field, value);
    }

    [NamedSetting("index")] protected virtual int Index { get; set; }

    [NamedSetting("visible")]
    public bool Visible
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }
    } = true;

    public bool IsHovered { get; set; }
    public bool IsPressed { get; set; }
    public bool UpdateCursorOnHover { get; set; }
    public bool NeedsLayout { get; protected set; } = true;

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

    public virtual Vector4i? Viewport
    {
        get => field ?? Parent?.Viewport;
        set;
    }

    public Action<UIElement>? OnClick { get; set; }
    public Action<UIElement>? OnHoverEnter { get; set; }
    public Action<UIElement>? OnHoverExit { get; set; }

    /// <summary>
    /// Tests mouse interaction with this element.
    /// </summary>
    /// <param name="mouse">The current mouse state.</param>
    /// <param name="scale">The UI scale.</param>
    public virtual void Test(MouseState mouse, Vector2 scale)
    {
        if (!Visible) return;

        var absX = Computed.AbsoluteX;
        var absY = Computed.AbsoluteY;

        var mouseX = mouse.X / scale.X;
        var mouseY = mouse.Y / scale.Y;

        var oldHovered = IsHovered;
        IsHovered = mouse.X >= absX && mouseX <= absX + Computed.Width &&
                    mouse.Y >= absY && mouseY <= absY + Computed.Height;

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
        NeedsLayout = true;
    }

    /// <summary>
    /// Performs layout calculations if needed.
    /// </summary>
    public virtual void Layout()
    {
        if (!NeedsLayout) return;
        Computed.UpdateAbsoluteBasedOnParent(this, Parent);
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

    public virtual void ApplyStyleSheet(StyleSheet styleSheet)
    {
        var type = GetType();
        var properties = type.GetProperties();
        foreach (var propertyInfo in properties)
        {
            var attribute = propertyInfo.GetCustomAttribute<NamedSettingAttribute>();
            if (attribute is null) continue;

            SetNamedSetting(styleSheet, propertyInfo, attribute);
        }
    }

    private void SetNamedSetting(StyleSheet styleSheet, PropertyInfo propertyInfo,
        NamedSettingAttribute namedSettingAttribute)
    {
        ApplyStyleValue(styleSheet.GetStyleValueForTag(Tag, namedSettingAttribute.Name), propertyInfo);
        foreach (var cls in Classes)
        {
            ApplyStyleValue(styleSheet.GetStyleValueForTag(cls, namedSettingAttribute.Name), propertyInfo);
        }

        ApplyStyleValue(styleSheet.GetStyleValueForTag(ID, namedSettingAttribute.Name), propertyInfo);
    }

    protected virtual void ApplyStyleValue(IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue == null)
            return;

        switch (styleValue)
        {
            case NumberValue nv when propertyInfo.PropertyType == typeof(LiteralOrPercentage):
            {
                var newValue = new LiteralOrPercentage(nv.Value, nv.Unit is "%");
                propertyInfo.SetValue(this, newValue);
                break;
            }

            case NumberValue nv when propertyInfo.PropertyType == typeof(float):
            {
                propertyInfo.SetValue(this, nv.Value);
                break;
            }

            case ColorValue cv when propertyInfo.PropertyType == typeof(Vector4):
            {
                propertyInfo.SetValue(this, cv.Vector);
                break;
            }

            case VectorValue vv when propertyInfo.PropertyType == typeof(Vector3):
            {
                propertyInfo.SetValue(this, new Vector3((float)vv.X, (float)vv.Y, (float)(vv.Z ?? 0)));
                break;
            }

            case StringValue sv when propertyInfo.PropertyType == typeof(string) ||
                                     propertyInfo.PropertyType == typeof(ReadOnlySpan<char>):
            {
                propertyInfo.SetValue(this, sv.Value);
                break;
            }

            case StringValue sv when propertyInfo.PropertyType == typeof(bool):
            {
                propertyInfo.SetValue(this, sv.Value == "true");
                break;
            }
        }
    }
}