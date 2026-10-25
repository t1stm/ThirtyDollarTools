using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Data_Buffers;
using Sundex.Engine.Renderer.Queues;

namespace Sundex.Engine.Text;

public class TextBuffer : IRenderable, IClippable, IDisposable
{
    public readonly GLBuffer<TextCharacter>.WithCPUCache Characters;
    public readonly ITextProvider TextProvider;

    private readonly List<Range> _freeRanges = [];
    private readonly Dictionary<TextSlice, Range> _usedRanges = [];

    private readonly VertexArrayObject _vao = new();

    private int _currentOffset;
    private bool _disposing;

    public TextBuffer(ITextProvider provider, DeleteQueue deleteQueue)
    {
        Characters = new GLBuffer<TextCharacter>.WithCPUCache(
            deleteQueue, BufferTarget.ArrayBuffer);
        TextProvider = provider;
        InitializeVAO(_vao, Characters);
    }

    /// <inheritdoc />
    public Vector4i? ClipRect { get; set; }

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
        var range = TakeRange(capacity < 0 ? text.Length : capacity);
        var slice = factory(text, this, range);
        lock (_usedRanges)
        {
            _usedRanges.Add(slice, range);
        }

        return slice;
    }

    /// <summary>
    ///     Forgets every slice at once and starts laying text out from the top again, keeping
    ///     the characters' storage - for an owner that refills the buffer and hands its old
    ///     slices back in through <see cref="Reuse" /> rather than allocating new ones. A slice
    ///     from before the reset is dead until then: don't set it or dispose it.
    /// </summary>
    public void Reset()
    {
        lock (_usedRanges)
        {
            _usedRanges.Clear();
        }

        lock (_freeRanges)
        {
            _freeRanges.Clear();
        }

        _currentOffset = 0;
    }

    /// <summary>
    ///     <see cref="GetTextSlice(ReadOnlySpan{char}, int)" /> into a slice this buffer handed
    ///     out before the last <see cref="Reset" />, so a refill allocates nothing. The slice
    ///     keeps its own settings (font size, color, manual updates).
    /// </summary>
    public void Reuse(TextSlice slice, ReadOnlySpan<char> text, int capacity = -1)
    {
        var range = TakeRange(capacity < 0 ? text.Length : capacity);
        slice.Rebind(range);
        lock (_usedRanges)
        {
            _usedRanges.Add(slice, range);
        }

        slice.Value = text;
    }

    private Range TakeRange(int capacity)
    {
        int charactersCapacity;
        lock (Characters)
        {
            // Doubling, as a list grows: a chunk adds its slices one at a time, and growing
            // to the exact size copied the whole buffer on every one of them.
            if ((charactersCapacity = Characters.Capacity) < _currentOffset + capacity)
                Characters.ResizeCPUBuffer(charactersCapacity =
                    Math.Max(_currentOffset + capacity, charactersCapacity * 2));
        }

        var range = GetFreeRangeIfExists(capacity, charactersCapacity);
        if (range != null) return range.Value;

        var taken = new Range(_currentOffset, _currentOffset + capacity);
        _currentOffset += capacity;
        return taken;
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
        TextProvider.BindAndSetUniforms(camera);

        GLQuad.DrawInstanced(endIndex);

#if DEBUG
        Span<char> characterHandleString
            = stackalloc char[11]; // 11 is the max characters an int can be represented with
        Characters.Handle.TryFormat(characterHandleString, out _);

        RenderMarker.Debug("Rendered Text Buffer: ", characterHandleString, MarkerType.Hidden);
#endif
    }

    public void Remove(TextSlice textSlice)
    {
        Range range;
        lock (_usedRanges)
        {
            range = _usedRanges[textSlice];
        }

        if (!_disposing)
        {
            var (offset, length) = range.GetOffsetAndLength(Characters.Capacity);
            for (var i = offset; i < offset + length; i++) Characters[i] = new TextCharacter();
        }

        lock (_usedRanges)
        {
            _usedRanges.Remove(textSlice);
        }

        lock (_freeRanges)
        {
            _freeRanges.Add(range);
        }
    }
}