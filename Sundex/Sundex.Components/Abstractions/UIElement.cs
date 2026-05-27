using System.Reflection;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Core.Animations;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Abstractions;

public abstract class UIElement
{
    private readonly Dictionary<string, (PropertyInfo prop, object? value)> _baseSnapshot = new();

    protected UIElement(UIContext context)
    {
        Context = context;
        Computed = new ComputedRectangle(this)
        {
            OnUpdate = InvalidateCoordinates
        };
    }

    public abstract string Tag { get; }
    public UIContext Context { get; }

    public string ID { get; set; } = "";
    public HashSet<string> Classes { get; set; } = [];
    public virtual ComputedRectangle Computed { get; protected set; }

    [NamedSetting("animations")]
    public List<Animation> Animations
    {
        get;
        set
        {
            field = value;
            UpdateAnimationRegistrationState();
        }
    } = [];

    [NamedSetting("x")]
    public virtual LiteralOrComputable X
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("y")]
    public virtual LiteralOrComputable Y
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("width")]
    public virtual LiteralOrComputable Width
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("height")]
    public virtual LiteralOrComputable Height
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("index")] public virtual int Index { get; internal set; }

    /// <summary>Horizontal anchor point. "center" shifts left by width/2, "end" shifts left by width.</summary>
    [NamedSetting("anchor-x")]
    public Anchor AnchorX
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = Anchor.Start;

    /// <summary>Vertical anchor point. "center" shifts up by height/2, "end" shifts up by height.</summary>
    [NamedSetting("anchor-y")]
    public Anchor AnchorY
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = Anchor.Start;

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

    [NamedSetting("cursor")]
    public CursorType Cursor
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = CursorType.Default;

    public bool IsHovered { get; set; }
    public bool IsPressed { get; set; }

    public bool UpdateCursorOnHover
    {
        get => Cursor != CursorType.Default;
        set => Cursor = value ? CursorType.Pointer : CursorType.Default;
    }

    public bool NeedsLayout { get; protected set; } = true;

    protected StyleSheet? StoredStyleSheet { get; private set; }

    public UIState CurrentState
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            InvalidateStyle();
        }
    } = UIState.None;

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

    public virtual void StopRendering()
    {
    }

    public void AddAnimation(Animation animation)
    {
        Animations.Add(animation);
        UpdateAnimationRegistrationState();
    }

    public void RemoveAnimation(Animation animation)
    {
        Animations.Remove(animation);
        UpdateAnimationRegistrationState();
    }

    public void UpdateAnimationRegistrationState()
    {
        if (Animations.Count == 0) Context.UnregisterUpdate(this);
        else Context.RegisterUpdate(this);
    }

    protected void UpdateSetDirty<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        InvalidateLayout();
    }

    /// <summary>Returns the X pixel offset introduced by the <see cref="AnchorX" /> setting.</summary>
    public float AnchorOffsetX(float elementWidth)
    {
        return AnchorX switch
        {
            Anchor.Center => -elementWidth / 2f,
            Anchor.End => -elementWidth,
            _ => 0f
        };
    }

    /// <summary>Returns the Y pixel offset introduced by the <see cref="AnchorY" /> setting.</summary>
    public float AnchorOffsetY(float elementHeight)
    {
        return AnchorY switch
        {
            Anchor.Center => -elementHeight / 2f,
            Anchor.End => -elementHeight,
            _ => 0f
        };
    }

    /// <summary>
    ///     Tests mouse interaction with this element.
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
        IsHovered = mouseX >= absX && mouseX <= absX + Computed.Width &&
                    mouseY >= absY && mouseY <= absY + Computed.Height;

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
                CurrentState = UIState.None;
                return;

            case true when mouse.IsButtonPressed(MouseButton.Left):
                OnClick?.Invoke(this);
                break;

            case true when mouse.IsButtonDown(MouseButton.Left):
                IsPressed = true;
                break;
        }

        CurrentState = IsPressed ? UIState.Pressed : UIState.Hovered;
    }

    /// <summary>
    ///     Updates this element's state.
    /// </summary>
    /// <param name="uiContext">The current UI context.</param>
    public virtual void Update(UIContext uiContext)
    {
        if (IsHovered && Cursor != CursorType.Default)
            uiContext.RequestCursor(Cursor);
    }

    protected virtual void ApplyAnimations(params ReadOnlySpan<Renderable?> renderables)
    {
        var animations = CollectionsMarshal.AsSpan(Animations);
        foreach (var renderable in renderables) renderable?.UpdateModel(false, animations);
    }

    /// <summary>
    ///     Marks this element's layout as dirty and notifies the parent.
    /// </summary>
    public virtual void InvalidateLayout()
    {
        if (NeedsLayout) return;
        NeedsLayout = true;
        Parent?.InvalidateLayout();
    }

    /// <summary>
    ///     Marks coordinates as dirty, requiring a recalculation.
    /// </summary>
    public virtual void InvalidateCoordinates()
    {
        if (NeedsLayout) return;
        NeedsLayout = true;
    }

    /// <summary>
    ///     Performs layout calculations if needed.
    /// </summary>
    public virtual void Layout()
    {
        if (!NeedsLayout) return;
        Computed.UpdateAbsoluteBasedOnParent(this, Parent);
        DoLayout();
        NeedsLayout = false;
    }

    /// <summary>
    ///     Measures the desired size of this element given the available parent size.
    ///     Default implementation resolves literal/percentage sizes against parent size.
    ///     Containers can override to size to content when Width/Height are set to Auto.
    /// </summary>
    /// <param name="parentWidth">Available width from parent.</param>
    /// <param name="parentHeight">Available height from parent.</param>
    /// <returns>Tuple of desired (width, height).</returns>
    public virtual (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        var w = Width.Resolve(parentWidth);
        var h = Height.Resolve(parentHeight);
        return (w, h);
    }

    /// <summary>
    ///     Internal method to handle specific layout logic.
    /// </summary>
    protected virtual void DoLayout()
    {
    }

    /// <summary>
    ///     Renders this element to the specified context.
    /// </summary>
    /// <param name="uiContext">The UI context to render into.</param>
    public virtual void DrawTo(UIContext uiContext)
    {
        if (!Visible) return;
        Layout();
        DrawSelf(uiContext);
    }

    /// <summary>
    ///     Internal method to handle specific rendering logic.
    /// </summary>
    /// <param name="context">The UI context to render into.</param>
    protected abstract void DrawSelf(UIContext context);

    public virtual void ApplyStyleSheet(StyleSheet styleSheet)
    {
        StoredStyleSheet = styleSheet;
        var type = GetType();
        var properties = type.GetProperties();
        foreach (var propertyInfo in properties)
        {
            var attribute = propertyInfo.GetCustomAttribute<NamedSettingAttribute>();
            if (attribute is null) continue;

            SetNamedSetting(styleSheet, propertyInfo, attribute);
        }

        // Snapshot the post-base-style values for any property that has at least one state override,
        // so we can restore them without re-running ApplyStyleSheet (which would recreate renderables).
        _baseSnapshot.Clear();
        foreach (var propertyInfo in properties)
        {
            var attribute = propertyInfo.GetCustomAttribute<NamedSettingAttribute>();
            if (attribute is null) continue;

            var hasOverride =
                styleSheet.GetStateOverrideForTag(ID, "hovered")?.ContainsKey(attribute.Name) == true ||
                styleSheet.GetStateOverrideForTag(ID, "pressed")?.ContainsKey(attribute.Name) == true ||
                Classes.Any(cls =>
                    styleSheet.GetStateOverrideForTag(cls, "hovered")?.ContainsKey(attribute.Name) == true ||
                    styleSheet.GetStateOverrideForTag(cls, "pressed")?.ContainsKey(attribute.Name) == true) ||
                styleSheet.GetStateOverrideForTag(Tag, "hovered")?.ContainsKey(attribute.Name) == true ||
                styleSheet.GetStateOverrideForTag(Tag, "pressed")?.ContainsKey(attribute.Name) == true;

            if (hasOverride)
                _baseSnapshot[attribute.Name] = (propertyInfo, propertyInfo.GetValue(this));
        }
    }

    /// <summary>
    ///     Re-applies state styling on top of the snapshotted base values.
    ///     Called automatically when <see cref="CurrentState" /> changes.
    /// </summary>
    public virtual void InvalidateStyle()
    {
        if (StoredStyleSheet is null) return;
        foreach (var (prop, value) in _baseSnapshot.Values)
        {
            var oldValue = prop.GetValue(this);
            prop.SetValue(this, value);
            HandleRenderableSwap(oldValue, value, prop.Name);
        }

        var stateName = CurrentState switch
        {
            UIState.Hovered => "hovered",
            UIState.Pressed => "pressed",
            _ => null
        };

        if (stateName is not null)
            ApplyStateOverride(StoredStyleSheet, stateName);

        InvalidateLayout();
    }

    /// <summary>
    ///     Applies only the properties defined in the state override block for the given state,
    ///     on top of the already-applied base styles.
    /// </summary>
    public virtual void ApplyStateOverride(StyleSheet styleSheet, string state)
    {
        var type = GetType();
        var properties = type.GetProperties();
        foreach (var propertyInfo in properties)
        {
            var attribute = propertyInfo.GetCustomAttribute<NamedSettingAttribute>();
            if (attribute is null) continue;

            // Check ID, then classes, then tag — same priority as base styles
            var overrideValue = styleSheet.GetStateOverrideForTag(ID, state)
                                    ?.GetValueOrDefault(attribute.Name)
                                ?? Classes.Select(cls => styleSheet.GetStateOverrideForTag(cls, state)
                                        ?.GetValueOrDefault(attribute.Name))
                                    .FirstOrDefault(v => v is not null)
                                ?? styleSheet.GetStateOverrideForTag(Tag, state)
                                    ?.GetValueOrDefault(attribute.Name);

            if (overrideValue is not null)
                ApplyStyleValue(styleSheet, overrideValue, propertyInfo);
        }
    }

    private void SetNamedSetting(StyleSheet styleSheet, PropertyInfo propertyInfo,
        NamedSettingAttribute namedSettingAttribute)
    {
        ApplyStyleValue(styleSheet, styleSheet.GetStyleValueForTag(Tag, namedSettingAttribute.Name), propertyInfo);
        foreach (var cls in Classes)
            ApplyStyleValue(styleSheet, styleSheet.GetStyleValueForTag(cls, namedSettingAttribute.Name), propertyInfo);

        ApplyStyleValue(styleSheet, styleSheet.GetStyleValueForTag(ID, namedSettingAttribute.Name), propertyInfo);
    }

    protected virtual void ApplyStyleValue(StyleSheet styleSheet, IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue == null)
            return;

        // Capture old property value to allow generic post-set handling (e.g., renderable re-queueing)
        var oldValue = propertyInfo.GetValue(this);

        switch (styleValue)
        {
            case ArrayValue av when propertyInfo.PropertyType == typeof(List<Animation>):
            {
                var animations = new List<Animation>();
                foreach (var value in av.Values)
                    if (value is StringValue sv && styleSheet.ComputedAnimations.TryGetValue(sv.Value, out var anim))
                        animations.Add(anim);

                propertyInfo.SetValue(this, animations);
                break;
            }

            case NumberValue nv when propertyInfo.PropertyType == typeof(LiteralOrComputable):
            {
                var newValue = new LiteralOrComputable(nv.Value, nv.Unit is "%");
                propertyInfo.SetValue(this, newValue);
                HandleRenderableSwap(oldValue, newValue, propertyInfo.Name);
                break;
            }

            case NumberValue nv when propertyInfo.PropertyType == typeof(float):
            {
                propertyInfo.SetValue(this, nv.Value);
                HandleRenderableSwap(oldValue, nv.Value, propertyInfo.Name);
                break;
            }

            case ColorValue cv when propertyInfo.PropertyType == typeof(Vector4):
            {
                propertyInfo.SetValue(this, cv.Vector);
                HandleRenderableSwap(oldValue, cv.Vector, propertyInfo.Name);
                break;
            }

            case VectorValue vv when propertyInfo.PropertyType == typeof(Vector3):
            {
                propertyInfo.SetValue(this, new Vector3((float)vv.X, (float)vv.Y, (float)(vv.Z ?? 0)));
                HandleRenderableSwap(oldValue, propertyInfo.GetValue(this), propertyInfo.Name);
                break;
            }

            case StringValue sv when propertyInfo.PropertyType == typeof(Anchor):
            {
                Anchor? anchor = sv.Value switch
                {
                    "center" => Anchor.Center,
                    "end" => Anchor.End,
                    "start" => Anchor.Start,
                    _ => null
                };

                if (anchor is not null)
                    propertyInfo.SetValue(this, anchor.Value);
                HandleRenderableSwap(oldValue, propertyInfo.GetValue(this), propertyInfo.Name);
                break;
            }

            case StringValue sv when propertyInfo.PropertyType == typeof(string) ||
                                     propertyInfo.PropertyType == typeof(ReadOnlySpan<char>):
            {
                propertyInfo.SetValue(this, sv.Value);
                HandleRenderableSwap(oldValue, sv.Value, propertyInfo.Name);
                break;
            }

            case StringValue sv when propertyInfo.PropertyType == typeof(bool):
            {
                propertyInfo.SetValue(this, sv.Value == "true");
                HandleRenderableSwap(oldValue, propertyInfo.GetValue(this), propertyInfo.Name);
                break;
            }

            case StringValue sv when propertyInfo.PropertyType == typeof(CursorType):
            {
                if (Enum.TryParse<CursorType>(sv.Value, true, out var cursor))
                    propertyInfo.SetValue(this, cursor);
                break;
            }
        }
    }

    /// <summary>
    ///     Generic hook to keep render queue consistent when a property of type IRenderable changes.
    ///     Swaps the old renderable with the new one while preserving order within the same render layer (Index).
    /// </summary>
    protected void HandleRenderableSwap(object? oldValue, object? newValue, string? propertyName = null)
    {
        var queueIndex = -1;
        if (oldValue is IRenderable oldRenderable)
            queueIndex = Context.DequeueRender(oldRenderable, Index);

        if (newValue is not IRenderable newRenderable) return;

        if (queueIndex == -1 && !string.IsNullOrEmpty(propertyName))
        {
            var property = GetType().GetProperty(propertyName);
            var priorityAttr = property?.GetCustomAttribute<RenderPriorityAttribute>();
            if (priorityAttr != null)
                queueIndex = priorityAttr.Priority;
        }

        Context.QueueRender(newRenderable, Index, queueIndex);
    }
}