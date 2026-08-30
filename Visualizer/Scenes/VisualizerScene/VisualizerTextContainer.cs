using Shared.Helpers.Positioning;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text;

namespace VisualizerScene;

public class VisualizerTextContainer
{
    private const int UpdatableTextSliceMaxLength = 1024;
    private readonly TextBuffer _controlsBuffer;
    private readonly TextBuffer _debugBuffer;
    private readonly TextBuffer _genericBuffer;
    private readonly TextBuffer _greetingBuffer;
    private readonly float _scale;

    public VisualizerTextContainer(VisualizerFonts fonts, int width, int height, float scale = 1f)
    {
        _scale = scale;

        _genericBuffer = new TextBuffer(fonts.LatoBoldProvider, fonts.DeleteQueue);
        _debugBuffer = new TextBuffer(fonts.LatoBoldProvider, fonts.DeleteQueue);
        _greetingBuffer = new TextBuffer(fonts.LatoBoldProvider, fonts.DeleteQueue);
        _controlsBuffer = new TextBuffer(fonts.LatoBoldProvider, fonts.DeleteQueue);
        Overlay = CreateLayout(width, height);
        Greeting = _greetingBuffer.GetTextSlice(" ", UpdatableTextSliceMaxLength);
    }

    public Layout Overlay { get; }
    public TextSlice Greeting { get; }
    public bool ShowDebug { get; set; }
    public bool ShowControls { get; set; } = true;

    private Layout CreateLayout(int width, int height)
    {
        var overlay = new Layout(width, height);

        overlay.Add("controls",
            () => _controlsBuffer.GetTextSlice(
                """
                All controls:

                Scroll -> Scroll up / down.
                Ctrl+Scroll -> Change the zoom.
                Up / Down -> Control the application's volume.
                Left / Right -> Seek the sequence.
                R -> Reload the current sequence.
                C -> Change the camera modes.
                F -> Toggle between fullscreen and windowed.
                Space -> Pause / resume the sequence.
                0-9 -> Seek to bookmark.
                Ctrl+0-9 -> Set bookmark to current time.
                Ctrl+Shift+0-9 -> Clear given bookmark time.
                Ctrl+D -> Show debugging info.
                Ctrl+Q -> Close the program.
                Page Up/Down -> Seek to previous/next sequence.

                """,
                (value, buffer, range) => new TextSlice(buffer, range)
                {
                    Value = value,
                    FontSize = 14 * _scale,
                    Position = (10, 30, 0)
                }));

        overlay.Add("debug",
            () => _debugBuffer.GetTextSlice("", UpdatableTextSliceMaxLength),
            text =>
            {
                text.FontSize = 14 * _scale;
                text.Position = (10, 30, 0);
            }
        );

        overlay.Add("log",
            () => _genericBuffer.GetTextSlice(" ", UpdatableTextSliceMaxLength),
            text =>
            {
                text.FontSize = 48f * _scale;
                text.Position = (20, 20, 0);
            });

        overlay.Add("update", () => _genericBuffer.GetTextSlice(" ", UpdatableTextSliceMaxLength),
            text => { text.SetPosition((10, 0, 0)); });

        return overlay;
    }

    public void RenderStaticText(Camera camera)
    {
        _genericBuffer.RenderBuffer(camera);

        if (ShowDebug)
            _debugBuffer.RenderBuffer(camera);
        if (ShowControls)
            _controlsBuffer.RenderBuffer(camera);
    }

    public void RenderGreeting(Camera camera)
    {
        _greetingBuffer.RenderBuffer(camera);
    }

    public T Get<T>(ReadOnlySpan<char> name) where T : IPositionable
    {
        return Overlay.Get<T>(name);
    }
}