using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Objects.Playfield.Batch;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

namespace ThirtyDollarVisualizer.Objects.Playfield;

public class Playfield(PlayfieldSettings settings) : IDisposable
{
    private readonly ChunkGenerator _chunkGenerator = new(settings);
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly ColoredPlane _objectBox = new()
    {
        Color = (0, 0, 0, 0.25f)
    };

    private readonly DollarStoreCamera _temporaryCamera = new(Vector3.Zero, Vector2i.Zero);
    private Vector3 _currentPosition = Vector3.Zero;
    private bool _disposed;

    private bool _firstPosition = true;
    private int _lastCullingIndex;
    private Vector3 _targetPosition = Vector3.Zero;

    public List<PlayfieldChunk> Chunks { get; private set; } = [];
    public List<SoundRenderable> Renderables { get; private set; } = [];

    public bool DisplayCenter { get; set; } = true;

    public void Dispose()
    {
        _disposed = true;
        foreach (var chunk in Chunks) chunk.Dispose();
        Chunks = [];
        Renderables = [];
        
        GC.SuppressFinalize(this);
    }

    public void UpdateSounds(Sequence sequence)
    {
        var events = sequence.Events;

        var chunks = _chunkGenerator.GenerateChunks(events);
        _chunkGenerator.PositionSounds(CollectionsMarshal.AsSpan(chunks));

        var renderables = new List<SoundRenderable>(chunks.Count * ChunkGenerator.DefaultChunkSize);
        foreach (var chunk in chunks)
            renderables.AddRange(chunk.Renderables);

        _lock.Wait();

        Chunks = chunks;
        Renderables = renderables;

        _lock.Release();
    }


    public void Render(DollarStoreCamera realCamera, float zoom, double updateDelta)
    {
        var layoutWidth = _chunkGenerator.LayoutHandler.Width;
        var layoutHeight = _chunkGenerator.LayoutHandler.Height + _chunkGenerator.LayoutHandler.Size +
                           _chunkGenerator.LayoutHandler.VerticalMargin;

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
        _objectBox.Position = (0, 0, 0);
        _objectBox.Scale = (layoutWidth, layoutHeight, 0);
        _objectBox.BorderRadius = 0f;

        _objectBox.UpdateModel(false);
        _objectBox.Render(_temporaryCamera);

        _lock.Wait();
        if (_disposed)
        {
            _lock.Release();
            return;
        }

        var span = CollectionsMarshal.AsSpan(Chunks);

        if (span.Length > 0)
            UpdateCullingIndex(span, camera_y);
        else _lastCullingIndex = 0;

        for (var index = _lastCullingIndex; index < span.Length; index++)
        {
            var chunk = span[index];
            if (chunk.StartY > camera_yh)
                break;

            chunk.Render(_temporaryCamera);
        }

        _lock.Release();
    }

    private void UpdateCullingIndex(ReadOnlySpan<PlayfieldChunk> span, float cameraY)
    {
        if (_lastCullingIndex >= span.Length)
            _lastCullingIndex = span.Length - 1;

        if (_lastCullingIndex < 0)
            _lastCullingIndex = 0;

        while (_lastCullingIndex > 0 && span[_lastCullingIndex - 1].EndY > cameraY) _lastCullingIndex--;

        while (span[_lastCullingIndex].EndY < cameraY)
        {
            if (++_lastCullingIndex < span.Length) break;
            _lastCullingIndex = span.Length - 1;
            break;
        }
    }
}