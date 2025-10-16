using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Objects.Playfield.Batch;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

namespace ThirtyDollarVisualizer.Objects.Playfield;

public class Playfield(PlayfieldSettings settings)
{
    private readonly ColoredPlane _objectBox = new()
    {
        Color = (0, 0, 0, 0.25f)
    };
    private readonly DollarStoreCamera _temporaryCamera = new(Vector3.Zero, Vector2i.Zero);
    private Vector3 _currentPosition = Vector3.Zero;
    private Vector3 _targetPosition = Vector3.Zero;
    
    private bool _firstPosition = true;
    public List<PlayfieldChunk> Chunks { get; set; } = [];
    public List<SoundRenderable> Renderables { get; set; } = [];
    public readonly ConcurrentDictionary<string, SingleTexture> DecreasingValuesCache = new();

    public bool DisplayCenter { get; set; } = true;

    public ValueTask UpdateSounds(Sequence sequence)
    {
        var events = sequence.Events;
        var chunkGenerator = new ChunkGenerator(settings);
        
        Chunks = chunkGenerator.GenerateChunks(events);
        Renderables = Chunks.SelectMany(chunk => chunk.Renderables).ToList();
        
        return ValueTask.CompletedTask;
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
        
        foreach (var chunk in CollectionsMarshal.AsSpan(Chunks))
        {
            // Skip rendering chunks that are outside the visible camera area
            if (chunk.EndY < camera_y || chunk.StartY > camera_yh)
                continue;
            
            chunk.Render(_temporaryCamera);
        }
    }
}