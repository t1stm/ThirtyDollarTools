using System.Collections;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;
using ThirtyDollarVisualizer.Engine.Renderer.Queues;

namespace ThirtyDollarVisualizer.Engine.Renderer.Buffers;

/// <summary>
///     Represents a generic GPU buffer that behaves like a list, providing dynamic array functionality
///     with OpenGL buffer management. This class combines the performance of GPU buffers with the
///     convenience of standard .NET collections.
/// </summary>
/// <typeparam name="TDataType">The unmanaged data type stored in the buffer (e.g., vertex data, indices)</typeparam>
/// <remarks>
///     This class maintains a CPU cache to minimize GPU memory transfers and supports tracked references
///     for efficient updates. It automatically manages buffer capacity expansion and provides full
///     <see cref="IList&lt;TDataType&gt;" /> compatibility for seamless integration with existing code.
/// </remarks>
public class GLBufferList<TDataType>(DeleteQueue deleteQueue)
    : IGPUBuffer<TDataType>, IList<TDataType> where TDataType : unmanaged
{
    public GLBufferList(DeleteQueue deleteQueue, int capacity) : this(deleteQueue)
    {
        Buffer.ResizeCPUBuffer(capacity);
    }

    protected GLBuffer<TDataType>.WithCPUCache Buffer { get; } = new(deleteQueue, BufferTarget.ArrayBuffer);
    protected Dictionary<int, TrackedBufferReference<TDataType>> TrackedBufferReferences { get; set; } = [];

    /// <summary>
    ///     Gets the current creation state of the underlying OpenGL buffer.
    /// </summary>
    public BufferState BufferState => Buffer.BufferState;

    /// <summary>
    ///     Gets the OpenGL handle for the underlying buffer object.
    /// </summary>
    public int Handle => Buffer.Handle;

    /// <summary>
    ///     Gets the current capacity of the buffer (total allocated space).
    /// </summary>
    public int Capacity => Buffer.Capacity;

    /// <summary>
    ///     Binds this buffer as the current OpenGL array buffer.
    /// </summary>
    public void Bind()
    {
        Buffer.Bind();
    }

    /// <summary>
    ///     Updates the GPU buffer with any pending CPU-side changes.
    /// </summary>
    public void Update()
    {
        Buffer.Update();
    }

    /// <summary>
    ///     Sets the entire buffer data, replacing all existing content.
    /// </summary>
    /// <param name="data">The data to copy into the buffer</param>
    public void Dangerous_SetBufferData(ReadOnlySpan<TDataType> data)
    {
        Buffer.Dangerous_SetBufferData(data);
    }

    /// <summary>
    ///     Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get or set</param>
    /// <returns>The element at the specified index</returns>
    public TDataType this[int index]
    {
        get => Buffer[index];
        set => Buffer[index] = value;
    }

    /// <summary>
    ///     Releases all resources used by the buffer list, including the underlying OpenGL buffer.
    /// </summary>
    public void Dispose()
    {
        Buffer.Dispose();
        TrackedBufferReferences.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets a value indicating whether the buffer list is read-only. Always returns false.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    ///     Gets or sets the number of elements currently stored in the buffer list.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    ///     Inserts an element at the specified index, shifting subsequent elements to the right.
    ///     Automatically expands capacity if needed and updates tracked references.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the element</param>
    /// <param name="item">The element to insert</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when index is negative or greater than Count</exception>
    public void Insert(int index, TDataType item)
    {
        if (index < 0 || index > Count)
            throw new IndexOutOfRangeException();

        ExpandCapacityIfNeeded(Count + 1);

        // Shift items to the right
        for (var i = Count; i > index; i--) Buffer[i] = Buffer[i - 1];

        // Insert the new item
        Buffer[index] = item;
        Count++;

        AdjustTrackedReferencesAfterInsertion(index);
    }

    /// <summary>
    ///     Removes the element at the specified index, shifting subsequent elements to the left.
    ///     Updates tracked references accordingly.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when index is negative or greater than or equal to Count</exception>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Count)
            throw new IndexOutOfRangeException();

        for (var i = index; i < Count - 1; i++) Buffer[i] = Buffer[i + 1];

        Count = Math.Max(0, Count - 1);
        Buffer[Count] = default;

        AdjustTrackedReferencesAfterRemoval(index);
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the buffer list.
    /// </summary>
    /// <returns>An enumerator for the buffer list</returns>
    public IEnumerator<TDataType> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return Buffer[i];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Adds an element to the end of the buffer list. Automatically expands capacity if needed.
    /// </summary>
    /// <param name="item">The element to add</param>
    public void Add(TDataType item)
    {
        ExpandCapacityIfNeeded(Count + 1);
        Buffer[Count] = item;
        Count++;
    }

    /// <summary>
    ///     Removes all elements from the buffer list, setting them to their default values.
    /// </summary>
    public void Clear()
    {
        for (var i = 0; i < Count; i++) Buffer[i] = default;

        Count = 0;
    }

    /// <summary>
    ///     Searches for the specified element and returns the zero-based index of the first occurrence.
    /// </summary>
    /// <param name="item">The element to locate</param>
    /// <returns>The zero-based index of the first occurrence, or -1 if not found</returns>
    public int IndexOf(TDataType item)
    {
        for (var index = 0; index < Buffer.Data.Length; index++)
        {
            var searchItem = Buffer.Data[index];
            if (searchItem.Equals(item))
                return index;
        }

        return -1;
    }

    /// <summary>
    ///     Determines whether the buffer list contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate</param>
    /// <returns>true if the element is found; otherwise, false</returns>
    public bool Contains(TDataType item)
    {
        return IndexOf(item) != -1;
    }

    /// <summary>
    ///     Copies the elements of the buffer list to an array, starting at the specified array index.
    /// </summary>
    /// <param name="array">The destination array</param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins</param>
    public void CopyTo(TDataType[] array, int arrayIndex)
    {
        var span = array.AsSpan();
        for (var i = arrayIndex; i < Count; i++)
        {
            var item = Buffer[i];
            span[i] = item;
        }
    }

    /// <summary>
    ///     This method is not supported for value types. Use RemoveAt instead.
    /// </summary>
    /// <param name="item">The element to remove (not used)</param>
    /// <returns>Never returns; always throws an exception</returns>
    /// <exception cref="NotSupportedException">Always thrown as this operation is not supported for value types</exception>
    public bool Remove(TDataType item)
    {
        throw new NotSupportedException(
            "Remove is not supported for value types. Use Remove(TrackedBufferReference<TDataType> item) or RemoveAt instead.");
    }

    /// <summary>
    ///     Gets a tracked reference to the element at the specified index. The reference automatically
    ///     updates the GPU buffer when the referenced value is modified.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get a reference for</param>
    /// <returns>A tracked reference that can be used to modify the element efficiently</returns>
    public TrackedBufferReference<TDataType> GetReferenceAt(int index)
    {
        return GetOrAddReference(index);
    }

    protected TrackedBufferReference<TDataType> GetOrAddReference(int index)
    {
        if (TrackedBufferReferences.TryGetValue(index, out var obj))
            return obj;

        obj = new TrackedBufferReference<TDataType>
        {
            Lookup = ReferenceLookup,
            Update = ReferenceUpdate,
            Index = index
        };

        TrackedBufferReferences.Add(index, obj);
        return obj;
    }

    protected TDataType ReferenceLookup(int index)
    {
        return Buffer[index];
    }

    protected void ReferenceUpdate(int index, TDataType value)
    {
        Buffer[index] = value;
    }

    protected void AdjustTrackedReferencesAfterInsertion(int insertIndex)
    {
        var referencesToUpdate = new List<KeyValuePair<int, TrackedBufferReference<TDataType>>>();

        foreach (var kvp in TrackedBufferReferences)
        {
            if (kvp.Key < insertIndex) continue;
            referencesToUpdate.Add(kvp);
        }

        foreach (var kvp in referencesToUpdate)
        {
            TrackedBufferReferences.Remove(kvp.Key);
            kvp.Value.Index = kvp.Key + 1;
            TrackedBufferReferences[kvp.Key + 1] = kvp.Value;
        }
    }

    protected void AdjustTrackedReferencesAfterRemoval(int removeIndex)
    {
        var referencesToUpdate = new List<KeyValuePair<int, TrackedBufferReference<TDataType>>>();

        TrackedBufferReferences.Remove(removeIndex);
        foreach (var kvp in TrackedBufferReferences)
        {
            if (kvp.Key <= removeIndex) continue;
            referencesToUpdate.Add(kvp);
        }

        foreach (var kvp in referencesToUpdate)
        {
            TrackedBufferReferences.Remove(kvp.Key);
            kvp.Value.Index = kvp.Key - 1;
            TrackedBufferReferences[kvp.Key - 1] = kvp.Value;
        }
    }

    /// <summary>
    ///     Expands the buffer capacity if the specified length exceeds the current capacity.
    ///     Uses a doubling strategy to minimize frequent reallocations.
    /// </summary>
    /// <param name="newLength">The minimum required capacity</param>
    public void ExpandCapacityIfNeeded(int newLength)
    {
        if (newLength < Capacity)
            return;

        var newCapacity = Math.Max(newLength, Capacity * 2);
        Buffer.ResizeCPUBuffer(newCapacity);
    }

    /// <summary>
    ///     Removes a tracked buffer reference from the list and adjusts the buffer accordingly.
    /// </summary>
    /// <param name="item">
    ///     The tracked buffer reference to remove, which contains the index of the buffer element to be
    ///     removed.
    /// </param>
    /// <returns>True if the item was successfully removed; otherwise, false.</returns>
    /// <throws cref="IndexOutOfRangeException">
    ///     Thrown when the TrackedBufferReference gives an item that is out of the range of the buffer.
    ///     This happens only when the object isn't synced properly and the Remove method is called from a thread that isn't
    ///     the render thread.
    /// </throws>
    public bool Remove(TrackedBufferReference<TDataType> item)
    {
        var index = item.Index;
        RemoveAt(index);
        return true;
    }
}