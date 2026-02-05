namespace ThirtyDollarVisualizer.Engine.Renderer.Enums;

[Flags]
public enum BufferState
{
    PendingCreation = 1,
    Created = 1 << 1,
    Failed = 1 << 2,
    PendingUpload = 1 << 3
}