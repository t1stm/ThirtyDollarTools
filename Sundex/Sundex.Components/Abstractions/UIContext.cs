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

namespace Sundex.Components.Abstractions;

[PreloadGraphicsContext]
public class UIContext : IGamePreloadable
{
    private static IAssetProvider _assetProvider = null!;
    private static IFontProvider _fontProvider = null!;
    private static ITextProvider _textProvider = null!;
    protected readonly List<List<IRenderable>> LayeredRenderQueue = [];

    private readonly HashSet<UIElement> _updatingElements = [];
    public required Camera Camera { get; set; }
    public float ViewportWidth => Camera.Width;
    public float ViewportHeight => Camera.Height;

    /// <summary>
    ///     Ratio of physical framebuffer pixels to logical UI units (monitor content scale).
    ///     Must be set by the owning scene whenever it divides window size by DPI scale for its Camera.
    /// </summary>
    public Vector2 PixelScale { get; set; } = Vector2.One;

    public IAssetProvider AssetProvider => _assetProvider;
    public IFontProvider FontProvider => _fontProvider;
    public ITextProvider TextProvider => _textProvider;
    public DeleteQueue DeleteQueue => _assetProvider.DeleteQueue;

    public Action<CursorType> RequestCursor { get; set; } = _ => { };

    public static void Preload(AssetProvider assetProvider)
    {
#if DEBUG
        // Makes a re-applied stylesheet able to drop rules that were deleted from it, which
        // hot reload needs and nothing else does. Set here rather than by the engine:
        // Sundex.Engine does not reference this assembly.
        UIElement.TrackPristineStyles = true;
#endif
        _assetProvider = assetProvider;
        _fontProvider = new FontProvider(assetProvider);
        _textProvider = new TextProvider(_assetProvider, _fontProvider, "Lato Bold");
    }

    /// <summary>
    ///     Injects mock provider instances for use in unit tests.
    ///     Should not be called in production code.
    /// </summary>
    internal static void InjectForTesting(
        IAssetProvider assetProvider,
        IFontProvider? fontProvider = null,
        ITextProvider? textProvider = null)
    {
        _assetProvider = assetProvider;
        _fontProvider = fontProvider!;
        _textProvider = textProvider!;
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
        if (queue.Any(r => ReferenceEquals(r, renderable)))
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

            for (var i = 0; i < queue.Count; i++)
            {
                if (!ReferenceEquals(queue[i], renderable)) continue;

                queue.RemoveAt(i);
                return i;
            }
        }

        // Stale index hint (element re-parented since it was queued): scan everything.
        RemoveFromAnyLayer(renderable);
        return -1;
    }

    private void RemoveFromAnyLayer(IRenderable renderable)
    {
        foreach (var layer in LayeredRenderQueue)
            for (var i = 0; i < layer.Count; i++)
            {
                if (!ReferenceEquals(layer[i], renderable)) continue;

                layer.RemoveAt(i);
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
            if (queue.Count > 0)
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

    /// <summary>The topmost element under the pointer (or the captured element during a drag).</summary>
    public UIElement? HoverTarget { get; private set; }

    /// <summary>The element that received the last press; it owns the pointer until release.</summary>
    public UIElement? CapturedElement { get; private set; }

    /// <summary>The element receiving keyboard/text input. One per context.</summary>
    public UIElement? FocusedElement { get; private set; }

    public float PointerX { get; private set; }
    public float PointerY { get; private set; }
    public bool PointerDown { get; private set; }

    /// <summary>
    ///     Bumped once per right-button press (on the up-to-down transition). Because
    ///     <see cref="UIElement.HandleRightPress" /> is level-triggered, a handler whose
    ///     action isn't idempotent (adding something, rather than deleting what's under the
    ///     pointer) remembers this id and acts only when it changes.
    /// </summary>
    public int RightPressId { get; private set; }

    private bool _rightDown;

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
        PointerX = x;
        PointerY = y;
        PointerDown = isDown;
        if (isRightDown && !_rightDown) RightPressId++;
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
            for (var e = winner; e != null; e = e.Parent)
                if (e.HandleScroll(scrollDelta))
                    break;

        if (wasPressed)
        {
            // The element that handles the press owns the capture (and future drag
            // updates); otherwise the raw hit winner does.
            UIElement? pressHandler = null;
            for (var e = winner; e != null; e = e.Parent)
                if (e.HandlePress(x, y))
                {
                    pressHandler = e;
                    break;
                }

            CapturedElement = pressHandler ?? winner;

            UIElement? focusable = null;
            for (var e = winner; e != null; e = e.Parent)
                if (e.Focusable)
                {
                    focusable = e;
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
                for (var e = winner; e != null; e = e.Parent)
                    if (e.HandleDoublePress(x, y))
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

        // Level-triggered, not edge-triggered: fires on every update while the right
        // button is held, so sweep gestures (erase everything crossed) work. Handlers
        // must be idempotent for a stationary pointer.
        if (isRightDown)
            for (var e = winner; e != null; e = e.Parent)
                if (e.HandleRightPress(x, y))
                    break;

        if (isDown && !wasPressed)
            CapturedElement?.HandlePointerDrag(x, y);

        if (wasReleased && CapturedElement != null)
        {
            var captured = CapturedElement;
            CapturedElement = null;

            if (captured.ContainsPoint(x, y))
                for (var e = captured; e != null; e = e.Parent)
                    if (e.OnClick != null)
                    {
                        e.OnClick(e);
                        break;
                    }
        }

        ApplyPointerState();
    }

    private void ApplyPointerState()
    {
        _newHoverCache.Clear();
        for (var e = HoverTarget; e != null; e = e.Parent)
            if (e.ContainsPoint(PointerX, PointerY))
                _newHoverCache.Add(e);

        _newPressCache.Clear();
        if (PointerDown && CapturedElement != null)
            for (var e = CapturedElement; e != null; e = e.Parent)
                _newPressCache.Add(e);

        foreach (var e in _hoverChain)
            if (!_newHoverCache.Contains(e))
            {
                e.IsHovered = false;
                e.SyncPointerState();
                e.OnHoverExit?.Invoke(e);
            }

        foreach (var e in _pressChain)
            if (!_newPressCache.Contains(e))
            {
                e.IsPressed = false;
                e.SyncPointerState();
            }

        foreach (var e in _newPressCache)
        {
            e.IsPressed = true;
            e.SyncPointerState();
        }

        foreach (var e in _newHoverCache)
        {
            var entered = !e.IsHovered;
            e.IsHovered = true;
            e.SyncPointerState();
            if (entered) e.OnHoverEnter?.Invoke(e);
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

    /// <summary>Forwards a unicode text input event to the focused element.</summary>
    public void DispatchTextInput(TextInputEventArgs e)
    {
        FocusedElement?.HandleTextInput(e);
    }

    /// <summary>
    ///     Forwards a key event to the focused element. Unhandled Escape blurs.
    /// </summary>
    /// <returns>True when the event was consumed by the UI.</returns>
    public bool DispatchKeyDown(KeyboardKeyEventArgs e)
    {
        if (FocusedElement == null) return false;
        if (FocusedElement.HandleKeyDown(e)) return true;
        if (e.Key != Keys.Escape) return false;

        Blur();
        return true;
    }

    #endregion
}