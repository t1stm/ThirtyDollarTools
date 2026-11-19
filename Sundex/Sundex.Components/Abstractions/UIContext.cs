using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Debug;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;
using Sundex.Reactive;

namespace Sundex.Components.Abstractions;

[PreloadGraphicsContext]
public class UIContext : IGamePreloadable
{
    private static IAssetProvider _assetProvider = null!;
    private static IFontProvider _fontProvider = null!;
    private static ITextProvider _textProvider = null!;

    /// <summary>One provider per font name, shared by every label in that font: each uploads its own textures.</summary>
    private static readonly ConcurrentDictionary<string, ITextProvider> TextProviders = new();

    private static Func<string, ITextProvider> _createTextProvider = null!;
    protected readonly List<List<IRenderable>> LayeredRenderQueue = [];

    private readonly HashSet<UIElement> _updatingElements = [];
    public required Camera Camera { get; set; }
    public float ViewportWidth => Camera.Width;
    public float ViewportHeight => Camera.Height;

    private readonly HashSet<UIElement> _mediaWatchers = [];
    private (float Width, float Height) _mediaViewport;

    /// <summary>
    ///     Tab: moves keyboard focus to the next focusable, visible, enabled element under
    ///     <paramref name="root" />, in tree order, wrapping at the ends. While a modal is open, only
    ///     what is in the topmost one takes part: a dialog keeps Tab inside it.
    /// </summary>
    public void MoveFocus(UIElement root, bool backwards)
    {
        UIElement scope = root;
        Panels.ModalLayer? modal = null;
        FindModal(root, ref modal);
        if (modal != null) scope = modal;

        var order = new List<UIElement>();
        CollectFocusable(scope, order);
        if (order.Count == 0) return;
        var at = FocusedElement == null ? -1 : order.IndexOf(FocusedElement);
        var next = backwards ? at <= 0 ? order.Count - 1 : at - 1 : (at + 1) % order.Count;
        Focus(order[next]);
    }

    private static void FindModal(UIElement element, ref Panels.ModalLayer? found)
    {
        if (!element.Visible) return;
        if (element is Panels.ModalLayer modal) found = modal;
        if (element is not Panels.Panel panel) return;
        foreach (var child in panel.Children) FindModal(child, ref found);
    }

    private static void CollectFocusable(UIElement element, List<UIElement> order)
    {
        if (!element.Visible || element.Disabled) return;
        if (element.Focusable) order.Add(element);
        if (element is not Panels.Panel panel) return;
        foreach (var child in panel.Children) CollectFocusable(child, order);
    }

    /// <summary>Keeps an element whose rules have <c>media[…]</c> blocks re-styled as the viewport changes.</summary>
    public void WatchMedia(UIElement element) => _mediaWatchers.Add(element);

    public void UnwatchMedia(UIElement element) => _mediaWatchers.Remove(element);

    /// <summary>Re-styles the watched elements whose media blocks the last resize switched on or off.</summary>
    public void RefreshMedia()
    {
        var viewport = (ViewportWidth, ViewportHeight);
        if (viewport == _mediaViewport) return;
        _mediaViewport = viewport;
        foreach (var element in _mediaWatchers.ToArray()) element.RefreshMedia();
    }

    /// <summary>
    ///     Ratio of physical framebuffer pixels to logical UI units (monitor content scale).
    ///     Must be set by the owning scene whenever it divides window size by DPI scale for its Camera.
    /// </summary>
    public Vector2 PixelScale { get; set; } = Vector2.One;

    public IAssetProvider AssetProvider => _assetProvider;
    public IFontProvider FontProvider => _fontProvider;
    public ITextProvider TextProvider => _textProvider;

    /// <summary>The interface font, which <see cref="TextProvider" /> draws with.</summary>
    public const string DefaultFont = "Lato Bold";

    /// <summary>The shared provider for a bundled font (see <c>FontProvider</c>), created on first request.</summary>
    public ITextProvider GetTextProvider(string fontName) => TextProviders.GetOrAdd(fontName, _createTextProvider);
    public DeleteQueue DeleteQueue => _assetProvider.DeleteQueue;

    public Action<CursorType> RequestCursor { get; set; } = _ => { };

    public static void Preload(AssetProvider assetProvider)
    {
        // A reloaded stylesheet can drop rules that were deleted from it.
        UIElement.TrackPristineStyles = SundexDevelopment.Enabled;
        _assetProvider = assetProvider;
        _fontProvider = new FontProvider(assetProvider);
        _createTextProvider = name => new TextProvider(_assetProvider, _fontProvider, name);
        TextProviders.Clear();
        _textProvider = TextProviders.GetOrAdd(DefaultFont, _createTextProvider);

        // A font's glyph data takes a few hundred ms to build, on first use. Started here, the
        // build overlaps the rest of the boot instead of stalling the first label drawn.
        Task.Run(() => _textProvider.GetGlyph(" "));
    }

    /// <summary>
    ///     Injects mock provider instances for use in unit tests.
    ///     Should not be called in production code.
    /// </summary>
    /// <param name="fontTextProviders">
    ///     Providers <see cref="GetTextProvider" /> returns by font name. Any other font gets
    ///     <paramref name="textProvider" />.
    /// </param>
    internal static void InjectForTesting(
        IAssetProvider assetProvider,
        IFontProvider? fontProvider = null,
        ITextProvider? textProvider = null,
        IReadOnlyDictionary<string, ITextProvider>? fontTextProviders = null)
    {
        _assetProvider = assetProvider;
        _fontProvider = fontProvider!;
        _textProvider = textProvider!;
        _createTextProvider = name => fontTextProviders?.GetValueOrDefault(name) ?? textProvider!;
        TextProviders.Clear();
    }

    public void Clear()
    {
        foreach (var queue in LayeredRenderQueue) queue.Clear();
    }

    public void QueueRender(IRenderable renderable, int renderIndex, int queueIndex = -1)
    {
        while (LayeredRenderQueue.Count <= renderIndex)
            LayeredRenderQueue.Add([]);

        var queue = LayeredRenderQueue[renderIndex];

        // Check for duplicates to prevent same renderable from being queued multiple times in the same layer
        if (queue.Any(queued => ReferenceEquals(queued, renderable)))
            return;

        // Move semantics: a subtree re-parented to a new depth re-queues its renderables
        // at the new layer; drop the stale entry from whichever layer still holds it, or
        // the ghost keeps rendering after the element is removed (e.g. closing a modal).
        RemoveFromAnyLayer(renderable);

        if (queueIndex < 0 || queueIndex >= queue.Count)
        {
            queue.Add(renderable);
            return;
        }

        queue.Insert(queueIndex, renderable);
    }

    public int DequeueRender(IRenderable renderable, int index)
    {
        if (index >= 0 && index < LayeredRenderQueue.Count)
        {
            var queue = LayeredRenderQueue[index];

            for (var slot = 0; slot < queue.Count; slot++)
            {
                if (!ReferenceEquals(queue[slot], renderable)) continue;

                queue.RemoveAt(slot);
                return slot;
            }
        }

        // Stale index hint (element re-parented since it was queued): scan everything.
        RemoveFromAnyLayer(renderable);
        return -1;
    }

    private void RemoveFromAnyLayer(IRenderable renderable)
    {
        foreach (var layer in LayeredRenderQueue)
            for (var index = 0; index < layer.Count; index++)
            {
                if (!ReferenceEquals(layer[index], renderable)) continue;

                layer.RemoveAt(index);
                return;
            }
    }

    public void RegisterUpdate(UIElement element)
    {
        _updatingElements.Add(element);
    }

    public void UnregisterUpdate(UIElement element)
    {
        _updatingElements.Remove(element);
    }

    public void Render()
    {
        foreach (var element in _updatingElements) element.Update(this);

        var scissorOn = false;
        var layerIndex = 0;
        foreach (var queue in CollectionsMarshal.AsSpan(LayeredRenderQueue))
        {
            // Position in LayeredRenderQueue is the UIElement.Index depth layer - an
            // outsized count here pinpoints which nesting depth a draw-call spike lives
            // at (e.g. a still-unbatched fixed pool queued at that layer).
            if (RenderMarker.Enabled && queue.Count > 0)
                RenderMarker.Debug("UI render layer ", $"{layerIndex} ({queue.Count} renderables)",
                    MarkerType.Hidden);
            layerIndex++;

            if (queue.Count == 0) continue;
            var count = 0;

            foreach (var renderable in queue)
            {
                // Pooled elements are "hidden" by zeroing scale rather than dequeuing (see
                // Panel-pool patterns like TrackEditorView's NoteBlockPool) - cull those,
                // anything that has flagged itself invisible (a fully transparent fill) and
                // anything fully clipped away before it costs a bind/upload/draw.
                if (renderable is Renderable { IsVisible: false }) continue;
                if (renderable is Renderable { Scale: var scale } && (scale.X <= 0 || scale.Y <= 0))
                    continue;

                if ((renderable as IClippable)?.ClipRect is { } clip)
                {
                    if (clip.Z <= clip.X || clip.W <= clip.Y) continue;

                    if (!scissorOn) GL.Enable(EnableCap.ScissorTest);
                    scissorOn = true;
                    // GL.Scissor takes physical framebuffer pixels; clip/Camera are logical UI units, so
                    // scale by PixelScale. Origin is bottom-left; UI rects are top-left based.
                    GL.Scissor((int)(clip.X * PixelScale.X), (int)((Camera.Height - clip.W) * PixelScale.Y),
                        Math.Max(0, (int)((clip.Z - clip.X) * PixelScale.X)),
                        Math.Max(0, (int)((clip.W - clip.Y) * PixelScale.Y)));
                }
                else if (scissorOn)
                {
                    GL.Disable(EnableCap.ScissorTest);
                    scissorOn = false;
                }

                renderable.Render(Camera);
                count++;
            }

            if (RenderMarker.Enabled)
                RenderMarker.Debug("UI layer ", $"{layerIndex - 1} rendered {count} renderables",
                    MarkerType.Hidden);
        }

        if (scissorOn) GL.Disable(EnableCap.ScissorTest);
    }

    #region Pointer routing / focus

    private const long DoubleClickMs = 400;
    private const float DoubleClickSlopPx = 4;

    private readonly List<UIElement> _hoverChain = [];
    private readonly List<UIElement> _pressChain = [];
    private readonly List<UIElement> _newHoverCache = [];
    private readonly List<UIElement> _newPressCache = [];

    private UIElement? _lastPressTarget;
    private long _lastPressTime;
    private float _lastPressX;
    private float _lastPressY;

    /// <summary>
    ///     Where a <see cref="Panels.Portal" /> puts what it holds: a window-sized panel over the rest
    ///     of the tree, so a dialog or a popover declared deep in a page is neither clipped by the
    ///     page's scroller nor drawn under the player. The scene makes it; null until then.
    /// </summary>
    public Panels.Panel? OverlayHost { get; set; }

    /// <summary>The topmost element under the pointer (or the captured element during a drag).</summary>
    public UIElement? HoverTarget { get; private set; }

    /// <summary>
    ///     The cursor the pointer shows: the deepest hovered element that names one wins (a button's
    ///     label shows its button's pointer), and a disabled one shows the default. Read by the scene
    ///     each frame, so a button's pointer needs no <see cref="UIElement.Update" /> walk of the tree.
    /// </summary>
    public CursorType CursorAtPointer
    {
        get
        {
            for (var element = HoverTarget; element != null; element = element.Parent)
                if (element.IsHovered && (element.Disabled || element.Cursor != CursorType.Default))
                    return element.Disabled ? CursorType.Default : element.Cursor;
            return CursorType.Default;
        }
    }

    /// <summary>The element that received the last press; it owns the pointer until release.</summary>
    public UIElement? CapturedElement { get; private set; }

    /// <summary>The element receiving keyboard/text input. One per context.</summary>
    public UIElement? FocusedElement { get; private set; }

    public float PointerX { get; private set; }
    public float PointerY { get; private set; }
    public bool PointerDown { get; private set; }

    /// <summary>Whether either Shift key is held, polled by the host before pointer routing.</summary>
    public bool ShiftDown { get; set; }

    /// <summary>
    ///     Bumped once per right-button press (on the up-to-down transition). Because
    ///     <see cref="UIElement.HandleRightPress" /> is level-triggered, a handler whose
    ///     action isn't idempotent (adding something, rather than deleting what's under the
    ///     pointer) remembers this id and acts only when it changes.
    /// </summary>
    public int RightPressId { get; private set; }

    private bool _rightDown;

    /// <summary>The press <see cref="ConsumeRightPress" /> took; its held frames reach no element.</summary>
    private int _consumedRightPressId;

    /// <summary>
    ///     What had focus when the last key went down. Its character arrives right after it, in the
    ///     same batch of window events, so the next frame's pointer update ends the key's claim -
    ///     text with no key of its own (an input method's) then goes wherever focus is.
    /// </summary>
    private (UIElement? Focus, bool Pending) _keyFocus;

    /// <summary>
    ///     Swallows the rest of the current right-press. For a handler that removes itself on the
    ///     press - a menu closing - so the element that lay under it doesn't get the same,
    ///     still-held press on the next frame.
    /// </summary>
    public void ConsumeRightPress()
    {
        _consumedRightPressId = RightPressId;
    }

    /// <summary>
    ///     Routes a pointer update to the UI tree under <paramref name="root" />: resolves the
    ///     topmost hit (occlusion), maintains hover/pressed chains, capture, click
    ///     (press + release on the same element, bubbling to the first ancestor with a handler),
    ///     wheel routing, and click-to-focus. Called by <see cref="UIElement.Test" /> on root
    ///     elements; call directly with primitives in headless tests.
    /// </summary>
    public void UpdatePointer(UIElement root, float x, float y,
        bool isDown, bool wasPressed, bool wasReleased, Vector2 scrollDelta,
        bool isRightDown = false)
    {
        // Once a frame, before input: a resize that crossed a breakpoint re-styles here.
        RefreshMedia();
        _keyFocus = default;
        PointerX = x;
        PointerY = y;
        PointerDown = isDown;
        var rightPressed = isRightDown && !_rightDown;
        if (rightPressed) RightPressId++;
        _rightDown = isRightDown;

        // A capture owned by another UI tree makes this tree unreachable for the pointer.
        if (CapturedElement != null && !ReferenceEquals(RootOf(CapturedElement), root))
            return;

        var winner = CapturedElement ?? root.HitTest(x, y);

        // Multiple roots share this context and are tested one after another each frame;
        // when the pointer is over another tree, leave that tree's state alone.
        if (winner == null && HoverTarget != null && !ReferenceEquals(RootOf(HoverTarget), root))
            return;

        HoverTarget = winner;

        if (scrollDelta != Vector2.Zero)
            for (var element = winner; element != null; element = element.Parent)
                if (element.HandleScroll(scrollDelta))
                    break;

        if (wasPressed)
        {
            // The element that handles the press owns the capture (and future drag
            // updates); otherwise the raw hit winner does.
            UIElement? pressHandler = null;
            for (var element = winner; element != null; element = element.Parent)
                if (element.HandlePress(x, y))
                {
                    pressHandler = element;
                    break;
                }

            CapturedElement = pressHandler ?? winner;

            UIElement? focusable = null;
            for (var element = winner; element != null; element = element.Parent)
                if (element.Focusable)
                {
                    focusable = element;
                    break;
                }

            if (focusable != null) Focus(focusable);
            else if (FocusedElement != null && ReferenceEquals(RootOf(FocusedElement), root)) Blur();

            var now = Environment.TickCount64;
            var isDoubleClick = ReferenceEquals(winner, _lastPressTarget) &&
                                now - _lastPressTime <= DoubleClickMs &&
                                Math.Abs(x - _lastPressX) <= DoubleClickSlopPx &&
                                Math.Abs(y - _lastPressY) <= DoubleClickSlopPx;
            if (isDoubleClick)
            {
                for (var element = winner; element != null; element = element.Parent)
                    if (element.HandleDoublePress(x, y))
                        break;

                _lastPressTarget = null; // a third press starts a fresh cycle
            }
            else
            {
                _lastPressTarget = winner;
                _lastPressTime = now;
                _lastPressX = x;
                _lastPressY = y;
            }
        }

        // A right click (the press's first frame) goes to the nearest element that listens for one,
        // and the rest of that press is its: a context menu opening under the pointer.
        if (rightPressed)
            for (var element = winner; element != null; element = element.Parent)
                if (element.OnRightClick != null)
                {
                    if (!element.Disabled)
                    {
                        element.OnRightClick(element);
                        ConsumeRightPress();
                    }

                    break;
                }

        // Level-triggered, not edge-triggered: fires on every update while the right
        // button is held, so sweep gestures (erase everything crossed) work. Handlers
        // must be idempotent for a stationary pointer.
        if (isRightDown && RightPressId != _consumedRightPressId)
            for (var element = winner; element != null; element = element.Parent)
                if (element.HandleRightPress(x, y))
                    break;

        if (isDown && !wasPressed)
            CapturedElement?.HandlePointerDrag(x, y);

        if (wasReleased && CapturedElement != null)
        {
            var captured = CapturedElement;
            CapturedElement = null;

            if (!captured.HandlePointerRelease(x, y) && captured.ContainsPoint(x, y))
                for (var element = captured; element != null; element = element.Parent)
                    if (element.OnClick != null)
                    {
                        if (!element.Disabled) element.OnClick(element);
                        break;
                    }
        }

        ApplyPointerState();
    }

    private void ApplyPointerState()
    {
        _newHoverCache.Clear();
        for (var element = HoverTarget; element != null; element = element.Parent)
            if (element.ContainsPoint(PointerX, PointerY))
                _newHoverCache.Add(element);

        _newPressCache.Clear();
        if (PointerDown && CapturedElement != null)
            for (var element = CapturedElement; element != null; element = element.Parent)
                _newPressCache.Add(element);

        foreach (var element in _hoverChain)
            if (!_newHoverCache.Contains(element))
            {
                element.IsHovered = false;
                element.SyncPointerState();
                element.OnHoverExit?.Invoke(element);
            }

        foreach (var element in _pressChain)
            if (!_newPressCache.Contains(element))
            {
                element.IsPressed = false;
                element.SyncPointerState();
            }

        foreach (var element in _newPressCache)
        {
            element.IsPressed = true;
            element.SyncPointerState();
        }

        foreach (var element in _newHoverCache)
        {
            var entered = !element.IsHovered;
            element.IsHovered = true;
            element.SyncPointerState();
            if (entered) element.OnHoverEnter?.Invoke(element);
        }

        _hoverChain.Clear();
        _hoverChain.AddRange(_newHoverCache);
        _pressChain.Clear();
        _pressChain.AddRange(_newPressCache);
    }

    private static UIElement RootOf(UIElement element)
    {
        while (element.Parent != null) element = element.Parent;
        return element;
    }

    /// <summary>
    ///     Releases pointer/focus state held inside a subtree being removed from its
    ///     tree. Must run on removal: a capture left on a detached element blocks all
    ///     pointer input (its root no longer matches any live root), and focus keeps
    ///     routing keys to an element that is no longer on screen. Called by
    ///     Panel.RemoveChild.
    /// </summary>
    public void NotifyDetached(UIElement subtreeRoot)
    {
        if (IsInSubtree(CapturedElement, subtreeRoot)) CapturedElement = null;
        if (IsInSubtree(HoverTarget, subtreeRoot)) HoverTarget = null;
        if (IsInSubtree(_lastPressTarget, subtreeRoot)) _lastPressTarget = null;
        if (IsInSubtree(FocusedElement, subtreeRoot)) Blur();

        // Finding 2.3: a removed element leaves the hover and press chains quietly. Left in, the next
        // pointer update would call its OnHoverExit on a tree that may already be disposed.
        for (var index = _hoverChain.Count - 1; index >= 0; index--)
            if (IsInSubtree(_hoverChain[index], subtreeRoot))
            {
                _hoverChain[index].IsHovered = false;
                _hoverChain.RemoveAt(index);
            }

        for (var index = _pressChain.Count - 1; index >= 0; index--)
            if (IsInSubtree(_pressChain[index], subtreeRoot))
            {
                _pressChain[index].IsPressed = false;
                _pressChain.RemoveAt(index);
            }
    }

    private static bool IsInSubtree(UIElement? element, UIElement subtreeRoot)
    {
        for (; element != null; element = element.Parent)
            if (ReferenceEquals(element, subtreeRoot))
                return true;
        return false;
    }

    /// <summary>Moves keyboard focus to <paramref name="element" />, blurring the previous one.</summary>
    public void Focus(UIElement element)
    {
        if (ReferenceEquals(FocusedElement, element)) return;
        Blur();
        FocusedElement = element;
        element.NotifyFocusGained();
    }

    /// <summary>Clears keyboard focus.</summary>
    public void Blur()
    {
        var old = FocusedElement;
        FocusedElement = null;
        old?.NotifyFocusLost();
    }

    /// <summary>
    ///     Forwards a unicode text input event to the focused element - unless focus moved since
    ///     the key that typed it went down: a letter that opens a dialog with a name field must not
    ///     type itself into the field it just focused.
    /// </summary>
    public void DispatchTextInput(TextInputEventArgs args)
    {
        if (_keyFocus is { Pending: true } key && !ReferenceEquals(FocusedElement, key.Focus)) return;
        FocusedElement?.HandleTextInput(args);
    }

    /// <summary>
    ///     Forwards a key event to the focused element. Unhandled Escape blurs.
    /// </summary>
    /// <returns>True when the event was consumed by the UI.</returns>
    public bool DispatchKeyDown(KeyboardKeyEventArgs args)
    {
        _keyFocus = (FocusedElement, true);
        if (FocusedElement == null) return false;
        if (FocusedElement.HandleKeyDown(args)) return true;
        if (args.Key != Keys.Escape) return false;

        Blur();
        return true;
    }

    #endregion
}
