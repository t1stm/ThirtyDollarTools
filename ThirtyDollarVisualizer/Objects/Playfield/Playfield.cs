using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Objects.Playfield.Batch;

namespace ThirtyDollarVisualizer.Objects.Playfield;

public class Playfield(PlayfieldSettings settings)
{
    private readonly List<float> _dividerPositionsY = [];

    private readonly ColoredPlane _objectBox = new()
    {
        Color = (0, 0, 0, 0.25f)
    };

    private readonly DollarStoreCamera _temporaryCamera = new(Vector3.Zero, Vector2i.Zero);

    /// <summary>
    /// Dictionary containing all decreasing value events' textures for this playfield.
    /// </summary>
    public readonly ConcurrentDictionary<string, SingleTexture> DecreasingValuesCache = new();

    private byte[] _animatedAssets = []; // TODO hyped up for this.

    private Vector3 _currentPosition = Vector3.Zero;
    private bool _firstPosition = true;

    private Vector3 _targetPosition = Vector3.Zero;
    public Memory<PlayfieldLine> Lines = Memory<PlayfieldLine>.Empty;

    /// <summary>
    /// Contains all sound renderables.
    /// </summary>
    public List<SoundRenderable> Objects = [];

    public bool DisplayCenter { get; set; } = true;

    public ValueTask UpdateSounds(Sequence sequence)
    {
        var events = sequence.Events;
        var chunkGenerator = new ChunkGenerator(settings);
        
        var chunks = chunkGenerator.GenerateChunks(events);
        
        
        
        return ValueTask.CompletedTask;
    }

    public static bool IsEventHidden(BaseEvent ev)
    {
        return string.IsNullOrEmpty(ev.SoundEvent) || (ev.SoundEvent.StartsWith('#') && ev is not ICustomActionEvent) ||
               ev is IHiddenEvent;
    }

    public void Render(DollarStoreCamera realCamera, float zoom, float updateDelta)
    {
        // avoid doing modifications to the main camera
        _temporaryCamera.CopyFrom(realCamera);

        // offset temporary camera to enable positioning anywhere
        var left_margin = (_temporaryCamera.Width - _layoutHandler.Width) / 2f;

        _targetPosition = DisplayCenter ? (-left_margin, 0, 0) : Vector3.Zero;
        if (_firstPosition)
        {
            _currentPosition = _targetPosition;
            _firstPosition = false;
        }

        _temporaryCamera.SetOffset(_currentPosition =
            SteppingFunctions.Exponential(_currentPosition, _targetPosition, updateDelta));
        _temporaryCamera.UpdateMatrix();

        // get generic camera values
        var camera_height = _temporaryCamera.Height;

        // set render culling limits
        var clamped_scale = Math.Min(zoom, 1f);
        var height_scale = camera_height / clamped_scale - camera_height;

        // get render culling camera values
        var camera_y = _temporaryCamera.Position.Y;
        var camera_yh = camera_y + camera_height;

        // get render culling object values
        var size_renderable = _layoutHandler.Size;
        var vertical_margin = _layoutHandler.VerticalMargin;

        // fix values when the zoom is changed
        camera_y -= height_scale / 2;
        camera_yh += height_scale / 2;

        // gets the number of dividers at the top and bottom of the screen
        var top_camera_dividers = 0;
        var bottom_camera_dividers = 0;

        // explicitly convert the list to a span for faster iteration
        foreach (var position in CollectionsMarshal.AsSpan(_dividerPositionsY))
        {
            if (position <= camera_y + size_renderable)
                top_camera_dividers++;

            if (position <= camera_yh)
                bottom_camera_dividers++;
        }

        // render the background dimmed box
        _objectBox.SetPosition((0, 0, 0));
        _objectBox.Scale = (_layoutHandler.Width, _layoutHandler.Height, 0);
        _objectBox.BorderRadius = 0f;

        _objectBox.UpdateModel(false);
        _objectBox.Render(_temporaryCamera);

        // get all lines this playfield has
        var tdw_span = Lines.Span;

        // finds the index of the line at the start of the view
        var start_line = camera_y / (size_renderable + vertical_margin);
        var start_index = start_line - top_camera_dividers - 1;

        // same but for the end of the view
        var end_line = camera_yh / (size_renderable + vertical_margin);
        var end_index = end_line - bottom_camera_dividers + 1;

        var start = (int)Math.Max(0, Math.Floor(start_index));
        var end = (int)Math.Min(tdw_span.Length, Math.Ceiling(end_index));

        // renders all lines in the start - end range
        int i;
        for (i = start; i < end; i++)
        {
            var line = tdw_span[i];
            line.Render(_temporaryCamera);
        }
    }
}