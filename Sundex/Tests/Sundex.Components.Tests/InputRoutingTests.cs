using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

// Drives UIContext.UpdatePointer / Dispatch* directly with primitives,
// since OpenTK's MouseState cannot be constructed in tests.
public class InputRoutingTests
{
    private static void Move(UIContext ctx, UIElement root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, false, false, false, Vector2.Zero);
    }

    private static void Press(UIContext ctx, UIElement root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, true, true, false, Vector2.Zero);
    }

    private static void Drag(UIContext ctx, UIElement root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, true, false, false, Vector2.Zero);
    }

    private static void Release(UIContext ctx, UIElement root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, false, false, true, Vector2.Zero);
    }

    private static void Scroll(UIContext ctx, UIElement root, float x, float y, float deltaY)
    {
        ctx.UpdatePointer(root, x, y, false, false, false, new Vector2(0, deltaY));
    }

    private static (TestUIContext ctx, Panel root) NewTree()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        return (ctx, root);
    }

    [Fact]
    public void Occlusion_TopmostOverlappingSiblingWins()
    {
        var (ctx, root) = NewTree();
        var bottom = new Panel(ctx) { X = 0, Y = 0, Width = 200, Height = 200 };
        var top = new Panel(ctx) { X = 100, Y = 100, Width = 200, Height = 200 };
        root.Children = [bottom, top];
        root.Layout();

        // Overlap region
        Move(ctx, root, 150, 150);

        Assert.Same(top, ctx.HoverTarget);
        Assert.True(top.IsHovered);
        Assert.False(bottom.IsHovered);

        // Bottom-only region
        Move(ctx, root, 50, 50);
        Assert.Same(bottom, ctx.HoverTarget);
        Assert.True(bottom.IsHovered);
        Assert.False(top.IsHovered);
    }

    [Fact]
    public void Hover_AncestorChainIsHovered()
    {
        var (ctx, root) = NewTree();
        var child = new Panel(ctx) { X = 10, Y = 10, Width = 100, Height = 100 };
        root.Children = [child];
        root.Layout();

        Move(ctx, root, 50, 50);

        Assert.Same(child, ctx.HoverTarget);
        Assert.True(child.IsHovered);
        Assert.True(root.IsHovered);
    }

    [Fact]
    public void Hover_EnterAndExitCallbacksFireOnce()
    {
        var (ctx, root) = NewTree();
        var enters = 0;
        var exits = 0;
        var child = new Panel(ctx)
        {
            X = 10,
            Y = 10,
            Width = 100,
            Height = 100,
            OnHoverEnter = _ => enters++,
            OnHoverExit = _ => exits++
        };
        root.Children = [child];
        root.Layout();

        Move(ctx, root, 50, 50);
        Move(ctx, root, 60, 60);
        Assert.Equal(1, enters);
        Assert.Equal(0, exits);

        Move(ctx, root, 500, 500);
        Assert.Equal(1, exits);
    }

    [Fact]
    public void Capture_PressedElementStaysPressedOffBounds()
    {
        var (ctx, root) = NewTree();
        var child = new Panel(ctx) { X = 10, Y = 10, Width = 100, Height = 100 };
        root.Children = [child];
        root.Layout();

        Press(ctx, root, 50, 50);
        Assert.Same(child, ctx.CapturedElement);
        Assert.True(child.IsPressed);

        // Drag far outside the element: capture keeps it pressed.
        Drag(ctx, root, 700, 500);
        Assert.True(child.IsPressed);
        Assert.Same(child, ctx.HoverTarget);

        Release(ctx, root, 700, 500);
        Assert.False(child.IsPressed);
        Assert.Null(ctx.CapturedElement);
    }

    [Fact]
    public void Click_RequiresPressAndReleaseOnSameElement()
    {
        var (ctx, root) = NewTree();
        var clicks = 0;
        var child = new Panel(ctx)
        {
            X = 10,
            Y = 10,
            Width = 100,
            Height = 100,
            OnClick = _ => clicks++
        };
        root.Children = [child];
        root.Layout();

        Press(ctx, root, 50, 50);
        Release(ctx, root, 60, 60);
        Assert.Equal(1, clicks);

        // Press inside, release outside: no click.
        Press(ctx, root, 50, 50);
        Release(ctx, root, 700, 500);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void Click_BubblesToFirstAncestorWithHandler()
    {
        var (ctx, root) = NewTree();
        var clicks = 0;
        var button = new Panel(ctx)
        {
            X = 10,
            Y = 10,
            Width = 100,
            Height = 100,
            OnClick = _ => clicks++
        };
        var label = new Panel(ctx) { X = 10, Y = 10, Width = 50, Height = 50 };
        button.Children = [label];
        root.Children = [button];
        root.Layout();

        // Hit lands on the label (topmost), which has no handler; the button's fires.
        Press(ctx, root, 40, 40);
        Assert.Same(label, ctx.HoverTarget);
        Release(ctx, root, 40, 40);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void Scroll_BubblesUntilHandled()
    {
        var (ctx, root) = NewTree();
        var parent = new ScrollRecorder(ctx) { X = 0, Y = 0, Width = 300, Height = 300, Handles = true };
        var child = new ScrollRecorder(ctx) { X = 10, Y = 10, Width = 100, Height = 100, Handles = false };
        parent.Children = [child];
        root.Children = [parent];
        root.Layout();

        Scroll(ctx, root, 50, 50, -1);

        Assert.Equal(1, child.ScrollCalls);
        Assert.Equal(1, parent.ScrollCalls);

        // A handling child stops the bubble.
        child.Handles = true;
        Scroll(ctx, root, 50, 50, -1);
        Assert.Equal(2, child.ScrollCalls);
        Assert.Equal(1, parent.ScrollCalls);
    }

    [Fact]
    public void Focus_ClickFocusesAndClickAwayBlurs()
    {
        var (ctx, root) = NewTree();
        var focuses = 0;
        var blurs = 0;
        var input = new Panel(ctx)
        {
            X = 10,
            Y = 10,
            Width = 100,
            Height = 30,
            Focusable = true,
            OnFocus = _ => focuses++,
            OnBlur = _ => blurs++
        };
        root.Children = [input];
        root.Layout();

        Press(ctx, root, 50, 20);
        Release(ctx, root, 50, 20);
        Assert.Same(input, ctx.FocusedElement);
        Assert.True(input.IsFocused);
        Assert.Equal(1, focuses);

        // Click empty space: blur.
        Press(ctx, root, 700, 500);
        Assert.Null(ctx.FocusedElement);
        Assert.Equal(1, blurs);
    }

    [Fact]
    public void Focus_EscapeBlursAndKeysRouteToFocused()
    {
        var (ctx, root) = NewTree();
        var input = new KeyRecorder(ctx) { X = 10, Y = 10, Width = 100, Height = 30, Focusable = true };
        root.Children = [input];
        root.Layout();

        Press(ctx, root, 50, 20);
        Assert.Same(input, ctx.FocusedElement);

        Assert.True(ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.A, 0, 0, false)));
        Assert.Equal(Keys.A, input.LastKey);

        ctx.DispatchTextInput(new TextInputEventArgs('h'));
        Assert.Equal("h", input.Text);

        // Escape is not consumed by the element -> blurs.
        Assert.True(ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Escape, 0, 0, false)));
        Assert.Null(ctx.FocusedElement);

        // Nothing focused: events are not consumed.
        Assert.False(ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.A, 0, 0, false)));
    }

    [Fact]
    public void DetachedSubtree_GetsCorrectIndicesAndHoverAfterAddChild()
    {
        // Mirrors the settings screen: a row with a toggle button is composed while
        // detached (shallow indices), then AddChild-ed into a deep list. The button
        // must still win the hit-test over its own row.
        var (ctx, root) = NewTree();
        var list = new Panel(ctx) { Width = 400, Height = 400 };
        var deep = new Panel(ctx) { Width = 400, Height = 400 };
        root.Children = [deep];
        deep.Children = [list];
        root.Layout();

        var row = new Panel(ctx) { Width = 300, Height = 44 };
        var button = new Panel(ctx) { X = 100, Y = 4, Width = 80, Height = 36 };
        row.Children = [button];

        list.AddChild(row);
        root.Layout();

        Assert.Equal(list.Index + 1, row.Index);
        Assert.Equal(row.Index + 1, button.Index);

        Move(ctx, root, 140, 20);
        Assert.Same(button, ctx.HoverTarget);
        Assert.True(button.IsHovered);
    }

    [Fact]
    public void MultiRoot_PointerOverOneTreeDoesNotClearTheOther()
    {
        var ctx = new TestUIContext();
        var rootA = new Panel(ctx) { Width = 300, Height = 300 };
        var rootB = new Panel(ctx) { X = 400, Y = 0, Width = 300, Height = 300 };
        rootA.Layout();
        rootB.Layout();

        // Pointer over tree A; both roots are tested each frame.
        Move(ctx, rootA, 50, 50);
        Assert.True(rootA.IsHovered);

        Move(ctx, rootB, 50, 50); // B sees no hit; must not clear A's state
        Assert.True(rootA.IsHovered);
        Assert.Same(rootA, ctx.HoverTarget);
    }

    [Fact]
    public void RemovingTheCapturedSubtree_ReleasesCaptureHoverAndFocus()
    {
        // The view-swap-on-double-click scenario: press #2 captures an element,
        // the double-press handler removes that element's whole view from the
        // tree. The stale capture must not brick pointer input (UpdatePointer
        // early-returns while a capture belongs to no live root).
        var (ctx, root) = NewTree();
        var oldView = new Panel(ctx) { Width = 800, Height = 600 };
        var capturer = new CapturingSwapper(ctx) { Width = 200, Height = 200, Focusable = true };
        oldView.Children = [capturer];
        var newView = new Panel(ctx) { Width = 800, Height = 600 };
        capturer.OnDoublePress = () =>
        {
            root.RemoveChild(oldView);
            root.AddChild(newView);
        };
        root.AddChild(oldView);
        root.Layout();

        Press(ctx, root, 50, 50);
        Release(ctx, root, 50, 50);
        Press(ctx, root, 50, 50); // double-press swaps the views mid-dispatch
        Assert.Null(ctx.CapturedElement);
        Assert.Null(ctx.FocusedElement);
        Release(ctx, root, 50, 50);

        // Pointer input still reaches the new view.
        root.Layout(); // the app lays out every frame
        Move(ctx, root, 50, 50);
        Assert.Same(newView, ctx.HoverTarget);
        Press(ctx, root, 50, 50);
        Assert.Same(newView, ctx.CapturedElement);
    }

    [Fact]
    public void RightPressId_BumpsOncePerPress_NotPerFrameWhileHeld()
    {
        // Right presses are dispatched level-triggered (for sweep gestures), so handlers
        // whose action isn't idempotent key off this id to act once per press.
        var (ctx, root) = NewTree();
        var target = new Panel(ctx) { X = 0, Y = 0, Width = 100, Height = 100 };
        root.Children = [target];
        root.Layout();

        var idle = ctx.RightPressId;
        ctx.UpdatePointer(root, 50, 50, false, false, false, Vector2.Zero, true);
        var pressed = ctx.RightPressId;
        Assert.NotEqual(idle, pressed);

        // held down over two more frames - same press
        ctx.UpdatePointer(root, 50, 50, false, false, false, Vector2.Zero, true);
        ctx.UpdatePointer(root, 60, 50, false, false, false, Vector2.Zero, true);
        Assert.Equal(pressed, ctx.RightPressId);

        ctx.UpdatePointer(root, 60, 50, false, false, false, Vector2.Zero); // released
        Assert.Equal(pressed, ctx.RightPressId);

        ctx.UpdatePointer(root, 60, 50, false, false, false, Vector2.Zero, true); // pressed again
        Assert.NotEqual(pressed, ctx.RightPressId);
    }

    private sealed class CapturingSwapper(UIContext context) : Panel(context)
    {
        public Action OnDoublePress { get; set; } = () => { };

        public override bool HandlePress(float x, float y)
        {
            return true; // captures, like a draggable clip block
        }

        public override bool HandleDoublePress(float x, float y)
        {
            OnDoublePress();
            return true;
        }
    }

    private sealed class ScrollRecorder(UIContext context) : Panel(context)
    {
        public bool Handles { get; set; }
        public int ScrollCalls { get; private set; }

        public override bool HandleScroll(Vector2 scrollDelta)
        {
            ScrollCalls++;
            return Handles;
        }
    }

    private sealed class KeyRecorder(UIContext context) : Panel(context)
    {
        public Keys LastKey { get; private set; } = Keys.Unknown;
        public string Text { get; private set; } = "";

        public override bool HandleKeyDown(KeyboardKeyEventArgs e)
        {
            LastKey = e.Key;
            return e.Key != Keys.Escape;
        }

        public override void HandleTextInput(TextInputEventArgs e)
        {
            Text += e.AsString;
        }
    }
}
