using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using OpenTK.Mathematics;
using Serilog;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Core;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Tests;

public class RenderQueueTests
{
    [Fact]
    public void TestQueueRender_Append()
    {
        var context = new TestUIContext();
        var renderable1 = new MockRenderable();
        var renderable2 = new MockRenderable();

        context.QueueRender(renderable1, 0);
        context.QueueRender(renderable2, 0);

        var queue = context.GetRenderQueue()[0];
        Assert.Equal(2, queue.Count);
        Assert.Same(renderable1, queue[0]);
        Assert.Same(renderable2, queue[1]);
    }

    [Fact]
    public void TestQueueRender_Insert()
    {
        var context = new TestUIContext();
        var renderable1 = new MockRenderable();
        var renderable2 = new MockRenderable();
        var renderable3 = new MockRenderable();

        context.QueueRender(renderable1, 0);
        context.QueueRender(renderable3, 0);
        context.QueueRender(renderable2, 0, 1);

        var queue = context.GetRenderQueue()[0];
        Assert.Equal(3, queue.Count);
        Assert.Same(renderable1, queue[0]);
        Assert.Same(renderable2, queue[1]);
        Assert.Same(renderable3, queue[2]);
    }

    [Fact]
    public void TestDequeueRender_Found()
    {
        var context = new TestUIContext();
        var renderable1 = new MockRenderable();
        var renderable2 = new MockRenderable();

        context.QueueRender(renderable1, 0);
        context.QueueRender(renderable2, 0);

        var index = context.DequeueRender(renderable1, 0);

        Assert.Equal(0, index);
        var queue = context.GetRenderQueue()[0];
        Assert.Single(queue);
        Assert.Same(renderable2, queue[0]);
    }

    [Fact]
    public void TestDequeueRender_NotFound()
    {
        var context = new TestUIContext();
        var renderable1 = new MockRenderable();
        var renderable2 = new MockRenderable();

        context.QueueRender(renderable1, 0);

        var index = context.DequeueRender(renderable2, 0);

        Assert.Equal(-1, index);
        var queue = context.GetRenderQueue()[0];
        Assert.Single(queue);
    }

    [Fact]
    public void TestHandleRenderableSwap_MaintainsOrder()
    {
        var context = new TestUIContext();
        var element = new TestElement(context);

        var r1 = new MockRenderable();
        var r2 = new MockRenderable();
        var r3 = new MockRenderable();

        context.QueueRender(r1, element.GetIndex());
        context.QueueRender(r2, element.GetIndex());
        context.QueueRender(r3, element.GetIndex());

        var r2New = new MockRenderable();

        // Swap r2 with r2New
        element.TestHandleRenderableSwap(r2, r2New);

        var queue = context.GetRenderQueue()[element.GetIndex()];
        Assert.Equal(3, queue.Count);
        Assert.Same(r1, queue[0]);
        Assert.Same(r2New, queue[1]);
        Assert.Same(r3, queue[2]);
    }

    [Fact]
    public void TestHandleRenderableSwap_WithNestedElements()
    {
        var context = new TestUIContext();
        var root = new TestElement(context);
        var child = new TestElement(context) { Parent = root };
        var grandchild = new TestElement(context) { Parent = child };

        Assert.Equal(0, root.GetIndex());
        Assert.Equal(1, child.GetIndex());
        Assert.Equal(2, grandchild.GetIndex());

        var rOld = new MockRenderable();
        var rNew = new MockRenderable();

        context.QueueRender(rOld, grandchild.GetIndex());

        grandchild.TestHandleRenderableSwap(rOld, rNew);

        var queue = context.GetRenderQueue()[grandchild.GetIndex()];
        Assert.Single(queue);
        Assert.Same(rNew, queue[0]);
    }

    [Fact]
    public void TestHandleRenderableSwap_FromNull()
    {
        var context = new TestUIContext();
        var element = new TestElement(context);

        var r1 = new MockRenderable();

        // oldValue is null, newValue is IRenderable. Should append.
        element.TestHandleRenderableSwap(null, r1);

        var queue = context.GetRenderQueue()[element.GetIndex()];
        Assert.Single(queue);
        Assert.Same(r1, queue[0]);
    }

    [Fact]
    public void TestButtonStyleSwap_BackgroundStaysBehindText()
    {
        var context = new TestUIContext();

        // Simulating a Button-like structure:
        // Button (Panel) has a Background and a Child (Label)
        // Label has a TextBuffer

        var button = new TestElement(context) { Index = 10 };
        var label = new TestElement(context) { Index = 11, Parent = button };

        var textBuffer = new MockRenderable();
        context.QueueRender(textBuffer, label.GetIndex());

        var initialBackground = new MockRenderable();
        button.TestHandleRenderableSwap(null, initialBackground);

        var queue10 = context.GetRenderQueue()[button.GetIndex()];
        var queue11 = context.GetRenderQueue()[label.GetIndex()];

        Assert.Same(initialBackground, queue10[0]);
        Assert.Same(textBuffer, queue11[0]);

        // Now swap background via style
        var newBackground = new MockRenderable();
        button.TestHandleRenderableSwap(initialBackground, newBackground);

        // Check order
        // In a real scenario, both might be in the same layer if Index is the same, 
        // but here they are in different layers (10 and 11).
        // If they are in different layers, the layer index handles it.
        // Wait, if Button and Label have different Index, then background (layer 10) 
        // should ALWAYS be before text (layer 11) regardless of position in queue 10.

        Assert.Same(newBackground, context.GetRenderQueue()[10][0]);
        Assert.Same(textBuffer, context.GetRenderQueue()[11][0]);
    }

    [Fact]
    public void TestSameLayerSwap_MaintainsRelativeOrder()
    {
        var context = new TestUIContext();
        var element = new TestElement(context) { Index = 5 };

        var rBackground = new MockRenderable();
        var rForeground = new MockRenderable();

        context.QueueRender(rBackground, 5);
        context.QueueRender(rForeground, 5);

        var queue = context.GetRenderQueue()[5];
        Assert.Same(rBackground, queue[0]);
        Assert.Same(rForeground, queue[1]);

        // Swap background
        var rNewBackground = new MockRenderable();
        element.TestHandleRenderableSwap(rBackground, rNewBackground);

        Assert.Same(rNewBackground, queue[0]);
        Assert.Same(rForeground, queue[1]);
    }

    [Fact]
    public void TestSameLayerSwap_BackgroundWithSameIndex()
    {
        var context = new TestUIContext();
        var button = new TestElement(context) { Index = 5 };

        var background = new MockRenderable();
        var text = new MockRenderable();

        // Simulating Background queued first, then text in SAME layer
        context.QueueRender(background, 5);
        context.QueueRender(text, 5);

        var queue = context.GetRenderQueue()[5];
        Assert.Equal(2, queue.Count);
        Assert.Same(background, queue[0]);
        Assert.Same(text, queue[1]);

        // Swap background
        var newBackground = new MockRenderable();
        button.TestHandleRenderableSwap(background, newBackground);

        // In the current implementation, it should maintain index 0
        Assert.Same(newBackground, queue[0]);
        Assert.Same(text, queue[1]);
    }

    [Fact]
    public void TestHoverStateChange_NoDuplicatesInQueue()
    {
        var context = new TestUIContext();
        var element = new TestPanel(context)
        {
            CustomTag = "panel",
            Index = 5
        };

        // Mock a stylesheet with a hover state for background
        var holder = new StyleSheetHolder
        {
            Components =
            {
                ["panel"] = new Dictionary<string, IStyleValue>
                {
                    { "background", new ColorValue("#7aa2f7") },
                    {
                        "state[hovered]",
                        new BlockValue(new Dictionary<string, IStyleValue>
                            { { "background", new ColorValue("#000000") } })
                    }
                }
            }
        };
        var stylesheet = new StyleSheet(holder);

        // Apply base style - this should call HandleRenderableSwap and add Background to queue
        element.ApplyStyleSheet(stylesheet);

        // Simulate LoaderInterface calling DrawTo
        element.DrawTo(context);

        var queue = context.GetRenderQueue()[5];
        // FAILURE EXPECTED HERE if the fix is not applied
        Assert.Single(queue);

        var baseBackground = element.Background;

        // Change state to Hovered
        var propCurrentState = typeof(UIElement).GetProperty("CurrentState");
        Assert.NotNull(propCurrentState);
        propCurrentState.SetValue(element, UIState.Hovered);

        var hoverBackground = element.Background;
        Assert.NotSame(baseBackground, hoverBackground);

        // If there were duplicates, the queue would be [hover, base]
        queue = context.GetRenderQueue()[5];
        Assert.Single(queue);
        Assert.Same(hoverBackground, queue[0]);
    }

    [Fact]
    public void TestHoverStateChange_ActuallyChangesColor()
    {
        var context = new TestUIContext();
        var element = new TestPanel(context)
        {
            CustomTag = "panel",
            Index = 5
        };

        var baseBackground = new MockRenderable();
        element.Background = baseBackground;
        context.QueueRender(baseBackground, 5);

        // Mock a stylesheet with a hover state for background
        var holder = new StyleSheetHolder();
        var hoverProps = new Dictionary<string, IStyleValue>
        {
            { "background", new ColorValue("#FF0000") }
        };
        holder.Components["panel"] = new Dictionary<string, IStyleValue>
        {
            { "state[hovered]", new BlockValue(hoverProps) }
        };
        var stylesheet = new StyleSheet(holder);

        // Apply base style
        element.ApplyStyleSheet(stylesheet);

        // Change state to Hovered
        var propCurrentState = typeof(UIElement).GetProperty("CurrentState");
        Assert.NotNull(propCurrentState);
        propCurrentState.SetValue(element, UIState.Hovered);
        // CurrentState setter calls InvalidateStyle

        var hoverBackground = element.Background;
        Assert.NotSame(baseBackground, hoverBackground);

        // CHECK: Is the new hover background in the queue?
        var queue = context.GetRenderQueue()[5];
        Assert.Single(queue);
        Assert.Same(hoverBackground, queue[0]);
    }

    [Fact]
    public void TestInvalidateStyle_RestoresBackgroundToQueue()
    {
        var context = new TestUIContext();
        var element = new TestPanel(context)
        {
            CustomTag = "panel",
            Index = 5
        };

        var baseBackground = new MockRenderable();
        element.Background = baseBackground;

        // Mock a stylesheet with a hover state for background
        var holder = new StyleSheetHolder();
        var hoverProps = new Dictionary<string, IStyleValue>
        {
            { "background", new ColorValue("#FF0000") }
        };
        holder.Components["panel"] = new Dictionary<string, IStyleValue>
        {
            { "state[hovered]", new BlockValue(hoverProps) }
        };
        var stylesheet = new StyleSheet(holder);

        // Apply base style
        element.ApplyStyleSheet(stylesheet);
        // Ensure baseBackground is in queue (in real app DrawSelf does this, or we can simulate it)
        context.QueueRender(baseBackground, 5);

        Assert.Same(baseBackground, context.GetRenderQueue()[5][0]);

        // Change state to Hovered
        var propCurrentState = typeof(UIElement).GetProperty("CurrentState");
        Assert.NotNull(propCurrentState);
        propCurrentState.SetValue(element, UIState.Hovered);
        element.InvalidateStyle();

        var hoverBackground = element.Background;
        Assert.NotSame(baseBackground, hoverBackground);
        // During InvalidateStyle, ApplyStateOverride calls ApplyStyleValue which calls HandleRenderableSwap
        // So hoverBackground should be in the queue now.
        Assert.Same(hoverBackground, context.GetRenderQueue()[5][0]);

        // Change state back to None
        propCurrentState.SetValue(element, UIState.None);
        element.InvalidateStyle();

        Assert.Same(baseBackground, element.Background);

        // CRITICAL CHECK: Is it back in the render queue?
        var queue = context.GetRenderQueue()[5];
        Assert.Contains(baseBackground, queue);
        Assert.Same(baseBackground, queue[0]);
    }

    [Fact]
    public void TestHandleRenderableSwap_BackgroundFallbackToIndexZero()
    {
        var context = new TestUIContext();
        var element = new TestPanel(context) { Index = 5 };

        var text = new MockRenderable();
        context.QueueRender(text, 5);

        var newBackground = new MockRenderable();
        element.Background =
            newBackground; // This will trigger ApplyStyleValue or we can call HandleRenderableSwap directly
        element.TestHandleRenderableSwap(null, newBackground, "Background");

        var queue = context.GetRenderQueue()[5];
        Assert.Equal(2, queue.Count);
        Assert.Same(newBackground, queue[0]); // SHOULD BE AT INDEX 0 NOW
        Assert.Same(text, queue[1]);
    }

    [Fact]
    public void TestHandleRenderableSwap_WithPriorityAttribute()
    {
        var context = new TestUIContext();
        var element = new TestElement(context) { Index = 5 };

        var existing = new MockRenderable();
        context.QueueRender(existing, 5);

        // Test Background (Priority 0)
        var background = new MockRenderable();
        element.TestHandleRenderableSwap(null, background, "Background");

        var queue = context.GetRenderQueue()[5];
        Assert.Equal(2, queue.Count);
        Assert.Same(background, queue[0]); // Priority 0 should go to start
        Assert.Same(existing, queue[1]);

        // Test Foreground (Priority 99)
        var foreground = new MockRenderable();
        element.TestHandleRenderableSwap(null, foreground, "Foreground");

        Assert.Equal(3, queue.Count);
        Assert.Same(background, queue[0]);
        Assert.Same(existing, queue[1]);
        Assert.Same(foreground, queue[2]); // Priority 99 should append
    }

    private class TestUIContext : UIContext
    {
        [SetsRequiredMembers]
        public TestUIContext()
        {
            var logger = new LoggerConfiguration().CreateLogger();
            InjectForTesting(
                new AssetProvider(logger, [Assembly.GetExecutingAssembly()], new GLInfo()),
                new MockFontProvider(),
                new MockTextProvider());
            Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(800, 600));
        }

        public List<List<IRenderable>> GetRenderQueue()
        {
            return LayeredRenderQueue;
        }
    }

    private class MockRenderable : Renderable, IDisposable
    {
        public void Dispose()
        {
        }

        public override void Render(Camera camera)
        {
        }

        public override void Update()
        {
        }
    }

    private class TestElement(UIContext context) : UIElement(context)
    {
        public override string Tag => "test";

        public new int Index
        {
            get => base.Index;
            set => base.Index = value;
        }

        [RenderPriority(0)] public Renderable? Background { get; set; }

        [RenderPriority(99)] public Renderable? Foreground { get; set; }

        protected override void DrawSelf(UIContext context)
        {
        }

        public void TestHandleRenderableSwap(object? oldV, object? newV, string? propName = null)
        {
            HandleRenderableSwap(oldV, newV, propName);
        }

        public int GetIndex()
        {
            return Index;
        }
    }
}