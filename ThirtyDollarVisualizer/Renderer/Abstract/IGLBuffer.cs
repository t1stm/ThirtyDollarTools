using ThirtyDollarVisualizer.Renderer.Enums;

namespace ThirtyDollarVisualizer.Renderer.Abstract;

/// <summary>
/// Represents a GPU buffer interface for storing data designed to be used with OpenGL rendering.
/// Supports unmanaged generic data types and provides functionality to manage buffer data and lifecycle.
/// </summary>
/// <typeparam name="TDataType">The type of data stored in the buffer, constrained to unmanaged types.</typeparam>
public interface IGLBuffer<TDataType> : IBuffer, IDisposable where TDataType : unmanaged
{
    /// <summary>
    /// Gets the current state of the buffer's creation process.
    /// Indicates whether the buffer is pending creation, successfully created, or has encountered a failure.
    /// </summary>
    /// <remarks>
    /// The state is represented using the <see cref="CreationState"/> enumeration, which supports flags to convey multiple states if necessary.
    /// </remarks>
    CreationState CreationState { get; }

    /// <summary>
    /// Gets the maximum number of elements that the buffer can hold.
    /// Represents the allocated capacity of the buffer on the GPU.
    /// </summary>
    /// <remarks>
    /// The capacity defines the amount of memory reserved for the buffer.
    /// When the buffer needs to store more data than its current capacity,
    /// it may require resizing, which could involve reallocation of memory.
    /// </remarks>
    int Capacity { get; }

    /// <summary>
    /// Provides indexed access to data within a GPU buffer, allowing retrieval and modification of individual elements at the specified index.
    /// Accessing or setting values using this property directly interacts with the internal buffer or its update mechanism.
    /// </summary>
    /// <param name="index">The zero-based index of the element to access or modify within the buffer.</param>
    /// <returns>The element of type <typeparamref name="TDataType"/> at the specified index.</returns>
    TDataType this[int index] { get; set; }

    /// <summary>
    /// Sets the buffer data, replacing all existing content with the provided data.
    /// </summary>
    /// <param name="newData">The data to set in the buffer as a read-only span of the buffer's data type.</param>
    void SetBufferData(ReadOnlySpan<TDataType> newData);
}