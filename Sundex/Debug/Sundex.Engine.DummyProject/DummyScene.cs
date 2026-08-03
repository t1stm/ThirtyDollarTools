using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;

namespace Sundex.Engine.DummyProject;

public class DummyScene(Game game) : Scene(game)
{
    private DummyCamera _camera = null!;
    private UIContext _context = null!;
    private Panel _root = null!;

    public override void Initialize(InitArguments initArguments)
    {
        _camera = new DummyCamera(Vector3.Zero, new Vector2i(1024, 600));
        _camera.UpdateMatrix();
        _context = new UIContext { Camera = _camera };

        var valueLabel = new Label(_context, "Hello, Sundex!") { FontSizePx = 16f };

        var input = new TextInput(_context, "Hello, Sundex!") { Width = 300 };
        input.OnValueChanged = ti => valueLabel.Value = ti.Value;

        var column = new FlexPanel(_context)
        {
            Direction = LayoutDirection.Vertical,
            HorizontalAlign = Align.Center,
            Spacing = 12
        };
        column.Children = [input, valueLabel];

        _root = new FlexPanel(_context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            HorizontalAlign = Align.Center,
            VerticalAlign = Align.Center
        };
        _root.Children = [column];
        _root.DrawTo(_context);
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
        _root.Update(_context);
        _root.Layout();
    }

    public override void Resize(int w, int h)
    {
        _camera.Viewport = new Vector2i(w, h);
        _camera.UpdateMatrix();
        _root.InvalidateCoordinates();
        _root.Layout();
    }

    public override void Shutdown()
    {
    }

    public override void FileDrop(string[] locations)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        _root.Test(mouseState, Vector2.One);
    }

    public override void TextInput(TextInputEventArgs e)
    {
        _context.DispatchTextInput(e);
    }

    public override void KeyDown(KeyboardKeyEventArgs e)
    {
        _context.DispatchKeyDown(e);
    }

    public override void Keyboard(KeyboardState state)
    {
    }
}