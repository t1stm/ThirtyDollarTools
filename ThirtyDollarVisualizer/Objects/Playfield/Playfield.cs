using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Objects.Playfield.Batch;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

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
    /// Contains all sound renderables in chunks.
    /// </summary>
    public List<PlayfieldChunk> Chunks = [];

    public bool DisplayCenter { get; set; } = true;

    public ValueTask UpdateSounds(Sequence sequence)
    {
        var events = sequence.Events;
        var chunkGenerator = new ChunkGenerator(settings);
        
        Chunks = chunkGenerator.GenerateChunks(events);
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

        // fix values when the zoom is changed
        camera_y -= height_scale / 2;
        camera_yh += height_scale / 2;
        
        // position object box
        _objectBox.SetPosition((0, 0, 0));
        _objectBox.Scale = (_temporaryCamera.Width, camera_yh, 0);
        _objectBox.BorderRadius = 0f;

        _objectBox.UpdateModel(false);
        _objectBox.Render(_temporaryCamera);

        foreach (var chunk in Chunks)
        {
            chunk.Render(_temporaryCamera);
        }
    }
}