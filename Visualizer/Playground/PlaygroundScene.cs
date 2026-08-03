using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Engine;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;

namespace Playground;

/// <summary>
///     Manual testbed for the Sundex core rework (docs/handover/sundex-core-rework.md):
///     TextInput typing/caret/selection, NumericInput clamping, Checkbox, Slider
///     (capture drag), a ScrollView with 100 dynamically added rows, and a modal layer.
/// </summary>
public class PlaygroundScene : Scene
{
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;
    private readonly Panel _root;

    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;

    public PlaygroundScene(Game game) : base(game)
    {
        var clientSize = Game.ClientSize;
        if (Game.TryGetScreenScale(out var scaleX, out var scaleY))
            _lastScale = new Vector2(scaleX, scaleY);

        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);
        _context = new UIContext
        {
            Camera = _camera,
            PixelScale = _lastScale,
            RequestCursor = type => _cursorType = type
        };

        _root = BuildUI(_context);
        _root.DrawTo(_context);
    }

    private Panel BuildUI(UIContext ctx)
    {
        var root = new Panel(ctx)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };

        var sliderValue = new Label(ctx, "50") { FontSizePx = 16f };
        var slider = new Slider(ctx)
        {
            Width = 320,
            Height = 12,
            Min = 0,
            Max = 100,
            Value = 50,
            OnValueChanged = s => sliderValue.SetTextContents($"{s.Value:0}")
        };

        var scroll = new ScrollView(ctx)
        {
            Width = 340,
            Height = 220,
            Background = new ColoredPlane { Color = (0.10f, 0.11f, 0.14f, 1f) }
        };
        for (var i = 0; i < 100; i++) scroll.AddChild(NewRow(ctx, i));

        var column = new StackPanel(ctx)
        {
            X = 20,
            Y = 20,
            Direction = LayoutDirection.Vertical,
            Spacing = 14,
            Width = LiteralOrComputable.AutoSize,
            Height = LiteralOrComputable.AutoSize,
            Children =
            [
                new Label(ctx, "Sundex component playground") { FontSizePx = 22f },
                new TextInput(ctx, "type, select, drag")
                {
                    Width = 320,
                    Background = new ColoredPlane { Color = (0.15f, 0.16f, 0.21f, 1f) }
                },
                new NumericInput(ctx, 50)
                {
                    Width = 220,
                    Min = 0,
                    Max = 100,
                    Step = 5,
                    Background = new ColoredPlane { Color = (0.15f, 0.16f, 0.21f, 1f) }
                },
                new Checkbox(ctx, "Toggle me"),
                new StackPanel(ctx)
                {
                    Direction = LayoutDirection.Horizontal,
                    Spacing = 12,
                    Width = LiteralOrComputable.AutoSize,
                    Height = LiteralOrComputable.AutoSize,
                    Children = [slider, sliderValue]
                },
                new Button(ctx, "Open modal", new ColoredPlane { Color = (0.30f, 0.42f, 0.80f, 1f) })
                {
                    OnClick = _ => OpenModal()
                },
                scroll
            ]
        };

        root.AddChild(column);
        return root;
    }

    private Panel NewRow(UIContext ctx, int index)
    {
        return new Panel(ctx)
        {
            Height = 26,
            Width = LiteralOrComputable.Percent(100),
            Padding = 4,
            Background = new ColoredPlane
            {
                Color = index % 2 == 0 ? (0.16f, 0.17f, 0.21f, 1f) : (0.13f, 0.14f, 0.18f, 1f)
            },
            Children = [new Label(ctx, $"Row {index}") { FontSizePx = 14f }]
        };
    }

    private void OpenModal()
    {
        var modal = new ModalLayer(_context);
        var content = new FlexPanel(_context)
        {
            Direction = LayoutDirection.Vertical,
            HorizontalAlign = Align.Center,
            Padding = 16,
            Spacing = 14,
            Width = 300,
            Height = 130,
            Background = new ColoredPlane { Color = (0.15f, 0.17f, 0.22f, 1f) }
        };
        content.AddChild(new Label(_context, "A modal dialog") { FontSizePx = 18f });
        content.AddChild(new Button(_context, "Close", new ColoredPlane { Color = (0.30f, 0.42f, 0.80f, 1f) })
        {
            OnClick = _ => _root.RemoveChild(modal)
        });

        modal.OnDismissRequested = m => _root.RemoveChild(m);
        modal.AddChild(content);
        _root.AddChild(modal);
    }

    public override void Initialize(InitArguments initArguments)
    {
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments renderArgs)
    {
        _context.Render();
    }

    public override void TransitionedTo()
    {
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _camera.UpdateMatrix();
        _cursorType = CursorType.Default;
        _root.Update(_context);
        _root.Layout();

        var cursor = _cursorType switch
        {
            CursorType.Default => MouseCursor.Default,
            CursorType.Pointer => MouseCursor.PointingHand,
            CursorType.ResizeX => MouseCursor.ResizeEW,
            CursorType.ResizeY => MouseCursor.ResizeNS,
            _ => MouseCursor.Default
        };

        if (Game.Cursor != cursor)
            Game.Cursor = cursor;
    }

    public override void Resize(int w, int h)
    {
        float width = w;
        float height = h;
        if (Game.TryGetScreenScale(out var scaleX, out var scaleY))
        {
            width /= scaleX;
            height /= scaleY;
            _lastScale = new Vector2(scaleX, scaleY);
        }

        _camera.Viewport = new Vector2i((int)width, (int)height);
        _camera.UpdateMatrix();
        _context.PixelScale = _lastScale;

        _root.InvalidateCoordinates();
        _root.Layout();
    }

    public override void Shutdown()
    {
    }

    public override void FileDrop(string[] locations)
    {
    }

    public override void Keyboard(KeyboardState state)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        _root.Test(mouseState, _lastScale);
    }

    public override void TextInput(TextInputEventArgs e)
    {
        _context.DispatchTextInput(e);
    }

    public override void KeyDown(KeyboardKeyEventArgs e)
    {
        _context.DispatchKeyDown(e);
    }
}