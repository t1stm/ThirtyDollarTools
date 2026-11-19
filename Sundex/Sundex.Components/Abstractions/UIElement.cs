using System.Reflection;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Core.Animations;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Abstractions;

public abstract partial class UIElement
{
    /// <summary>
    ///     Cache of every [NamedSetting] property on a type, so stylesheet lookups skip
    ///     reflection. The set is fixed per type and computed on first use.
    /// </summary>
    private static readonly Dictionary<Type, (PropertyInfo Property, NamedSettingAttribute Setting)[]> NamedSettings =
        new();

    private readonly Dictionary<string, (PropertyInfo prop, object? value)> _baseSnapshot = new();

    /// <summary>
    ///     What the styled properties held before any stylesheet touched them, keyed by
    ///     property name. First write wins, so an element styled by several sheets in turn
    ///     still remembers the value from before the first of them. Only populated while
    ///     <see cref="TrackPristineStyles" /> is on; null otherwise.
    /// </summary>
    private Dictionary<string, (PropertyInfo prop, object? value)>? _pristine;

    /// <summary>
    ///     Records what each styled property held before styling, so <see cref="ResetStyles" />
    ///     can revert a tree and let the whole cascade be re-run over it - which is how an
    ///     edited stylesheet drops rules deleted from it instead of leaving their last value
    ///     in place. Costs a dictionary per styled element, so only the hot-reload bootstrap
    ///     in Debug builds turns it on.
    ///     <para>
    ///         Only properties a sheet actually wrote are recorded, so a value assigned from
    ///         code after the style pass is left alone unless a sheet was also setting it.
    ///     </para>
    /// </summary>
    public static bool TrackPristineStyles { get; set; }

    /// <summary>
    ///     Whether the start pass in <see cref="DrawTo" /> has run for the current
    ///     <see cref="Animations" /> set. Animation clocks start on first draw, not when a
    ///     stylesheet assigns them, so a tree styled long before it is shown does not play
    ///     out unseen.
    /// </summary>
    private bool _animationsStarted;

    protected UIElement(UIContext context)
    {
        Context = context;
        Computed = new ComputedRectangle
        {
            OnUpdate = InvalidateCoordinates
        };
    }

    public abstract string Tag { get; }
    public UIContext Context { get; }

    public string ID { get; set; } = "";

    /// <summary>
    ///     The style classes on this element, in the order they are applied: a later class
    ///     overrides an earlier one's properties, and the id overrides all of them (see
    ///     <see cref="SetNamedSetting" />). Ordered, not a set, so a state class appended
    ///     by <see cref="SetClass" /> reliably wins over the base class it modifies.
    /// </summary>
    public List<string> Classes { get; set; } = [];
    public virtual ComputedRectangle Computed { get; protected set; }

    [NamedSetting("animations")]
    public List<Animation> Animations
    {
        get;
        set
        {
            field = value;
            // A fresh set has its own stopped clocks; the next DrawTo starts them.
            _animationsStarted = false;
            UpdateAnimationRegistrationState();
        }
    } = [];

    [NamedSetting("x")]
    public virtual LiteralOrComputable X
    {
        get;
        set => UpdateSetPosition(ref field, value);
    }

    [NamedSetting("y")]
    public virtual LiteralOrComputable Y
    {
        get;
        set => UpdateSetPosition(ref field, value);
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

    /// <summary>
    ///     Main-axis size dictated by a flex parent to a percent-sized child: its share
    ///     of the free space, recomputed on every flex layout pass. Kept apart from
    ///     <see cref="Width" />/<see cref="Height" /> so the declared percentage survives
    ///     resolution rather than being replaced by the resolved pixels. Null outside a
    ///     flex parent's layout.
    /// </summary>
    protected internal float? ParentAssignedWidth
    {
        get;
        set => UpdateSetPosition(ref field, value);
    }

    /// <inheritdoc cref="ParentAssignedWidth" />
    protected internal float? ParentAssignedHeight
    {
        get;
        set => UpdateSetPosition(ref field, value);
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
            // Showing or hiding changes what the parent's content measures.
            Parent?.InvalidateMeasure();
            InvalidateLayout();

            // The render queue is retained: only DrawTo queues and only StopRendering
            // dequeues, so toggling the flag has to do both. Re-show only inside a live
            // tree - a detached subtree queues when its container gets its own DrawTo.
            if (!value) StopRendering();
            else if (Parent is { Drawn: true }) DrawTo(Context);
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

    /// <summary>
    ///     True while this element's renderables are queued (DrawTo ran and StopRendering
    ///     hasn't). Panel.AddChild and the <see cref="Visible" /> setter consult it, so
    ///     composing a subtree while detached never queues renders - the whole subtree
    ///     queues when it is drawn into a live tree.
    /// </summary>
    protected internal bool Drawn { get; private set; }

    protected internal StyleSheet? StoredStyleSheet { get; private set; }

    public UIState CurrentState
    {
        get;
        private set
        {
            if (field == value) return;
            var hovered = field is UIState.Hovered or UIState.Pressed;
            field = value;
            InvalidateStyle();
            if (hovered != value is UIState.Hovered or UIState.Pressed) HoverChanged(!hovered);
        }
    } = UIState.None;

    /// <summary>
    ///     True while an ancestor marked <c>group = true</c> is hovered: the element then applies its
    ///     <c>state[group-hovered]</c> block over its own state (CSS <c>.group:hover .child</c>).
    /// </summary>
    public bool GroupHovered
    {
        get;
        internal set
        {
            if (field == value) return;
            field = value;
            InvalidateStyle();
        }
    }

    /// <summary>Called when the pointer starts or stops being on this element (pressing counts as on it).</summary>
    protected virtual void HoverChanged(bool hovered)
    {
    }

    public virtual UIElement? Parent
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Index = (field?.Index + 1 ?? 0) + ZIndex;
            UpdateEffectiveOpacity();
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

    /// <summary>A right click on this element or anything in it: the context menu's way in.</summary>
    public Action<UIElement>? OnRightClick { get; set; }

    /// <summary>
    ///     While true the element takes no clicks, shows no hover or press, and is styled by its
    ///     sheet's <c>state[disabled]</c> block instead. A click on it stops here rather than
    ///     falling through to an ancestor's handler.
    /// </summary>
    public bool Disabled
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SyncPointerState();
        }
    }

    /// <summary>
    ///     While true the element is styled by its sheet's <c>state[errored]</c> block: what it was
    ///     meant to show has failed (<see cref="Panels.Image" />). Ranks below disabled and above the
    ///     pointer states, so a passing pointer cannot clear it.
    /// </summary>
    public bool Errored
    {
        get;
        protected set
        {
            if (field == value) return;
            field = value;
            SyncPointerState();
        }
    }

    public Action<UIElement>? OnHoverEnter { get; set; }
    public Action<UIElement>? OnHoverExit { get; set; }
    public Action<UIElement>? OnFocus { get; set; }
    public Action<UIElement>? OnBlur { get; set; }

    /// <summary>Whether clicking this element gives it keyboard focus.</summary>
    public bool Focusable { get; set; }

    /// <summary>
    ///     Takes keyboard focus the first time it is laid out while visible, as HTML's
    ///     <c>autofocus</c> does: a name field that appears when a branch opens. Posting a focus
    ///     from logic runs before the frame's flush builds the branch, so the field isn't there yet.
    ///     Once per element: shown again after hiding, it doesn't take the focus back.
    /// </summary>
    public bool Autofocus { get; set; }

    private bool _autofocused;

    public bool IsFocused => ReferenceEquals(Context.FocusedElement, this);

    internal void NotifyFocusGained()
    {
        FocusGained();
        SyncPointerState();
        OnFocus?.Invoke(this);
    }

    internal void NotifyFocusLost()
    {
        FocusLost();
        SyncPointerState();
        OnBlur?.Invoke(this);
    }

    /// <summary>Component hook invoked when this element gains focus.</summary>
    protected virtual void FocusGained()
    {
    }

    /// <summary>Component hook invoked when this element loses focus.</summary>
    protected virtual void FocusLost()
    {
    }

    /// <summary>Receives unicode text input while focused.</summary>
    public virtual void HandleTextInput(TextInputEventArgs args)
    {
    }

    /// <summary>
    ///     Receives key events (including repeats) while focused.
    /// </summary>
    /// <returns>True when the key was consumed; unhandled Escape blurs the element.</returns>
    public virtual bool HandleKeyDown(KeyboardKeyEventArgs args)
    {
        return false;
    }

    /// <summary>
    ///     Receives scroll wheel input when hovered; unhandled events bubble to ancestors.
    /// </summary>
    /// <returns>True when the scroll was consumed.</returns>
    public virtual bool HandleScroll(Vector2 scrollDelta)
    {
        return false;
    }

    /// <summary>
    ///     Receives a press at the given UI coordinates; unhandled presses bubble to ancestors.
    ///     The handling element becomes the captured element and receives
    ///     <see cref="HandlePointerDrag" /> until release.
    /// </summary>
    /// <returns>True when the press was consumed.</returns>
    public virtual bool HandlePress(float x, float y)
    {
        return false;
    }

    /// <summary>Receives pointer movement while this element holds the capture.</summary>
    public virtual void HandlePointerDrag(float x, float y)
    {
    }

    /// <summary>
    ///     Receives the release that ends this element's capture, before any click. Return true to
    ///     say the press was a gesture of its own (a drag), so no click follows it.
    /// </summary>
    public virtual bool HandlePointerRelease(float x, float y)
    {
        return false;
    }

    /// <summary>
    ///     Receives the second press of a double-click (same element, within the
    ///     time/distance window), after the regular <see cref="HandlePress" />;
    ///     unhandled presses bubble to ancestors.
    /// </summary>
    /// <returns>True when consumed.</returns>
    public virtual bool HandleDoublePress(float x, float y)
    {
        return false;
    }

    /// <summary>
    ///     Fired on every pointer update while the right button is held (sweep
    ///     gestures); unhandled calls bubble to ancestors. Right presses never capture
    ///     the pointer and never produce clicks. Handlers must tolerate repeated calls
    ///     while the pointer rests on the same element.
    /// </summary>
    /// <returns>True when consumed.</returns>
    public virtual bool HandleRightPress(float x, float y)
    {
        return false;
    }

    public virtual void StopRendering()
    {
        Drawn = false;
    }

    /// <summary>
    ///     Assigns the clip rectangle (UI-space x1, y1, x2, y2) applied to this element's
    ///     renderables and its subtree; null removes clipping. Containers that clip
    ///     (ScrollView, TextInput) call this during layout - re-applied every layout pass,
    ///     so renderables swapped in between pick it up on the next one.
    /// </summary>
    public virtual void ApplyClip(Vector4i? clip)
    {
    }

    /// <summary>Intersects a clip rect with an optional outer clip rect.</summary>
    protected static Vector4i IntersectClip(Vector4i rect, Vector4i? outer)
    {
        if (outer is not { } outerClip) return rect;
        return new Vector4i(
            Math.Max(rect.X, outerClip.X), Math.Max(rect.Y, outerClip.Y),
            Math.Min(rect.Z, outerClip.Z), Math.Min(rect.W, outerClip.W));
    }

    public void AddAnimation(Animation animation)
    {
        Animations.Add(animation);
        UpdateAnimationRegistrationState();
        // Appending bypasses the Animations setter, so an already-drawn element has to
        // start this one here. Before the first draw, DrawTo starts it.
        if (_animationsStarted) animation.Start();
    }

    public void RemoveAnimation(Animation animation)
    {
        Animations.Remove(animation);
        UpdateAnimationRegistrationState();
    }

    public virtual void UpdateAnimationRegistrationState()
    {
        if (Animations.Count == 0) Context.UnregisterUpdate(this);
        else Context.RegisterUpdate(this);
    }

    /// <summary>Sets a property that can change what this element measures.</summary>
    protected void UpdateSetDirty<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        InvalidateMeasure();
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
    ///     Root elements (no parent) route the pointer through
    ///     <see cref="UIContext.UpdatePointer" />, which resolves the single topmost hit
    ///     (occlusion), capture, clicks, focus, and wheel routing centrally. Hover/pressed
    ///     state is applied there; overrides can still read the mouse for per-frame logic
    ///     (drags, custom gestures).
    /// </summary>
    /// <param name="mouse">The current mouse state.</param>
    /// <param name="scale">The UI scale.</param>
    public virtual void Test(MouseState mouse, Vector2 scale)
    {
        if (!Visible) return;

        if (Parent == null)
            Context.UpdatePointer(this,
                mouse.X, mouse.Y,
                mouse.IsButtonDown(MouseButton.Left),
                mouse.IsButtonPressed(MouseButton.Left),
                mouse.IsButtonReleased(MouseButton.Left),
                mouse.ScrollDelta,
                mouse.IsButtonDown(MouseButton.Right));
    }

    /// <summary>Whether the point (in UI coordinates) lies inside this element's bounds.</summary>
    public bool ContainsPoint(float x, float y)
    {
        var absX = Computed.AbsoluteX;
        var absY = Computed.AbsoluteY;
        return x >= absX && x <= absX + Computed.Width &&
               y >= absY && y <= absY + Computed.Height;
    }

    /// <summary>
    ///     Returns the topmost element in this subtree containing the point, or null.
    ///     "Topmost" follows render order: higher <see cref="Index" /> wins, later
    ///     candidates win ties. Containers override to include their children.
    /// </summary>
    public virtual UIElement? HitTest(float x, float y)
    {
        if (!Visible) return null;
        return ContainsPoint(x, y) ? this : null;
    }

    /// <summary>Recomputes <see cref="CurrentState" /> from the pointer flags. Called by the context.</summary>
    internal void SyncPointerState()
    {
        CurrentState = Disabled ? UIState.Disabled
            : Errored ? UIState.Errored
            : IsPressed ? UIState.Pressed
            : IsHovered ? UIState.Hovered
            : IsFocused ? UIState.Focused
            : UIState.None;
    }

    /// <summary>
    ///     Updates this element's state.
    /// </summary>
    /// <param name="uiContext">The current UI context.</param>
    public virtual void Update(UIContext uiContext)
    {
        if (IsHovered && !Disabled && Cursor != CursorType.Default)
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
        // Mid-layout, this element is about to lay out whatever asked anyway. Anything still
        // marked once the pass is over is caught by ChildrenNeedLayout and passed up then: marked
        // under a clean parent instead, it would be out of reach of every later pass.
        if (_laying || NeedsLayout) return;
        NeedsLayout = true;
        Parent?.InvalidateLayout();
    }

    /// <summary>
    ///     Marks coordinates as dirty, requiring a recalculation.
    ///     Notifies the parent (like <see cref="InvalidateLayout" />) so the dirty element
    ///     is reachable from the root's next Layout() pass.
    /// </summary>
    public virtual void InvalidateCoordinates() => InvalidateLayout();

    /// <summary>
    ///     Performs layout calculations if needed.
    /// </summary>
    public virtual void Layout()
    {
        if (!NeedsLayout) return;
        _laying = true;
        try
        {
            Computed.UpdateAbsoluteBasedOnParent(this, Parent);
            DoLayout();
        }
        finally
        {
            _laying = false;
            NeedsLayout = false;
        }

        if (Autofocus && !_autofocused && Visible)
        {
            _autofocused = true;
            Context.Focus(this);
        }

        // Something under here was invalidated after its turn in this pass: take another.
        if (ChildrenNeedLayout()) InvalidateLayout();
    }

    /// <summary>Whether a visible child is still marked for layout once this element's pass is over.</summary>
    protected virtual bool ChildrenNeedLayout() => false;
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
        var width = Width.Resolve(parentWidth);
        var height = Height.Resolve(parentHeight);
        return (width, height);
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
        Drawn = true;
        StartAnimations();
        Layout();
        DrawSelf(uiContext);
    }

    /// <summary>
    ///     Starts this element's animation clocks, once per <see cref="Animations" /> set.
    ///     Stylesheet animations arrive with a stopped stopwatch and are started here. Runs
    ///     once per set so that a finished non-looping animation, which stops its own clock,
    ///     is not resumed and its completion callback not refired on every draw.
    /// </summary>
    private void StartAnimations()
    {
        // Set even with no animations: AddAnimation on an already-drawn element starts its
        // own, and must not then be restarted by a later DrawTo.
        if (_animationsStarted) return;
        _animationsStarted = true;
        foreach (var animation in Animations) animation.Start();
    }

    /// <summary>
    ///     Internal method to handle specific rendering logic.
    /// </summary>
    /// <param name="context">The UI context to render into.</param>
    protected abstract void DrawSelf(UIContext context);

    /// <summary>Settings that set several others: applied before the ones they cover.</summary>
    private static readonly HashSet<string> Shorthands =
        ["margin", "padding", "gap", "spacing", "border-width", "overflow"];

    /// <summary>Returns the [NamedSetting] properties of an element type, computed once per type.</summary>
    private static (PropertyInfo Property, NamedSettingAttribute Setting)[] GetNamedSettings(Type type)
    {
        // ponytail: plain lock, component building is single-threaded. Swap for
        // ConcurrentDictionary if elements are ever built off the UI thread.
        lock (NamedSettings)
        {
            if (NamedSettings.TryGetValue(type, out var cached)) return cached;

            var settings = type.GetProperties()
                .Select(property => (Property: property, Setting: property.GetCustomAttribute<NamedSettingAttribute>()))
                .Where(pair => pair.Setting is not null)
                .Select(pair => (pair.Property, Setting: pair.Setting!))
                // Shorthands first, as in CSS: `margin = 8px; margin-top = 0px;` keeps the top at 0
                // whatever order reflection lists the properties in.
                .OrderBy(pair => Shorthands.Contains(pair.Setting.Name) ? 0 : 1)
                .ToArray();

            NamedSettings[type] = settings;
            return settings;
        }
    }

    public virtual void ApplyStyleSheet(StyleSheet styleSheet)
    {
        ApplyOwnStyle(styleSheet);
    }

    /// <summary>
    ///     Adds or removes a style class and re-styles this element from the sheet it was
    ///     last given - how a runtime state (a selected row, the active tool) is expressed,
    ///     instead of reaching into the element's renderables from code. Only this element
    ///     is re-styled, not its subtree, so a container can carry a state class without
    ///     re-running reflection over everything inside it.
    ///     <para>
    ///         A class only sets properties; removing one does not unset them. A modifier
    ///         class must therefore override a property an earlier class (or the element's
    ///         tag) also declares, so removing the modifier restores that base value -
    ///         <c>class track-row { background = panel }</c> under
    ///         <c>class track-row-selected { background = row_selected }</c>.
    ///     </para>
    /// </summary>
    /// <returns>True when the class set actually changed.</returns>
    public bool SetClass(string name, bool enabled)
    {
        if (enabled)
        {
            if (Classes.Contains(name)) return false;
            Classes.Add(name);
        }
        else if (!Classes.Remove(name))
        {
            return false;
        }

        Restyle();
        return true;
    }

    /// <summary>
    ///     Styles this element again from the sheet it was last given, as its classes, state and the
    ///     viewport now select: a property no rule sets any more goes back to what it was before the
    ///     sheet touched it, and one that code changed since the last pass is left as code set it
    ///     (an inline style beats a sheet, as in CSS). Only this element, not its subtree.
    /// </summary>
    public void Restyle()
    {
        if (StoredStyleSheet is not { } sheet) return;
        ApplyOwnStyle(sheet, true);
        // The base pass overwrites whatever the current hover/press override had put on
        // top, so reapply it from the snapshot ApplyOwnStyle has just retaken.
        InvalidateStyle();
    }

    /// <summary>The <c>state[...]</c> blocks a sheet can give an element, one per non-idle <see cref="UIState" />.</summary>
    private static readonly string[] StateNames = ["hovered", "pressed", "disabled", "errored", "focused", "group-hovered"];

    /// <summary>
    ///     What the sheet did to each property it set: the value from before its first write, and the
    ///     value of its last. A current value unlike the last written one was set by code since.
    /// </summary>
    private Dictionary<string, (PropertyInfo Property, object? Before, object? Styled)>? _styled;

    private void ApplyOwnStyle(StyleSheet styleSheet, bool restyle = false)
    {
        // A state override still on top would be read as the base below and snapshotted as it:
        // restyled while disabled, an element kept its disabled opacity once enabled again.
        if (restyle) RestoreBase();
        StoredStyleSheet = styleSheet;
        var properties = GetNamedSettings(GetType());
        _styled ??= [];
        _mediaKey = 0;
        _usesMedia = false;
        // Up front, not per property: the loop skips settings no rule sets right now, and a media
        // block that doesn't hold at this size sets nothing yet still has to be watched.
        WatchesMedia(styleSheet);
        if (TrackPristineStyles) _pristine ??= [];

        foreach (var (propertyInfo, attribute) in properties)
        {
            // A byref-like setting (Label's ReadOnlySpan<char> value) cannot be read back through
            // reflection at all, so it is applied without being recorded.
            var trackable = propertyInfo is { CanRead: true, CanWrite: true } && !propertyInfo.PropertyType.IsByRefLike;
            var known = false;
            (PropertyInfo Property, object? Before, object? Styled) record = default;
            if (trackable) known = _styled.TryGetValue(propertyInfo.Name, out record);
            // Nothing to apply and nothing to undo, which is most settings of most elements: skip
            // the reflection reads below, the bulk of styling's cost.
            if (!known && !AnyRuleSets(styleSheet, attribute.Name)) continue;

            object? current = null;
            if (trackable)
            {
                current = propertyInfo.GetValue(this);
                // Set by code since the sheet last wrote it: code wins.
                if (known && !Equals(current, record.Styled)) continue;
            }

            // Read before applying, and only the first time: an element inside an imported
            // component is styled by that component's sheet and then again by its host's, and
            // what has to be remembered is the value from before either of them ran.
            var pristine = TrackPristineStyles && trackable && !_pristine!.ContainsKey(propertyInfo.Name);
            var matched = SetNamedSetting(styleSheet, propertyInfo, attribute);
            if (pristine && matched) _pristine![propertyInfo.Name] = (propertyInfo, current);
            if (!trackable) continue;

            if (matched)
            {
                _styled[propertyInfo.Name] = (propertyInfo, known ? record.Before : current, propertyInfo.GetValue(this));
            }
            else if (known && restyle)
            {
                // No rule sets it any more: back to what it was before the sheet.
                propertyInfo.SetValue(this, record.Before);
                HandleRenderableSwap(current, record.Before, propertyInfo.Name);
                _styled.Remove(propertyInfo.Name);
            }
        }

        if (_usesMedia) Context.WatchMedia(this);

        // Snapshot the post-base-style values for any property that has at least one state override,
        // so we can restore them without re-running ApplyStyleSheet (which would recreate renderables).
        // The state blocks are looked up once per element, not once per property.
        _baseSnapshot.Clear();
        List<Dictionary<string, IStyleValue>>? overrides = null;
        foreach (var state in StateNames)
        {
            AddOverride(ref overrides, styleSheet.GetStateOverrideForTag(ID, state));
            foreach (var cls in Classes) AddOverride(ref overrides, styleSheet.GetStateOverrideForTag(cls, state));
            AddOverride(ref overrides, styleSheet.GetStateOverrideForTag(Tag, state));
        }

        if (overrides == null) return;
        foreach (var (propertyInfo, attribute) in properties)
        foreach (var block in overrides)
        {
            if (!block.ContainsKey(attribute.Name)) continue;
            _baseSnapshot[attribute.Name] = (propertyInfo, propertyInfo.GetValue(this));
            break;
        }
    }

    private static void AddOverride(ref List<Dictionary<string, IStyleValue>>? overrides,
        Dictionary<string, IStyleValue>? block)
    {
        if (block != null) (overrides ??= []).Add(block);
    }

    /// <summary>Whether a rule of the sheet sets the setting here: a tag, class or id rule, or a media block that holds.</summary>
    private bool AnyRuleSets(StyleSheet styleSheet, string setting)
    {
        if (styleSheet.GetStyleValueForTag(Tag, setting) is not null) return true;
        foreach (var cls in Classes)
            if (styleSheet.GetStyleValueForTag(cls, setting) is not null)
                return true;
        if (styleSheet.GetStyleValueForTag(ID, setting) is not null) return true;

        if (MediaSets(styleSheet, Tag, setting) || MediaSets(styleSheet, ID, setting)) return true;
        foreach (var cls in Classes)
            if (MediaSets(styleSheet, cls, setting))
                return true;
        return false;
    }

    /// <summary>
    ///     Re-applies state styling on top of the snapshotted base values.
    ///     Called automatically when <see cref="CurrentState" /> changes.
    /// </summary>
    public virtual void InvalidateStyle()
    {
        if (StoredStyleSheet is null) return;
        RestoreBase();

        var stateName = CurrentState switch
        {
            UIState.Hovered => "hovered",
            UIState.Pressed => "pressed",
            UIState.Disabled => "disabled",
            UIState.Errored => "errored",
            UIState.Focused => "focused",
            _ => null
        };

        if (stateName is not null)
            ApplyStateOverride(StoredStyleSheet, stateName);
        if (GroupHovered)
            ApplyStateOverride(StoredStyleSheet, "group-hovered");

        InvalidateLayout();
    }

    /// <summary>Takes the state overrides off again: each overridden property back to its base value.</summary>
    private void RestoreBase()
    {
        foreach (var (prop, value) in _baseSnapshot.Values)
        {
            var oldValue = prop.GetValue(this);
            prop.SetValue(this, value);
            HandleRenderableSwap(oldValue, value, prop.Name);
            Restyled(prop);
        }
    }

    /// <summary>
    ///     Applies only the properties defined in the state override block for the given state,
    ///     on top of the already-applied base styles.
    /// </summary>
    public virtual void ApplyStateOverride(StyleSheet styleSheet, string state)
    {
        foreach (var (propertyInfo, attribute) in GetNamedSettings(GetType()))
        {
            // ID, then classes, then tag - the base styles' priority, and as there the last
            // class that sets the property wins
            var overrideValue = styleSheet.GetStateOverrideForTag(ID, state)
                                    ?.GetValueOrDefault(attribute.Name)
                                ?? Classes.Select(cls => styleSheet.GetStateOverrideForTag(cls, state)
                                        ?.GetValueOrDefault(attribute.Name))
                                    .LastOrDefault(candidate => candidate is not null)
                                ?? styleSheet.GetStateOverrideForTag(Tag, state)
                                    ?.GetValueOrDefault(attribute.Name);

            if (overrideValue is not null)
            {
                ApplyStyleValue(styleSheet, overrideValue, propertyInfo);
                Restyled(propertyInfo);
            }
        }
    }

    /// <summary>A write by the style machinery itself (a state override), so not one by code.</summary>
    private void Restyled(PropertyInfo property)
    {
        if (_styled != null && _styled.TryGetValue(property.Name, out var record))
            _styled[property.Name] = record with { Styled = property.GetValue(this) };
    }

    /// <returns>Whether any rule in the sheet addressed this property.</returns>
    private bool SetNamedSetting(StyleSheet styleSheet, PropertyInfo propertyInfo,
        NamedSettingAttribute namedSettingAttribute)
    {
        var matched = false;

        var tagValue = styleSheet.GetStyleValueForTag(Tag, namedSettingAttribute.Name);
        matched |= tagValue is not null;
        ApplyStyleValue(styleSheet, tagValue, propertyInfo);

        foreach (var cls in Classes)
        {
            var classValue = styleSheet.GetStyleValueForTag(cls, namedSettingAttribute.Name);
            matched |= classValue is not null;
            ApplyStyleValue(styleSheet, classValue, propertyInfo);
        }

        var idValue = styleSheet.GetStyleValueForTag(ID, namedSettingAttribute.Name);
        matched |= idValue is not null;
        ApplyStyleValue(styleSheet, idValue, propertyInfo);

        // media[…] blocks last, in the same tag, class, id order: a matching one beats the base rule.
        matched |= ApplyMedia(styleSheet, Tag, propertyInfo, namedSettingAttribute.Name);
        foreach (var cls in Classes) matched |= ApplyMedia(styleSheet, cls, propertyInfo, namedSettingAttribute.Name);
        matched |= ApplyMedia(styleSheet, ID, propertyInfo, namedSettingAttribute.Name);

        return matched;
    }

    /// <summary>
    ///     Puts every property a stylesheet wrote back the way it was before any of them
    ///     ran, so the whole cascade can be applied again from nothing. Needs
    ///     <see cref="TrackPristineStyles" /> to have been on while the tree was styled;
    ///     a no-op otherwise.
    ///     <para>
    ///         A whole-tree step: run it over the tree before re-applying any sheet, never
    ///         between two applies - an element styled by an imported component's sheet and
    ///         then by its host's would lose the first pass.
    ///     </para>
    /// </summary>
    public virtual void ResetStyles()
    {
        if (_pristine is null) return;

        foreach (var (property, value) in _pristine.Values)
        {
            var oldValue = property.GetValue(this);
            property.SetValue(this, value);
            HandleRenderableSwap(oldValue, value, property.Name);
        }

        _pristine.Clear();
        _baseSnapshot.Clear();
        // What the sheet wrote is undone, so nothing it wrote can count as set by code any more.
        _styled?.Clear();
    }

    protected virtual void ApplyStyleValue(StyleSheet styleSheet, IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue == null)
            return;

        // Capture old property value to allow generic post-set handling (e.g., renderable re-queueing)
        var oldValue = propertyInfo.GetValue(this);

        switch (styleValue)
        {
            case ArrayValue arrayValue when propertyInfo.PropertyType == typeof(List<Animation>):
            {
                var animations = new List<Animation>();
                foreach (var value in arrayValue.Values)
                    if (value is StringValue stringValue && styleSheet.ComputedAnimations.TryGetValue(stringValue.Value, out var anim))
                        // Per element, not the sheet's shared object: an animation owns a
                        // mutable stopwatch, so sharing it syncs every element matching the
                        // rule to one clock.
                        animations.Add(anim.CreateInstance());

                propertyInfo.SetValue(this, animations);
                break;
            }

            case ArrayValue arrayValue when propertyInfo.PropertyType == typeof(Vector4[]):
            {
                propertyInfo.SetValue(this, arrayValue.Values.OfType<ColorValue>().Select(color => color.Vector).ToArray());
                break;
            }

            case NumberValue numberValue when propertyInfo.PropertyType == typeof(LiteralOrComputable):
            {
                var newValue = new LiteralOrComputable(numberValue.Value, numberValue.Unit is "%");
                propertyInfo.SetValue(this, newValue);
                HandleRenderableSwap(oldValue, newValue, propertyInfo.Name);
                break;
            }

            case StringValue stringValue when propertyInfo.PropertyType == typeof(LiteralOrComputable):
            {
                // auto, 50%, 12px: a NumberValue can't say auto.
                propertyInfo.SetValue(this, LiteralOrComputable.Parse(stringValue.Value));
                break;
            }

            case NumberValue numberValue when propertyInfo.PropertyType == typeof(int):
            {
                propertyInfo.SetValue(this, (int)numberValue.Value);
                break;
            }

            case NumberValue numberValue when propertyInfo.PropertyType == typeof(double):
            {
                propertyInfo.SetValue(this, (double)numberValue.Value);
                break;
            }

            case NumberValue numberValue when propertyInfo.PropertyType == typeof(float):
            {
                propertyInfo.SetValue(this, numberValue.Value);
                HandleRenderableSwap(oldValue, numberValue.Value, propertyInfo.Name);
                break;
            }

            case ColorValue colorValue when propertyInfo.PropertyType == typeof(Vector4):
            {
                propertyInfo.SetValue(this, colorValue.Vector);
                HandleRenderableSwap(oldValue, colorValue.Vector, propertyInfo.Name);
                break;
            }

            case VectorValue vectorValue when propertyInfo.PropertyType == typeof(Vector3):
            {
                propertyInfo.SetValue(this, new Vector3((float)vectorValue.X, (float)vectorValue.Y, (float)(vectorValue.Z ?? 0)));
                HandleRenderableSwap(oldValue, propertyInfo.GetValue(this), propertyInfo.Name);
                break;
            }

            case StringValue stringValue when propertyInfo.PropertyType == typeof(string) ||
                                     propertyInfo.PropertyType == typeof(ReadOnlySpan<char>):
            {
                propertyInfo.SetValue(this, stringValue.Value);
                HandleRenderableSwap(oldValue, stringValue.Value, propertyInfo.Name);
                break;
            }

            case StringValue stringValue when propertyInfo.PropertyType == typeof(bool):
            {
                propertyInfo.SetValue(this, stringValue.Value == "true");
                HandleRenderableSwap(oldValue, propertyInfo.GetValue(this), propertyInfo.Name);
                break;
            }

            // Covers every enum-typed setting: the value is parsed as a member name,
            // case-insensitively.
            case StringValue stringValue when propertyInfo.PropertyType.IsEnum:
            {
                if (Enum.TryParse(propertyInfo.PropertyType, stringValue.Value, true, out var parsed))
                    propertyInfo.SetValue(this, parsed);
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

        // Only an element that is currently rendering may put a renderable into the queue;
        // a swap on a detached or stopped element would otherwise resurrect its plane and
        // paint it forever. queueIndex >= 0 means the old renderable really was queued,
        // which covers elements kept live by something other than DrawTo.
        if (queueIndex == -1 && !Drawn) return;

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