using OpenTK.Graphics.ES20;

namespace ThirtyDollarVisualizer.Engine.Next.Abstractions.Buffers;

public abstract class BufferObject<TDataType> : IDisposable where TDataType : unmanaged
{
    public uint Handle { get; protected set; }
    public int Length { get; protected set; }
    public BufferTarget Target { get; protected set; }

    public abstract void SetBufferData(Span<TDataType> data, BufferTarget buffer_type,
        BufferUsageHint draw_hint = BufferUsageHint.DynamicDraw);

    public abstract void Bind();
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}