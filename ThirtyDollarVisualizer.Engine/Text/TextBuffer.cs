using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Buffers;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;

namespace ThirtyDollarVisualizer.Engine.Text;

public class TextBuffer : IRenderable, IDisposable
{
    private readonly List<Range> _freeRanges = [];
    private readonly Dictionary<TextSlice, Range> _usedRanges = [];

    private readonly VertexArrayObject _vao = new();
    public readonly GLBuffer<TextCharacter>.WithCPUCache Characters;
    public readonly TextProvider TextProvider;

    private int _currentOffset;
    private bool _disposing;

    public TextBuffer(TextProvider provider)
    {
        Characters = new GLBuffer<TextCharacter>.WithCPUCache(
            provider.AssetProvider.DeleteQueue, BufferTarget.ArrayBuffer);
        TextProvider = provider;
        InitializeVAO(_vao, Characters);
    }

    public void Dispose()
    {
        _disposing = true;
        foreach (var (textSlice, _) in _usedRanges) textSlice.Dispose();

        _vao.Dispose();
        Characters.Dispose();

        GC.SuppressFinalize(this);
    }

    public void Render(Camera camera)
    {
        RenderBuffer(camera);
    }

    private static void InitializeVAO(VertexArrayObject vao, GLBuffer<TextCharacter>.WithCPUCache characters)
    {
        vao.AddBuffer(GLQuad.VBOWithoutUV, new VertexBufferLayout().PushFloat(3));

        var layout = new VertexBufferLayout();
        TextCharacter.SelfReflectToGL(layout);

        vao.AddBuffer(characters, layout);
        vao.SetIndexBuffer(GLQuad.EBO);
    }

    public void Resize(int newSize)
    {
        Characters.ResizeCPUBuffer(newSize);
    }

    public TextSlice GetTextSlice(ReadOnlySpan<char> text, int capacity = -1)
    {
        return GetTextSlice(text, (value, buffer, range) => new TextSlice(buffer, range)
        {
            Value = value
        }, capacity);
    }

    public TextSlice GetTextSlice(ReadOnlySpan<char> text,
        Func<ReadOnlySpan<char>, TextBuffer, Range, TextSlice> factory, int capacity = -1)
    {
        if (capacity < 0)
            capacity = text.Length;

        int charactersCapacity;
        lock (Characters)
        {
            if ((charactersCapacity = Characters.Capacity) < _currentOffset + capacity)
                Characters.ResizeCPUBuffer(charactersCapacity = _currentOffset + capacity);
        }

        var range = GetFreeRangeIfExists(capacity, charactersCapacity);
        if (range == null)
        {
            range = new Range(_currentOffset, _currentOffset + capacity);
            _currentOffset += capacity;
        }

        var slice = factory(text, this, range.Value);
        lock (_usedRanges)
        {
            _usedRanges.Add(slice, range.Value);
        }

        return slice;
    }

    private Range? GetFreeRangeIfExists(int capacity, int charactersCapacity)
    {
        lock (_freeRanges)
        {
            if (_freeRanges.Count <= 0) return null;
            for (var i = 0; i < _freeRanges.Count; i++)
            {
                var freeRange = _freeRanges[i];
                var (offset, length) = freeRange.GetOffsetAndLength(charactersCapacity);
                if (length < capacity) continue;

                _freeRanges.RemoveAt(i);

                if (length <= capacity) return new Range(offset, offset + capacity);
                var remainingRange = new Range(offset + capacity, offset + length);
                _freeRanges.Add(remainingRange);

                return new Range(offset, offset + capacity);
            }
        }

        return null;
    }

    public void RenderBuffer(Camera camera, int endIndex = -1)
    {
        if (endIndex < 0)
            endIndex = _currentOffset;

        _vao.Bind();
        _vao.Update();
        TextProvider.BindAndSetUniforms(camera, Vector4.One);

        GL.DrawElementsInstanced(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt,
            IntPtr.Zero, endIndex);

        Span<char>
            characterHandleString = stackalloc char[11]; // 11 is the max characters an int can be represented with
        Characters.Handle.TryFormat(characterHandleString, out _);

        RenderMarker.Debug("Rendered Text Buffer: ", characterHandleString, MarkerType.Hidden);
    }

    public void Remove(TextSlice textSlice)
    {
        Range range;
        lock (_usedRanges)
        {
            range = _usedRanges[textSlice];
            _usedRanges.Remove(textSlice);
        }

        lock (_freeRanges)
        {
            _freeRanges.Add(range);
        }

        if (_disposing) return;

        var (offset, length) = range.GetOffsetAndLength(Characters.Capacity);
        for (var i = offset; i < length; i++) Characters[i] = new TextCharacter();
    }
}