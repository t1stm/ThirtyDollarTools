using Sundex.Components.Panels;

namespace Sundex.Components.Abstractions;

/// <summary>
///     An element's end. Removing an element from its parent only stops it drawing; what it holds
///     beyond the managed heap (a label's GPU text buffer, an image's texture and its per-frame
///     update) stays until <see cref="Dispose" />. A container disposes its children with it.
/// </summary>
public abstract partial class UIElement : IDisposable
{
    public bool IsDisposed { get; private set; }

    /// <summary>Takes the element out of its parent and frees what it holds. Safe to call twice.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        if (Parent is Panel parent) parent.RemoveChild(this);
        else StopRendering();
        Context.UnregisterUpdate(this);
        Context.UnwatchMedia(this);
        OnDispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Frees what this element holds. Called once, by <see cref="Dispose" />.</summary>
    protected virtual void OnDispose()
    {
    }
}
