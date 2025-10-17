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
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ChunkGenerator _chunkGenerator = new(settings);
    
    private readonly DollarStoreCamera _temporaryCamera = new(Vector3.Zero, Vector2i.Zero);
    private Vector3 _currentPosition = Vector3.Zero;
    private Vector3 _targetPosition = Vector3.Zero;
    
    private bool _firstPosition = true;
    public List<PlayfieldChunk> Chunks { get; private set; } = [];
    public List<SoundRenderable> Renderables { get; private set; } = [];
    
    public readonly ConcurrentDictionary<string, SingleTexture> DecreasingValuesCache = new();

    public bool DisplayCenter { get; set; } = true;

    public async ValueTask UpdateSounds(Sequence sequence)
    {
        var events = sequence.Events;

        var chunks = _chunkGenerator.GenerateChunks(events);
        _chunkGenerator.PositionSounds(CollectionsMarshal.AsSpan(chunks));
        
        var renderables = new List<SoundRenderable>(chunks.Count * ChunkGenerator.DefaultChunkSize);
        foreach (var chunk in chunks)
            renderables.AddRange(chunk.Renderables);

        await _lock.WaitAsync();
        
        Chunks = chunks;
        Renderables = renderables;
        
        _lock.Release();
    }

    public void Render(DollarStoreCamera realCamera, float zoom, float updateDelta)
    {
        var layoutWidth = _chunkGenerator.LayoutHandler.Width;
        var layoutHeight = _chunkGenerator.LayoutHandler.Height;
        
        var leftMargin = (_temporaryCamera.Width - layoutWidth) / 2f;
        _targetPosition = DisplayCenter ? (-leftMargin, 0, 0) : Vector3.Zero;
        
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
        _objectBox.Scale = (layoutWidth, layoutHeight, 0);
        _objectBox.BorderRadius = 0f;

        _objectBox.UpdateModel(false);
        _objectBox.Render(_temporaryCamera);
        
        _lock.Wait();
        foreach (var chunk in CollectionsMarshal.AsSpan(Chunks))
        {
            // Skip rendering chunks that are outside the visible camera area
            if (chunk.EndY < camera_y || chunk.StartY > camera_yh)
                continue;
            
            chunk.Render(_temporaryCamera);
        }
        _lock.Release();
    }
}